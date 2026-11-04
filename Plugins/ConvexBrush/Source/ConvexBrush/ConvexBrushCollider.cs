#if USE_LARGE_WORLDS
using Real = System.Double;
#else
using Real = System.Single;
#endif

using System;
using System.Runtime.Serialization;
using FlaxEngine;

namespace ConvexBrush
{
    /// <summary>
    /// Convex collider defined by a set of faces (planes), editable in the viewport per-face like a CSG brush.
    /// The collision shape is cooked at runtime into a virtual <see cref="FlaxEngine.CollisionData"/> asset (convex mesh).
    /// </summary>
    /// <remarks>
    /// In a cooked game, enable <c>Physics Settings → Support Cooking At Runtime</c> so the collision can be generated.
    /// </remarks>
    [ActorContextMenu("New/Physics/Colliders/Convex Brush Collider"), ActorToolbox("Physics", "Convex Brush Collider")]
    public class ConvexBrushCollider : MeshCollider
    {
        /// <summary>
        /// The default brush size used when creating a new brush.
        /// </summary>
        public static readonly Float3 DefaultSize = new Float3(100.0f);

        [Serialize]
        private BrushFace[] _faces = CreateBoxFaces(DefaultSize);

        private ConvexBrushGeometry _geometry;
        private int _geometryHash;
        private CollisionData _virtualData;
        private int _cookedHash;
        private bool _cookFailed;
        private bool _invalidLogged;

        /// <summary>
        /// Occurs when brush faces get modified (geometry rebuilt).
        /// </summary>
        public event Action FacesChanged;

        /// <summary>
        /// The brush faces (planes in actor local space). The collider volume is the intersection of all faces.
        /// </summary>
        [NoSerialize, EditorOrder(200), EditorDisplay("Brush"), Tooltip("The brush faces (planes in actor local space). The collider volume is the intersection of all faces.")]
        public BrushFace[] Faces
        {
            get => _faces;
            set
            {
                _faces = value ?? Array.Empty<BrushFace>();
                OnFacesChanged();
            }
        }

        /// <summary>
        /// Gets the amount of faces.
        /// </summary>
        [HideInEditor, NoSerialize]
        public int FacesCount => _faces?.Length ?? 0;

        /// <summary>
        /// Gets the brush geometry (local space) built from the current faces.
        /// </summary>
        [HideInEditor, NoSerialize]
        public ConvexBrushGeometry Geometry
        {
            get
            {
                // Faces array can be modified in-place (eg. by undo) so validate cached geometry
                var hash = ComputeHash(_faces);
                if (_geometry == null || hash != _geometryHash)
                {
                    _geometry = ConvexBrushGeometry.Build(_faces);
                    _geometryHash = hash;
                }
                return _geometry;
            }
        }

        /// <summary>
        /// Creates the faces for an axis-aligned box centered at the origin.
        /// </summary>
        /// <param name="size">The box size.</param>
        /// <returns>The faces.</returns>
        public static BrushFace[] CreateBoxFaces(Float3 size)
        {
            var e = size * 0.5f;
            return new[]
            {
                new BrushFace(new Float3(1, 0, 0), e.X),
                new BrushFace(new Float3(-1, 0, 0), e.X),
                new BrushFace(new Float3(0, 1, 0), e.Y),
                new BrushFace(new Float3(0, -1, 0), e.Y),
                new BrushFace(new Float3(0, 0, 1), e.Z),
                new BrushFace(new Float3(0, 0, -1), e.Z),
            };
        }

        /// <summary>
        /// Resets the brush into an axis-aligned box.
        /// </summary>
        /// <param name="size">The box size.</param>
        public void ResetToBox(Float3 size)
        {
            Faces = CreateBoxFaces(size);
        }

        /// <summary>
        /// Gets the face.
        /// </summary>
        /// <param name="index">The face index.</param>
        /// <returns>The face.</returns>
        public BrushFace GetFace(int index)
        {
            return _faces[index];
        }

        /// <summary>
        /// Sets the face.
        /// </summary>
        /// <param name="index">The face index.</param>
        /// <param name="face">The face.</param>
        public void SetFace(int index, BrushFace face)
        {
            if (_faces[index].Equals(face))
                return;
            var faces = (BrushFace[])_faces.Clone();
            faces[index] = face;
            Faces = faces;
        }

        /// <summary>
        /// Adds a new face. If <paramref name="touchHull"/> is set, the face distance is adjusted so the plane touches the current hull (shape doesn't change until the face is moved inwards).
        /// </summary>
        /// <param name="normal">The face normal (local space).</param>
        /// <param name="touchHull">True if place the face on the current hull boundary.</param>
        /// <param name="distance">The face distance (used when <paramref name="touchHull"/> is false).</param>
        /// <returns>The added face index.</returns>
        public int AddFace(Float3 normal, bool touchHull = true, float distance = 0.0f)
        {
            if (normal.LengthSquared < 1e-12f)
                throw new ArgumentException("Face normal cannot be zero.", nameof(normal));
            var n = normal.Normalized;
            if (touchHull)
            {
                var geometry = Geometry;
                distance = float.MinValue;
                for (int i = 0; i < geometry.Vertices.Length; i++)
                    distance = Math.Max(distance, Float3.Dot(n, geometry.Vertices[i]));
                if (geometry.Vertices.Length == 0)
                    distance = 0.0f;
            }
            var faces = new BrushFace[FacesCount + 1];
            Array.Copy(_faces, faces, FacesCount);
            faces[FacesCount] = new BrushFace(n, distance);
            Faces = faces;
            return faces.Length - 1;
        }

        /// <summary>
        /// Inserts a copy of the face right after it (eg. to rotate it into a bevel).
        /// </summary>
        /// <param name="index">The face index.</param>
        /// <returns>The new face index.</returns>
        public int DuplicateFace(int index)
        {
            var faces = new BrushFace[FacesCount + 1];
            Array.Copy(_faces, 0, faces, 0, index + 1);
            faces[index + 1] = _faces[index];
            Array.Copy(_faces, index + 1, faces, index + 2, FacesCount - index - 1);
            Faces = faces;
            return index + 1;
        }

        /// <summary>
        /// Removes the face.
        /// </summary>
        /// <param name="index">The face index.</param>
        public void RemoveFace(int index)
        {
            var faces = new BrushFace[FacesCount - 1];
            Array.Copy(_faces, 0, faces, 0, index);
            Array.Copy(_faces, index + 1, faces, index, FacesCount - index - 1);
            Faces = faces;
        }

        /// <summary>
        /// Gets the face pivot (polygon center, oriented so forward axis matches the face normal) in actor local space.
        /// </summary>
        /// <param name="index">The face index.</param>
        /// <returns>The local transform.</returns>
        public Transform GetFaceLocalTransform(int index)
        {
            var geometry = Geometry;
            var normal = geometry.Normals[index];
            if (normal.LengthSquared < 1e-12f)
                return Transform.Identity;
            var up = Math.Abs(normal.Y) < 0.99f ? Float3.Up : Float3.Right;
            return new Transform(geometry.GetFaceCenter(index), Quaternion.LookRotation(normal, up));
        }

        /// <summary>
        /// Sets the face plane from a pivot in actor local space (forward axis is the face normal, translation is a point on the face plane).
        /// </summary>
        /// <param name="index">The face index.</param>
        /// <param name="localTransform">The local transform.</param>
        public void SetFaceLocalTransform(int index, Transform localTransform)
        {
            var normal = Float3.Transform(Float3.Forward, localTransform.Orientation).Normalized;
            if (normal.LengthSquared < 1e-12f)
                return;
            Float3 point = localTransform.Translation;
            SetFace(index, new BrushFace(normal, Float3.Dot(normal, point)));
        }

        /// <summary>
        /// Checks if the world-space ray hits the given face.
        /// </summary>
        /// <param name="index">The face index.</param>
        /// <param name="ray">The ray (world space).</param>
        /// <param name="distance">The hit distance.</param>
        /// <param name="normal">The hit surface normal (world space).</param>
        /// <returns>True if face was hit, otherwise false.</returns>
        public bool IntersectsFace(int index, ref Ray ray, out Real distance, out Vector3 normal)
        {
            distance = 0;
            normal = Vector3.Up;
            var geometry = Geometry;
            if (index < 0 || index >= geometry.Polygons.Length)
                return false;
            var transform = Transform;
            Float3 localOrigin = transform.WorldToLocal(ray.Position);
            Float3 localDir = transform.WorldToLocalVector(ray.Direction);
            if (!geometry.IntersectsFace(index, localOrigin, localDir, out var localHit))
                return false;
            var worldHit = transform.LocalToWorld((Vector3)localHit);
            distance = Vector3.Distance(ray.Position, worldHit);
            normal = Vector3.Normalize(transform.LocalToWorldVector((Vector3)geometry.Normals[index]));
            return true;
        }

        /// <summary>
        /// Checks if the world-space ray hits any brush face.
        /// </summary>
        /// <param name="ray">The ray (world space).</param>
        /// <param name="distance">The closest hit distance.</param>
        /// <param name="normal">The hit surface normal (world space).</param>
        /// <param name="faceIndex">The hit face index.</param>
        /// <returns>True if brush was hit, otherwise false.</returns>
        public bool IntersectsBrush(ref Ray ray, out Real distance, out Vector3 normal, out int faceIndex)
        {
            distance = Real.MaxValue;
            normal = Vector3.Up;
            faceIndex = -1;
            for (int i = 0; i < FacesCount; i++)
            {
                if (IntersectsFace(i, ref ray, out var d, out var n) && d < distance)
                {
                    distance = d;
                    normal = n;
                    faceIndex = i;
                }
            }
            return faceIndex != -1;
        }

        /// <summary>
        /// Rebuilds the brush geometry and cooks the collision (skipped if nothing changed).
        /// </summary>
        /// <param name="force">True if force cooking even if faces didn't change.</param>
        public void Rebuild(bool force = false)
        {
            var hash = ComputeHash(_faces);
            if (!force && hash == _cookedHash && _virtualData && CollisionData == _virtualData)
                return;

            var geometry = Geometry;
            if (!geometry.IsValid)
            {
                if (!_invalidLogged)
                    Debug.LogWarning($"Convex Brush Collider '{Name}' faces don't form a closed convex volume. Collision was not updated.", this);
                _invalidLogged = true;
                return;
            }
            _invalidLogged = false;

            if (!_virtualData)
                _virtualData = Content.CreateVirtualAsset<CollisionData>();

            // Detach before cooking so the collider never uses data that is being replaced
            CollisionData = null;
            if (_virtualData.CookCollision(CollisionDataType.ConvexMesh, geometry.Vertices, geometry.Triangles))
            {
                if (!_cookFailed)
                    Debug.LogError($"Convex Brush Collider '{Name}' failed to cook collision. In a cooked game enable Physics Settings option 'Support Cooking At Runtime'.", this);
                _cookFailed = true;
                return;
            }
            _cookFailed = false;
            _cookedHash = hash;
            CollisionData = _virtualData;
        }

        /// <summary>
        /// Detaches the runtime-generated collision data from the collider so it won't be serialized (virtual assets don't exist on disk). Used by the editor before saving the scene.
        /// </summary>
        /// <returns>True if collision data was detached and needs to be restored with <see cref="RestoreRuntimeCollision"/>.</returns>
        public bool DetachRuntimeCollision()
        {
            if (_virtualData && CollisionData == _virtualData)
            {
                CollisionData = null;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Restores the runtime-generated collision data detached with <see cref="DetachRuntimeCollision"/>.
        /// </summary>
        public void RestoreRuntimeCollision()
        {
            if (_virtualData && CollisionData == null)
                CollisionData = _virtualData;
        }

        /// <inheritdoc />
        public override void OnBeginPlay()
        {
            base.OnBeginPlay();

            // Data serialized from another brush instance (eg. duplicated) or stale virtual asset reference - always use own data
            Rebuild(true);
        }

        /// <summary>
        /// Detects faces modified in-place (eg. by undo/redo that edits array elements directly) and updates the geometry and collision.
        /// </summary>
        /// <returns>True if faces were modified, otherwise false.</returns>
        public bool SyncChanges()
        {
            var hash = ComputeHash(_faces);
            bool collisionUpToDate = !IsDuringPlay || (hash == _cookedHash && _virtualData && CollisionData == _virtualData);
            if (_geometry != null && hash == _geometryHash && collisionUpToDate)
                return false;
            if (_invalidLogged && hash == _geometryHash)
                return false; // Still invalid, nothing to do
            OnFacesChanged();
            return true;
        }

        private void OnFacesChanged()
        {
            _geometry = null;
            if (IsDuringPlay)
                Rebuild();
            FacesChanged?.Invoke();
        }

        [OnDeserialized]
        internal void OnDeserialized(StreamingContext context)
        {
            // Eg. undo/redo or prefab changes applied to the existing object
            _faces ??= Array.Empty<BrushFace>();
            OnFacesChanged();
        }

        private static int ComputeHash(BrushFace[] faces)
        {
            var hash = new HashCode();
            if (faces != null)
            {
                for (int i = 0; i < faces.Length; i++)
                    hash.Add(faces[i]);
            }
            return hash.ToHashCode();
        }
    }
}
