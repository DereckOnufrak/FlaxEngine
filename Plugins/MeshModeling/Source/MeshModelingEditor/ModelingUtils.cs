// Copyright (c) Wojciech Figat. All rights reserved.

using FlaxEditor;
using FlaxEngine;
using MeshModeling;

namespace MeshModelingEditor
{
    /// <summary>
    /// Shared helpers for committing an edit made to an <see cref="EditableMesh"/> through the modeling tool.
    /// </summary>
    static class ModelingUtils
    {
        /// <summary>
        /// Rebuilds the render mesh, persists the source <see cref="EditableMeshData"/> asset, and marks the owning
        /// scene as edited. Call this once after any change to <see cref="EditableMesh.Mesh"/>'s instance data.
        /// </summary>
        public static void CommitEdit(EditableMesh mesh)
        {
            if (mesh == null)
                return;
            mesh.Rebuild();
            var asset = mesh.Mesh.Asset;
            if (asset != null)
                Editor.SaveJsonAsset(asset.Path, mesh.Mesh.Instance);
            if (mesh.Scene != null)
                Editor.Instance.Scene.MarkSceneEdited(mesh.Scene);
        }
    }
}
