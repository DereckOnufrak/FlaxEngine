// Copyright (c) Wojciech Figat. All rights reserved.

using System;
using FlaxEditor;
using FlaxEngine;
using MeshModeling;

namespace MeshModelingEditor
{
    /// <summary>
    /// Undo action for <see cref="EditableMesh.SetOrigin"/>: unlike a plain geometry edit, moving the origin also
    /// changes the actor's own position (to keep the mesh visually in place), so both need a before/after snapshot.
    /// </summary>
    /// <seealso cref="FlaxEditor.IUndoAction" />
    [Serializable]
    sealed class SetOriginAction : IUndoAction
    {
        [Serialize]
        private readonly Guid _meshActorId;

        [Serialize]
        private EditableMeshData _before;

        [Serialize]
        private EditableMeshData _after;

        [Serialize]
        private Vector3 _positionBefore;

        [Serialize]
        private Vector3 _positionAfter;

        /// <summary>
        /// Initializes a new instance of the <see cref="SetOriginAction"/> class.
        /// </summary>
        public SetOriginAction(EditableMesh mesh, EditableMeshData before, EditableMeshData after, Vector3 positionBefore, Vector3 positionAfter)
        {
            _meshActorId = mesh.ID;
            _before = before;
            _after = after;
            _positionBefore = positionBefore;
            _positionAfter = positionAfter;
        }

        /// <inheritdoc />
        public string ActionString => "Set mesh origin";

        /// <inheritdoc />
        public void Do()
        {
            Apply(_after, _positionAfter);
        }

        /// <inheritdoc />
        public void Undo()
        {
            Apply(_before, _positionBefore);
        }

        private void Apply(EditableMeshData snapshot, Vector3 position)
        {
            var meshActorId = _meshActorId;
            var mesh = FlaxEngine.Object.Find<EditableMesh>(ref meshActorId);
            if (mesh == null)
                return;
            var target = mesh.Mesh.Instance;
            if (target == null)
                return;

            ModelingUtils.CopyInto(snapshot, target);
            mesh.Position = position;
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
