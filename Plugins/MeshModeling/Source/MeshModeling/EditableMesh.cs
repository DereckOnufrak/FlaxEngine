// Copyright (c) Wojciech Figat. All rights reserved.

using System;
#if FLAX_EDITOR
using System.Threading.Tasks;
#endif
using FlaxEngine;

namespace MeshModeling
{
    /// <summary>
    /// An actor that renders geometry authored with the in-editor mesh modeling tools (see
    /// <see cref="EditableMeshData"/>). Renders through a child <see cref="StaticModel"/>, so baked levels behave
    /// like any other static mesh at runtime with no dependency on the editing tools.
    /// </summary>
    public class EditableMesh : Actor
    {
        private StaticModel _renderer;

        /// <summary>
        /// The editable topology asset this actor renders. Edited in the editor's viewport via the Modeling tool.
        /// </summary>
        public JsonAssetReference<EditableMeshData> Mesh;

        private StaticModel Renderer => _renderer != null ? _renderer : (_renderer = GetOrAddChild<StaticModel>());

        /// <summary>
        /// Re-triangulates <see cref="Mesh"/> and assigns the result to a temporary (virtual) model for immediate
        /// visual feedback while editing. Cheap, but the result cannot be saved into a scene/prefab as-is (virtual
        /// assets have no stable path) - call <see cref="Bake"/> once an edit gesture finishes.
        /// </summary>
        public void RebuildPreview()
        {
            var data = Mesh.Instance;
            if (data == null)
                return;

            EditableMeshBuilder.Triangulate(data, out var positions, out var triangles, out var normals, out var uv);
            var model = Content.CreateVirtualAsset<Model>();
            model.SetupLODs(new[] { 1 });
            model.LODs[0].Meshes[0].UpdateMesh(positions, triangles, normals, null, uv);
            Renderer.Model = model;
        }

#if FLAX_EDITOR
        /// <summary>
        /// Re-triangulates <see cref="Mesh"/> and persists the result as a real <see cref="Model"/> asset on disk,
        /// then points the renderer at that saved asset. Unlike <see cref="RebuildPreview"/>, this gives a stable
        /// reference that survives saving/reloading the scene. Intended to be called once per edit gesture (e.g. on
        /// mouse-up), not every frame of a drag.
        /// </summary>
        /// <param name="path">
        /// The output asset path. If null, defaults to next to the source <see cref="Mesh"/> asset.
        /// </param>
        public void Bake(string path = null)
        {
            var data = Mesh.Instance;
            if (data == null)
                return;
            if (string.IsNullOrEmpty(path))
            {
                if (Mesh.Asset == null)
                    throw new ArgumentException("A path is required to bake a mesh with no source asset.");
                path = System.IO.Path.ChangeExtension(Mesh.Asset.Path, null) + "Model.flax";
            }

            EditableMeshBuilder.Triangulate(data, out var positions, out var triangles, out var normals, out var uv);
            var model = Content.CreateVirtualAsset<Model>();
            model.SetupLODs(new[] { 1 });
            model.LODs[0].Meshes[0].UpdateMesh(positions, triangles, normals, null, uv);

            // Saving a virtual model reads its mesh data back from the GPU, so it must run off the main thread
            // (same reasoning as MeshDataCache's mesh data downloads).
            bool failed = true;
            Task.Run(() => failed = model.Save(true, path)).Wait();
            FlaxEngine.Object.Destroy(model);

            if (failed)
            {
                Debug.LogError($"Failed to bake EditableMesh to '{path}'.");
                return;
            }
            Renderer.Model = Content.Load<Model>(path);
        }
#endif
    }
}
