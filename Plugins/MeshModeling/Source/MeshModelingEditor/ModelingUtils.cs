// Copyright (c) Wojciech Figat. All rights reserved.

using System.Collections.Generic;
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

        /// <summary>
        /// Overwrites <paramref name="to"/>'s fields with copies of <paramref name="from"/>'s, in place (rather
        /// than reassigning the reference), so anything already holding onto <paramref name="to"/> keeps seeing
        /// consistent data. Used by undo actions to restore a before/after <see cref="EditableMeshData"/> snapshot
        /// onto the live instance.
        /// </summary>
        public static void CopyInto(EditableMeshData from, EditableMeshData to)
        {
            to.Positions = new List<Float3>(from.Positions);
            to.HalfEdges = new List<EditableMeshData.HalfEdge>(from.HalfEdges);
            to.Faces = new List<EditableMeshData.Face>(from.Faces);
            to.MaterialSlots = new List<EditableMeshData.MaterialSlot>(from.MaterialSlots);
        }
    }
}
