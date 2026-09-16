// Copyright (c) Wojciech Figat. All rights reserved.

#if USE_LARGE_WORLDS
using Real = System.Double;
#else
using Real = System.Single;
#endif

using System;
using FlaxEngine;

namespace FlaxEditor.SceneGraph.Actors
{
    /// <summary>
    /// Actor node for <see cref="WedgeBrush"/>.
    /// </summary>
    /// <seealso cref="ActorNode" />
    [HideInEditor]
    public sealed class WedgeBrushNode : ActorNode
    {
        /// <summary>
        /// Sub actor node used to edit the wedge's bounding box (same 6 faces as a box brush).
        /// </summary>
        /// <seealso cref="FlaxEditor.SceneGraph.ActorChildNode{T}" />
        public sealed class SideLinkNode : ActorChildNode<WedgeBrushNode>
        {
            private sealed class BrushSurfaceProxy
            {
                [HideInEditor]
                public WedgeBrush Brush;

                [HideInEditor]
                public int Index;

                [EditorOrder(10), EditorDisplay("Brush")]
                [Tooltip("The material used to render the brush surface.")]
                public MaterialBase Material
                {
                    get => Brush.Surfaces[Index].Material;
                    set
                    {
                        var surfaces = Brush.Surfaces;
                        surfaces[Index].Material = value;
                        Brush.Surfaces = surfaces;
                    }
                }

                [EditorOrder(30), EditorDisplay("Brush", "UV Scale"), Limit(-1000, 1000, 0.01f)]
                [Tooltip("The surface texture coordinates scale.")]
                public Float2 TexCoordScale
                {
                    get => Brush.Surfaces[Index].TexCoordScale;
                    set
                    {
                        var surfaces = Brush.Surfaces;
                        surfaces[Index].TexCoordScale = value;
                        Brush.Surfaces = surfaces;
                    }
                }

                [EditorOrder(40), EditorDisplay("Brush", "UV Offset"), Limit(-1000, 1000, 0.01f)]
                [Tooltip("The surface texture coordinates offset.")]
                public Float2 TexCoordOffset
                {
                    get => Brush.Surfaces[Index].TexCoordOffset;
                    set
                    {
                        var surfaces = Brush.Surfaces;
                        surfaces[Index].TexCoordOffset = value;
                        Brush.Surfaces = surfaces;
                    }
                }

                [EditorOrder(50), EditorDisplay("Brush", "UV Rotation")]
                [Tooltip("The surface texture coordinates rotation angle (in degrees).")]
                public float TexCoordRotation
                {
                    get => Brush.Surfaces[Index].TexCoordRotation;
                    set
                    {
                        var surfaces = Brush.Surfaces;
                        surfaces[Index].TexCoordRotation = value;
                        Brush.Surfaces = surfaces;
                    }
                }

                [EditorOrder(20), EditorDisplay("Brush", "Scale In Lightmap"), Limit(0, 10000, 0.1f)]
                [Tooltip("The scale in lightmap (per surface).")]
                public float ScaleInLightmap
                {
                    get => Brush.Surfaces[Index].ScaleInLightmap;
                    set
                    {
                        var surfaces = Brush.Surfaces;
                        surfaces[Index].ScaleInLightmap = value;
                        Brush.Surfaces = surfaces;
                    }
                }
            }

            private Vector3 _offset;

            /// <summary>
            /// Gets the brush actor.
            /// </summary>
            public WedgeBrush Brush => (WedgeBrush)((WedgeBrushNode)ParentNode).Actor;

            /// <summary>
            /// Gets the brush surface.
            /// </summary>
            public WedgeSurface Surface
            {
                get => Brush.Surfaces[Index];
                set
                {
                    var surfaces = Brush.Surfaces;
                    surfaces[Index] = value;
                    Brush.Surfaces = surfaces;
                }
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="SideLinkNode"/> class.
            /// </summary>
            /// <param name="actor">The parent node.</param>
            /// <param name="id">The identifier.</param>
            /// <param name="index">The index.</param>
            public SideLinkNode(WedgeBrushNode actor, Guid id, int index)
            : base(actor, id, index)
            {
                switch (index)
                {
                case 0:
                    _offset = new Vector3(0.5f, 0, 0);
                    break;
                case 1:
                    _offset = new Vector3(-0.5f, 0, 0);
                    break;
                case 2:
                    _offset = new Vector3(0, 0.5f, 0);
                    break;
                case 3:
                    _offset = new Vector3(0, -0.5f, 0);
                    break;
                case 4:
                    _offset = new Vector3(0, 0, 0.5f);
                    break;
                case 5:
                    _offset = new Vector3(0, 0, -0.5f);
                    break;
                }
            }

            /// <inheritdoc />
            public override Transform Transform
            {
                get
                {
                    var actor = Brush;
                    var localOffset = _offset * actor.Size + actor.Center;
                    Transform localTrans = new Transform(localOffset);
                    return actor.Transform.LocalToWorld(localTrans);
                }
                set
                {
                    var actor = Brush;
                    Transform localTrans = actor.Transform.WorldToLocal(value);
                    var prevLocalOffset = _offset * actor.Size + actor.Center;

                    // Mask to the face's own axis so an unrelated drift on the other axes
                    // (eg. a non-zero Center) never leaks into their Size/Center.
                    var axisMask = Vector3.Abs(_offset) * 2.0f;
                    var delta = axisMask * (localTrans.Translation - prevLocalOffset);

                    // On the min faces (-X/-Y/-Z, odd Index) the face sits on the negative
                    // side of Center, so growing the box means dragging the handle further
                    // negative - the opposite sign from the max faces. faceSign flips the
                    // Size delta to match so dragging outward always extends the box, while
                    // Center always moves by half the delta to keep the opposite face fixed.
                    var faceSign = _offset * 2.0f;
                    actor.Size += delta * faceSign;
                    actor.Center += delta * 0.5f;
                }
            }

            /// <inheritdoc />
            public override object EditableObject => new BrushSurfaceProxy
            {
                Brush = Brush,
                Index = Index,
            };

            /// <inheritdoc />
            public override bool RayCastSelf(ref RayCastData ray, out Real distance, out Vector3 normal)
            {
                return Brush.Intersects(Index, ref ray.Ray, out distance, out normal);
            }

            /// <inheritdoc />
            public override void OnDebugDraw(ViewportDebugDrawData data)
            {
                ParentNode.OnDebugDraw(data);
                data.HighlightBrushSurface(Brush.Surfaces[Index]);
            }
        }

        /// <summary>
        /// Sub actor node used to drag the sloped edge and adjust <see cref="WedgeBrush.Slope"/>.
        /// </summary>
        /// <seealso cref="FlaxEditor.SceneGraph.ActorChildNode{T}" />
        public sealed class SlopeLinkNode : ActorChildNode<WedgeBrushNode>
        {
            private sealed class WedgeSlopeProxy
            {
                [HideInEditor]
                public WedgeBrush Brush;

                [EditorOrder(10), EditorDisplay("Wedge"), Limit(-2, 2, 0.01f)]
                [Tooltip("How far the sloped face drops from full height, as a fraction of Size.Y. 0 keeps the top flat (a regular box), 1 collapses the front face entirely into a ramp.")]
                public float Slope
                {
                    get => Brush.Slope;
                    set => Brush.Slope = value;
                }
            }

            /// <summary>
            /// Gets the brush actor.
            /// </summary>
            public WedgeBrush Brush => (WedgeBrush)((WedgeBrushNode)ParentNode).Actor;

            /// <inheritdoc />
            public SlopeLinkNode(WedgeBrushNode actor, Guid id, int index)
            : base(actor, id, index)
            {
            }

            private static float GetFrontTopLocalY(WedgeBrush actor)
            {
                return actor.Center.Y + actor.Size.Y * 0.5f - actor.Slope * actor.Size.Y;
            }

            /// <inheritdoc />
            public override Transform Transform
            {
                get
                {
                    var actor = Brush;
                    var localOffset = new Vector3(actor.Center.X, GetFrontTopLocalY(actor), actor.Center.Z + actor.Size.Z * 0.5f);
                    return actor.Transform.LocalToWorld(new Transform(localOffset));
                }
                set
                {
                    var actor = Brush;
                    if (Mathf.IsZero(actor.Size.Y))
                        return;

                    var localTrans = actor.Transform.WorldToLocal(value);
                    var deltaY = localTrans.Translation.Y - GetFrontTopLocalY(actor);

                    // Moving the handle up means less drop (lower Slope); moving it down means more drop.
                    actor.Slope -= deltaY / actor.Size.Y;
                }
            }

            /// <inheritdoc />
            public override object EditableObject => new WedgeSlopeProxy
            {
                Brush = Brush,
            };

            /// <inheritdoc />
            public override bool RayCastSelf(ref RayCastData ray, out Real distance, out Vector3 normal)
            {
                var actor = Brush;
                var center = Transform.Translation;
                var radius = (Real)Mathf.Clamp(Math.Max(actor.Size.X, actor.Size.Z) * 0.05f, 1.0f, 25.0f);
                var sphere = new BoundingSphere(center, radius);
                if (CollisionsHelper.RayIntersectsSphere(ref ray.Ray, ref sphere, out distance))
                {
                    normal = -ray.Ray.Direction;
                    return true;
                }
                distance = 0;
                normal = Vector3.Up;
                return false;
            }

            /// <inheritdoc />
            public override void OnDebugDraw(ViewportDebugDrawData data)
            {
                ParentNode.OnDebugDraw(data);
                data.HighlightBrushSurface(Brush.Surfaces[2]);
            }
        }

        private readonly SlopeLinkNode _slopeNode;

        /// <inheritdoc />
        public WedgeBrushNode(Actor actor)
        : base(actor)
        {
            var id = ID;
            _slopeNode = new SlopeLinkNode(this, GetSubID(id, 6), 6);
            AddChildNode(_slopeNode);
            for (int i = 0; i < 6; i++)
                AddChildNode(new SideLinkNode(this, GetSubID(id, i), i));
        }

        /// <inheritdoc />
        public override bool RayCastSelf(ref RayCastData ray, out Real distance, out Vector3 normal)
        {
            if (((WedgeBrush)_actor).OrientedBox.Intersects(ref ray.Ray))
            {
                // The slope handle is a small target on top of the sloped face, so it needs to be
                // tested before the face that would otherwise swallow the same click.
                if (_slopeNode.RayCastSelf(ref ray, out distance, out normal))
                    return true;

                for (int i = 0; i < ChildNodes.Count; i++)
                {
                    if (ChildNodes[i] is SideLinkNode node && node.RayCastSelf(ref ray, out distance, out normal))
                        return true;
                }
            }

            distance = 0;
            normal = Vector3.Up;
            return false;
        }
    }
}
