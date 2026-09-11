// Copyright (c) Wojciech Figat. All rights reserved.

#if USE_LARGE_WORLDS
using Real = System.Double;
#else
using Real = System.Single;
#endif

using System.Collections.Generic;
using FlaxEditor.Gizmo;
using FlaxEngine;
using MeshModeling;

namespace MeshModelingEditor
{
    /// <summary>
    /// Gizmo for the in-editor mesh modeling tool. Picks vertices/edges/faces of the targeted
    /// <see cref="ModelingGizmoMode.SelectedMesh"/> under the cursor, shows a 3-axis move handle on the current
    /// selection, drags the selection (along a handle axis, or freely within the camera-facing plane when clicking
    /// the mesh directly), and draws a wireframe/selection overlay.
    /// </summary>
    /// <seealso cref="FlaxEditor.Gizmo.GizmoBase" />
    [HideInEditor]
    public class ModelingGizmo : GizmoBase
    {
        private const float HandleLength = 60.0f;
        private const float HandlePickDistance = 6.0f;

        /// <summary>
        /// The parent mode.
        /// </summary>
        public readonly ModelingGizmoMode Mode;

        private bool _wasMouseDown;
        private bool _isDragging;
        private float _dragDistance;
        private EditableMeshData _dragSnapshot;

        // Free (camera-plane) drag state - used when the click starts on the mesh itself, not a handle.
        private Vector3 _dragPlanePoint;
        private Float3 _dragPlaneNormal;
        private Vector3 _lastDragPoint;

        // Axis-constrained drag state - used when the click starts on one of the move-handle axes.
        private int _dragAxis = -1;
        private Vector3 _axisOrigin;
        private Float3 _axisDir;
        private float _lastAxisT;

        /// <inheritdoc />
        public override bool IsControllingMouse => _isDragging;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModelingGizmo"/> class.
        /// </summary>
        public ModelingGizmo(IGizmoOwner owner, ModelingGizmoMode mode)
        : base(owner)
        {
            Mode = mode;
        }

        /// <inheritdoc />
        public override void Update(float dt)
        {
            if (!IsActive)
                return;
            bool mouseDown = Owner.IsLeftMouseButtonDown;
            var mesh = Mode.SelectedMesh;
            var data = mesh ? mesh.Mesh.Instance : null;

            if (data == null)
            {
                _isDragging = false;
                _wasMouseDown = mouseDown;
                return;
            }

            if (_isDragging)
            {
                if (mouseDown)
                {
                    if (_dragAxis >= 0)
                        ApplyAxisDrag(mesh, data);
                    else
                        ApplyDrag(mesh, data);
                }
                else
                {
                    EndDrag(mesh, data);
                }
            }
            else if (mouseDown && !_wasMouseDown)
            {
                if (!TryBeginAxisDrag(mesh, data))
                    TryPickAndBeginDrag(mesh, data);
            }

            _wasMouseDown = mouseDown;
        }

        private bool TryBeginAxisDrag(EditableMesh mesh, EditableMeshData data)
        {
            if (Mode.SelectedVertexIndices.Count == 0)
                return false;

            var centroid = GetSelectionCentroid(mesh, data);
            var ray = Owner.MouseRay;
            var transform = mesh.Transform;
            Float3[] axes = { transform.Right, transform.Up, transform.Forward };

            int bestAxis = -1;
            float bestDist = HandlePickDistance;
            float bestT = 0.0f;
            for (int i = 0; i < axes.Length; i++)
            {
                ClosestPointRayLine(ray, centroid, axes[i], out float dist, out float t);
                if (t >= 0.0f && t <= HandleLength && dist < bestDist)
                {
                    bestDist = dist;
                    bestAxis = i;
                    bestT = t;
                }
            }
            if (bestAxis == -1)
                return false;

            _dragAxis = bestAxis;
            _axisOrigin = centroid;
            _axisDir = axes[bestAxis];
            _lastAxisT = bestT;
            _dragSnapshot = data.Clone();
            _dragDistance = 0.0f;
            _isDragging = true;
            return true;
        }

        private void ApplyAxisDrag(EditableMesh mesh, EditableMeshData data)
        {
            var ray = Owner.MouseRay;
            ClosestPointRayLine(ray, _axisOrigin, _axisDir, out _, out float t);
            float deltaT = t - _lastAxisT;
            if (Mathf.Abs(deltaT) < 1e-5f)
                return;

            Vector3 worldDelta = (Vector3)_axisDir * deltaT;
            Float3 localDelta = mesh.Transform.WorldToLocalVector(worldDelta);
            EditableMeshBuilder.MoveVertices(data, Mode.SelectedVertexIndices, localDelta);
            mesh.Rebuild();

            _dragDistance += Mathf.Abs(deltaT);
            _lastAxisT = t;
        }

        private void TryPickAndBeginDrag(EditableMesh mesh, EditableMeshData data)
        {
            EditableMeshBuilder.Triangulate(data, out var positions, out var triangles, out _, out _, out var triangleFaces);
            if (!RaycastMesh(mesh, positions, triangles, out int hitTriangle, out Vector3 hitWorldPoint))
            {
                Mode.ClearSelection();
                return;
            }
            int faceIndex = triangleFaces[hitTriangle];

            switch (Mode.SelectionType)
            {
            case ModelingGizmoMode.ElementType.Face:
                Mode.SelectFace(faceIndex, data.GetFaceLoop(faceIndex));
                break;
            case ModelingGizmoMode.ElementType.Vertex:
                Mode.SelectVertices(new[] { NearestLoopVertex(mesh, data, faceIndex, hitWorldPoint) });
                break;
            case ModelingGizmoMode.ElementType.Edge:
                var (a, b) = NearestLoopEdge(mesh, data, faceIndex, hitWorldPoint);
                Mode.SelectVertices(new[] { a, b });
                break;
            }

            if (Mode.SelectedVertexIndices.Count == 0)
                return;

            // Begin a potential drag from this same click - a click-drag in one gesture moves the just-picked
            // selection; a plain click-release ends up with ~0 drag distance and is treated as select-only.
            var centroid = GetSelectionCentroid(mesh, data);
            _dragPlanePoint = centroid;
            _dragPlaneNormal = Owner.ViewDirection;
            var plane = new Plane(_dragPlanePoint, _dragPlaneNormal);
            var ray = Owner.MouseRay;
            if (!CollisionsHelper.RayIntersectsPlane(ref ray, ref plane, out _lastDragPoint))
                _lastDragPoint = centroid;

            _dragAxis = -1;
            _dragSnapshot = data.Clone();
            _dragDistance = 0.0f;
            _isDragging = true;
        }

        private void ApplyDrag(EditableMesh mesh, EditableMeshData data)
        {
            var plane = new Plane(_dragPlanePoint, _dragPlaneNormal);
            var ray = Owner.MouseRay;
            if (!CollisionsHelper.RayIntersectsPlane(ref ray, ref plane, out Vector3 point))
                return;
            Vector3 worldDelta = point - _lastDragPoint;
            if (worldDelta.LengthSquared < 1e-10f)
                return;

            Float3 localDelta = mesh.Transform.WorldToLocalVector(worldDelta);
            EditableMeshBuilder.MoveVertices(data, Mode.SelectedVertexIndices, localDelta);
            mesh.Rebuild();

            _dragDistance += (float)worldDelta.Length;
            _lastDragPoint = point;
            _dragPlanePoint = point;
        }

        private void EndDrag(EditableMesh mesh, EditableMeshData data)
        {
            _isDragging = false;
            _dragAxis = -1;
            if (_dragDistance > 1e-3f)
            {
                var before = _dragSnapshot;
                var after = data.Clone();
                ModelingUtils.CommitEdit(mesh);
                Owner.Undo?.AddAction(new EditGeometryAction(mesh, before, after));
            }
            _dragSnapshot = null;
        }

        private Vector3 GetSelectionCentroid(EditableMesh mesh, EditableMeshData data)
        {
            var indices = Mode.SelectedVertexIndices;
            Vector3 centroid = Vector3.Zero;
            for (int i = 0; i < indices.Count; i++)
                centroid += mesh.Transform.LocalToWorld(data.Positions[indices[i]]);
            if (indices.Count > 0)
                centroid /= indices.Count;
            return centroid;
        }

        private bool RaycastMesh(EditableMesh mesh, Float3[] localPositions, int[] triangles, out int hitTriangle, out Vector3 hitWorldPoint)
        {
            hitTriangle = -1;
            hitWorldPoint = Vector3.Zero;
            var ray = Owner.MouseRay;
            var transform = mesh.Transform;
            float bestDistance = float.MaxValue;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector3 v0 = transform.LocalToWorld(localPositions[triangles[t]]);
                Vector3 v1 = transform.LocalToWorld(localPositions[triangles[t + 1]]);
                Vector3 v2 = transform.LocalToWorld(localPositions[triangles[t + 2]]);
                if (CollisionsHelper.RayIntersectsTriangle(ref ray, ref v0, ref v1, ref v2, out Real distance))
                {
                    float d = (float)distance;
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        hitTriangle = t / 3;
                    }
                }
            }
            if (hitTriangle == -1)
                return false;
            hitWorldPoint = ray.Position + ray.Direction * bestDistance;
            return true;
        }

        /// <summary>
        /// Finds the closest approach between a ray and an infinite line, returning the perpendicular distance
        /// between them and the parameter <paramref name="t"/> such that <paramref name="lineOrigin"/> + t *
        /// <paramref name="lineDir"/> is the closest point on the line. Used both to hit-test the move-handle axes
        /// and to drive axis-constrained dragging.
        /// </summary>
        private static void ClosestPointRayLine(Ray ray, Vector3 lineOrigin, Float3 lineDir, out float distance, out float t)
        {
            Vector3 d1 = ray.Direction;
            Vector3 d2 = lineDir;
            Vector3 r = ray.Position - lineOrigin;
            float a = (float)Vector3.Dot(d1, d1);
            float e = (float)Vector3.Dot(d2, d2);
            float f = (float)Vector3.Dot(d2, r);
            float b = (float)Vector3.Dot(d1, d2);
            float c = (float)Vector3.Dot(d1, r);
            float denom = a * e - b * b;
            float t1 = Mathf.Abs(denom) > 1e-6f ? (b * f - c * e) / denom : 0.0f;
            if (t1 < 0.0f)
                t1 = 0.0f; // The handle must be in front of the camera.
            t = e > 1e-8f ? (b * t1 + f) / e : 0.0f;
            Vector3 closestOnRay = ray.Position + d1 * t1;
            Vector3 closestOnLine = lineOrigin + (Vector3)(d2 * t);
            distance = (float)Vector3.Distance(closestOnRay, closestOnLine);
        }

        private static int NearestLoopVertex(EditableMesh mesh, EditableMeshData data, int faceIndex, Vector3 hitWorldPoint)
        {
            var loop = data.GetFaceLoop(faceIndex);
            int best = loop[0];
            float bestDist = float.MaxValue;
            foreach (var idx in loop)
            {
                float d = (float)Vector3.DistanceSquared(mesh.Transform.LocalToWorld(data.Positions[idx]), hitWorldPoint);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = idx;
                }
            }
            return best;
        }

        private static (int, int) NearestLoopEdge(EditableMesh mesh, EditableMeshData data, int faceIndex, Vector3 hitWorldPoint)
        {
            var loop = data.GetFaceLoop(faceIndex);
            int bestA = loop[0], bestB = loop[loop.Count > 1 ? 1 : 0];
            float bestDist = float.MaxValue;
            for (int i = 0; i < loop.Count; i++)
            {
                int a = loop[i], b = loop[(i + 1) % loop.Count];
                Vector3 pa = mesh.Transform.LocalToWorld(data.Positions[a]);
                Vector3 pb = mesh.Transform.LocalToWorld(data.Positions[b]);
                float d = DistancePointSegmentSquared(hitWorldPoint, pa, pb);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestA = a;
                    bestB = b;
                }
            }
            return (bestA, bestB);
        }

        private static float DistancePointSegmentSquared(Float3 p, Float3 a, Float3 b)
        {
            var ab = b - a;
            float len2 = ab.LengthSquared;
            float t = len2 > 1e-8f ? Float3.Dot(p - a, ab) / len2 : 0.0f;
            t = Mathf.Saturate(t);
            var closest = a + ab * t;
            return (p - closest).LengthSquared;
        }

        /// <inheritdoc />
        public override void Draw(ref RenderContext renderContext)
        {
            if (!IsActive)
                return;
            var mesh = Mode.SelectedMesh;
            var data = mesh ? mesh.Mesh.Instance : null;
            if (data == null)
                return;
            var transform = mesh.Transform;

            // Wireframe of the whole mesh, for context.
            EditableMeshBuilder.Triangulate(data, out var positions, out var triangles, out _, out _);
            var worldPositions = new Float3[positions.Length];
            for (int i = 0; i < positions.Length; i++)
                worldPositions[i] = transform.LocalToWorld(positions[i]);
            DebugDraw.DrawWireTriangles(worldPositions, triangles, Color.Gray);

            if (Mode.SelectionType == ModelingGizmoMode.ElementType.Face)
            {
                // Fill the selected face.
                if (Mode.SelectedFaceIndex >= 0 && Mode.SelectedFaceIndex < data.Faces.Count)
                {
                    var loop = data.GetFaceLoop(Mode.SelectedFaceIndex);
                    var fill = new List<Float3>();
                    for (int i = 1; i < loop.Count - 1; i++)
                    {
                        fill.Add(transform.LocalToWorld(data.Positions[loop[0]]));
                        fill.Add(transform.LocalToWorld(data.Positions[loop[i]]));
                        fill.Add(transform.LocalToWorld(data.Positions[loop[i + 1]]));
                    }
                    if (fill.Count > 0)
                        DebugDraw.DrawTriangles(fill.ToArray(), new Color(1.0f, 0.6f, 0.0f, 0.35f));
                }
            }
            else
            {
                // Vertex/edge modes: show every vertex as a small dot, brighter when selected.
                for (int i = 0; i < data.Positions.Count; i++)
                {
                    bool selected = Mode.SelectedVertexIndices.Contains(i);
                    var world = transform.LocalToWorld(data.Positions[i]);
                    DebugDraw.DrawSphere(new BoundingSphere(world, selected ? 3.0f : 1.5f), selected ? Color.Orange : Color.White);
                }
                if (Mode.SelectionType == ModelingGizmoMode.ElementType.Edge && Mode.SelectedVertexIndices.Count == 2)
                {
                    var a = transform.LocalToWorld(data.Positions[Mode.SelectedVertexIndices[0]]);
                    var b = transform.LocalToWorld(data.Positions[Mode.SelectedVertexIndices[1]]);
                    DebugDraw.DrawLine(a, b, Color.Orange);
                }
            }

            // 3-axis move handle at the current selection, so there's something to grab besides clicking the mesh.
            if (Mode.SelectedVertexIndices.Count > 0)
            {
                var centroid = GetSelectionCentroid(mesh, data);
                DrawAxisHandle(centroid, transform.Right, Color.Red);
                DrawAxisHandle(centroid, transform.Up, Color.Green);
                DrawAxisHandle(centroid, transform.Forward, Color.Blue);
            }
        }

        private static void DrawAxisHandle(Vector3 origin, Float3 direction, Color color)
        {
            Vector3 end = origin + (Vector3)direction * HandleLength;
            DebugDraw.DrawLine(origin, end, color);
            DebugDraw.DrawSphere(new BoundingSphere(end, 2.5f), color);
        }
    }
}
