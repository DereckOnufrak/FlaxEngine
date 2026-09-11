// Copyright (c) Wojciech Figat. All rights reserved.

using FlaxEngine;

namespace FlaxEditor.Tools.Modeling
{
    /// <summary>
    /// Shared helpers for committing an edit made to an <see cref="EditableMesh"/> through the modeling tool.
    /// </summary>
    static class ModelingUtils
    {
        /// <summary>
        /// Refreshes the live preview, bakes the render <see cref="Model"/>, persists the source
        /// <see cref="EditableMeshData"/> asset, and marks the owning scene as edited. Call this once after any
        /// change to <see cref="EditableMesh.Mesh"/>'s instance data.
        /// </summary>
        public static void CommitEdit(EditableMesh mesh)
        {
            if (mesh == null)
                return;
            mesh.RebuildPreview();
            mesh.Bake();
            var asset = mesh.Mesh.Asset;
            if (asset != null)
                Editor.SaveJsonAsset(asset.Path, mesh.Mesh.Instance);
            if (mesh.Scene != null)
                Editor.Instance.Scene.MarkSceneEdited(mesh.Scene);
        }
    }
}
