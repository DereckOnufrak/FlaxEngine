// Copyright (c) Wojciech Figat. All rights reserved.

using System;
using System.Collections.Generic;
using FlaxEngine;

namespace FlaxEditor.Tools.Modeling.Undo
{
    /// <summary>
    /// Undo action for a single mesh-editing gesture (drag, extrude, delete face, ...) performed on an
    /// <see cref="EditableMesh"/>. Stores full before/after snapshots of the edited <see cref="EditableMeshData"/> -
    /// meshes edited with this tool are small enough that a full snapshot is simpler and safer than diffing.
    /// </summary>
    /// <seealso cref="FlaxEditor.IUndoAction" />
    [Serializable]
    sealed class EditGeometryAction : IUndoAction
    {
        [Serialize]
        private readonly Guid _meshActorId;

        [Serialize]
        private EditableMeshData _before;

        [Serialize]
        private EditableMeshData _after;

        /// <summary>
        /// Initializes a new instance of the <see cref="EditGeometryAction"/> class.
        /// </summary>
        /// <param name="mesh">The edited actor.</param>
        /// <param name="before">A snapshot of the mesh data before the edit.</param>
        /// <param name="after">A snapshot of the mesh data after the edit.</param>
        public EditGeometryAction(EditableMesh mesh, EditableMeshData before, EditableMeshData after)
        {
            _meshActorId = mesh.ID;
            _before = before;
            _after = after;
        }

        /// <inheritdoc />
        public string ActionString => "Edit mesh geometry";

        /// <inheritdoc />
        public void Do()
        {
            Apply(_after);
        }

        /// <inheritdoc />
        public void Undo()
        {
            Apply(_before);
        }

        private void Apply(EditableMeshData snapshot)
        {
            var meshActorId = _meshActorId;
            var mesh = FlaxEngine.Object.Find<EditableMesh>(ref meshActorId);
            if (mesh == null)
                return;
            var target = mesh.Mesh.Instance;
            if (target == null)
                return;

            // Overwrite the live JsonAsset instance in place (rather than reassigning the reference) so anything
            // else already holding onto `target` keeps seeing consistent data.
            target.Positions = new List<Float3>(snapshot.Positions);
            target.HalfEdges = new List<EditableMeshData.HalfEdge>(snapshot.HalfEdges);
            target.Faces = new List<EditableMeshData.Face>(snapshot.Faces);
            target.MaterialSlots = new List<EditableMeshData.MaterialSlot>(snapshot.MaterialSlots);

            ModelingUtils.CommitEdit(mesh);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _before = null;
            _after = null;
        }
    }
}
