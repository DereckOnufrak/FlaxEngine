#if USE_LARGE_WORLDS
using Real = System.Double;
#else
using Real = System.Single;
#endif

using System;
using FlaxEditor;
using FlaxEditor.GUI.ContextMenu;
using FlaxEditor.SceneGraph;
using FlaxEditor.Windows;
using FlaxEngine;

namespace ConvexBrush
{
    /// <summary>
    /// Scene graph node for <see cref="ConvexBrushCollider"/>. Exposes every brush face as a selectable child node (like CSG brush surfaces).
    /// </summary>
    [HideInEditor]
    public sealed class ConvexBrushColliderNode : ActorNode
    {
        private static readonly Color FaceHighlightColor = new Color(1.0f, 0.6f, 0.0f, 0.35f);
        private static readonly Color FaceOutlineColor = new Color(1.0f, 0.75f, 0.1f, 1.0f);
        private static readonly Color BrushEdgesColor = new Color(0.4f, 0.9f, 1.0f, 0.9f);

        /// <summary>
        /// Selectable brush face sub-node. Translate gizmo moves the face along its normal, rotate gizmo tilts the face around its center.
        /// </summary>
        public sealed class FaceNode : ActorChildNode<ConvexBrushColliderNode>
        {
            /// <summary>
            /// Inspector proxy for a single face.
            /// </summary>
            private sealed class FaceProxy
            {
                [HideInEditor]
                public ConvexBrushCollider Brush;

                [HideInEditor]
                public int Index;

                [EditorOrder(0), EditorDisplay("Face"), Tooltip("The outward face normal (in actor local space).")]
                public Float3 Normal
                {
                    get => Brush.GetFace(Index).Normal;
                    set
                    {
                        var face = Brush.GetFace(Index);
                        face.Normal = value;
                        Brush.SetFace(Index, face);
                    }
                }

                [EditorOrder(10), EditorDisplay("Face"), Tooltip("The face plane distance from the actor origin along the normal (in actor local space units).")]
                public float Distance
                {
                    get => Brush.GetFace(Index).Distance;
                    set
                    {
                        var face = Brush.GetFace(Index);
                        face.Distance = value;
                        Brush.SetFace(Index, face);
                    }
                }
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="FaceNode"/> class.
            /// </summary>
            /// <param name="node">The brush node.</param>
            /// <param name="id">The node ID.</param>
            /// <param name="index">The face index.</param>
            public FaceNode(ConvexBrushColliderNode node, Guid id, int index)
            : base(node, id, index)
            {
            }

            /// <summary>
            /// Gets the brush actor.
            /// </summary>
            public ConvexBrushCollider Brush => (ConvexBrushCollider)_node.Actor;

            private bool IsValidIndex => Index < Brush.FacesCount;

            /// <inheritdoc />
            public override string Name => ParentNode.Name + ".Face" + Index;

            /// <inheritdoc />
            public override Transform Transform
            {
                get
                {
                    var brush = Brush;
                    if (!IsValidIndex)
                        return brush.Transform;
                    return brush.Transform.LocalToWorld(brush.GetFaceLocalTransform(Index));
                }
                set
                {
                    var brush = Brush;
                    if (!IsValidIndex)
                        return;
                    var local = brush.Transform.WorldToLocal(value);
                    brush.SetFaceLocalTransform(Index, local);
                }
            }

            /// <inheritdoc />
            public override object EditableObject => new FaceProxy
            {
                Brush = Brush,
                Index = Index,
            };

            /// <inheritdoc />
            public override bool RayCastSelf(ref RayCastData ray, out Real distance, out Vector3 normal)
            {
                if (!IsValidIndex)
                {
                    distance = 0;
                    normal = Vector3.Up;
                    return false;
                }
                return Brush.IntersectsFace(Index, ref ray.Ray, out distance, out normal);
            }

            /// <inheritdoc />
            public override void OnDebugDraw(ViewportDebugDrawData data)
            {
                ParentNode.OnDebugDraw(data);
                if (!IsValidIndex)
                    return;

                // Highlight selected face
                var brush = Brush;
                var geometry = brush.Geometry;
                brush.Transform.GetWorld(out Matrix world);
                var triangles = geometry.GetFaceTriangles(Index, true);
                if (triangles.Length != 0)
                    DebugDraw.DrawTriangles(triangles, world, FaceHighlightColor, 0.0f, false);
                var lines = geometry.GetFaceLines(Index);
                if (lines.Length != 0)
                    DebugDraw.DrawLines(lines, world, FaceOutlineColor, 0.0f, false);
            }

            /// <inheritdoc />
            public override void OnContextMenu(ContextMenu contextMenu, EditorWindow window)
            {
                ParentNode.OnContextMenu(contextMenu, window);

                contextMenu.AddSeparator();
                contextMenu.AddButton("Duplicate face (bevel)", () => _node.EditBrush("Duplicate brush face", b => b.DuplicateFace(Index)));
                var remove = contextMenu.AddButton("Remove face", () => _node.EditBrush("Remove brush face", b => b.RemoveFace(Index)));
                remove.Enabled = Brush.FacesCount > 4;
            }
        }

        /// <inheritdoc />
        public ConvexBrushColliderNode(Actor actor)
        : base(actor)
        {
            Brush.FacesChanged += SyncFaceNodes;
            SyncFaceNodes();
        }

        /// <summary>
        /// Gets the brush actor.
        /// </summary>
        public ConvexBrushCollider Brush => (ConvexBrushCollider)Actor;

        /// <inheritdoc />
        public override bool AffectsNavigation => true;

        /// <summary>
        /// Applies the brush modification with undo support.
        /// </summary>
        /// <param name="actionName">The undo action name.</param>
        /// <param name="edit">The edit callback.</param>
        public void EditBrush(string actionName, Action<ConvexBrushCollider> edit)
        {
            var brush = Brush;
            using (new UndoBlock(Root?.Undo, brush, actionName))
                edit(brush);
            SyncFaceNodes();
            Editor.Instance.Scene.MarkSceneEdited(brush.Scene);
        }

        /// <summary>
        /// Synchronizes child face nodes with the brush faces count.
        /// </summary>
        public void SyncFaceNodes()
        {
            var brush = Actor as ConvexBrushCollider;
            if (brush == null)
                return;
            var dstCount = brush.FacesCount;
            var srcCount = ActorChildNodes?.Count ?? 0;
            if (dstCount == srcCount)
                return;

            // Remove unused faces
            while (srcCount > dstCount)
            {
                var node = ActorChildNodes[--srcCount];
                Root?.SceneContext.Deselect(node);
                node.Dispose();
            }

            // Add new faces
            var id = ID;
            while (srcCount < dstCount)
            {
                AddChildNode(new FaceNode(this, GetSubID(id, srcCount), srcCount));
                srcCount++;
            }
        }

        /// <inheritdoc />
        public override bool RayCastSelf(ref RayCastData ray, out Real distance, out Vector3 normal)
        {
            SyncFaceNodes();

            // Brush is always pickable by its faces (like CSG brushes, unlike other colliders that require physics debug view)
            return Brush.IntersectsBrush(ref ray.Ray, out distance, out normal, out _);
        }

        /// <inheritdoc />
        public override void OnDebugDraw(ViewportDebugDrawData data)
        {
            base.OnDebugDraw(data);
            SyncFaceNodes();

            // Draw brush edges
            var brush = Brush;
            var geometry = brush.Geometry;
            brush.Transform.GetWorld(out Matrix world);
            for (int i = 0; i < geometry.Polygons.Length; i++)
            {
                var lines = geometry.GetFaceLines(i);
                if (lines.Length != 0)
                    DebugDraw.DrawLines(lines, world, geometry.IsValid ? BrushEdgesColor : Color.Red, 0.0f, true);
            }
        }

        /// <inheritdoc />
        public override void OnContextMenu(ContextMenu contextMenu, EditorWindow window)
        {
            base.OnContextMenu(contextMenu, window);

            contextMenu.AddButton("Reset brush to box", () => EditBrush("Reset brush to box", b => b.ResetToBox(ConvexBrushCollider.DefaultSize)));
        }

        /// <inheritdoc />
        public override void OnDispose()
        {
            if (Actor is ConvexBrushCollider brush)
                brush.FacesChanged -= SyncFaceNodes;

            base.OnDispose();
        }
    }
}
