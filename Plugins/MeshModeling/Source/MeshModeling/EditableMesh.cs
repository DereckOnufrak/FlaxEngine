// Copyright (c) Wojciech Figat. All rights reserved.

using FlaxEngine;

namespace MeshModeling
{
    /// <summary>
    /// An actor that renders geometry authored with the in-editor mesh modeling tools (see
    /// <see cref="EditableMeshData"/>). Renders through a child <see cref="StaticModel"/> using a virtual
    /// (in-memory) <see cref="Model"/> rebuilt from <see cref="Mesh"/> on demand and whenever the actor is enabled -
    /// so the persisted <see cref="EditableMeshData"/> JSON asset is the only thing that needs to survive a
    /// scene save/reload, and there's no separate baked Model asset (and its GPU-readback/caching complexity) to
    /// keep in sync.
    /// </summary>
    public class EditableMesh : Actor
    {
        private StaticModel _renderer;

        /// <summary>
        /// The editable topology asset this actor renders. Edited in the editor's viewport via the Modeling tool.
        /// </summary>
        public JsonAssetReference<EditableMeshData> Mesh;

        private StaticModel Renderer => _renderer != null ? _renderer : (_renderer = GetOrAddChild<StaticModel>());

        /// <inheritdoc />
        public override void OnEnable()
        {
            base.OnEnable();
            Rebuild();
        }

        /// <summary>
        /// Re-triangulates <see cref="Mesh"/> and assigns the result to the renderer via a fresh virtual
        /// (in-memory) <see cref="Model"/>. Call this after any edit to <see cref="Mesh"/>'s instance data; it also
        /// runs automatically whenever the actor is enabled (e.g. on scene load).
        /// </summary>
        public void Rebuild()
        {
            var data = Mesh.Instance;
            if (data == null)
                return;

            EditableMeshBuilder.Triangulate(data, out var positions, out var triangles, out var normals, out var uv);
            var model = Content.CreateVirtualAsset<Model>();
            model.SetupLODs(new[] { 1 });
            model.LODs[0].Meshes[0].UpdateMesh(positions, triangles, normals, null, uv);

            var old = Renderer.Model;
            Renderer.Model = model;
            if (old != null && old.IsVirtual)
                Object.Destroy(old);
        }

        /// <summary>
        /// Moves the mesh's local origin to the given local-space point, without moving the mesh visually in world
        /// space - compensates by shifting the actor's own position to match. E.g. pass
        /// <see cref="EditableMeshBuilder.ComputeBoundsCenter"/>'s result to center the origin on the mesh's
        /// bounding box, or an arbitrary point to place the origin anywhere relative to the geometry.
        /// </summary>
        /// <param name="localPoint">The local-space point (relative to the current origin) to move the origin to.</param>
        public void SetOrigin(Vector3 localPoint)
        {
            var data = Mesh.Instance;
            if (data == null || localPoint.LengthSquared < 1e-12f)
                return;

            var newPosition = Transform.LocalToWorld(localPoint);
            EditableMeshBuilder.OffsetAllVertices(data, -localPoint);
            Position = newPosition;
            Rebuild();
        }
    }
}
