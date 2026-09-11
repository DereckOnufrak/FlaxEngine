// Copyright (c) Wojciech Figat. All rights reserved.

using System;
using System.Collections.Generic;
using FlaxEditor.Gizmo;
using FlaxEditor.Viewport.Modes;
using FlaxEngine;

namespace FlaxEditor.Tools.Modeling
{
    /// <summary>
    /// In-editor mesh modeling tool: lets the user select and edit an <see cref="EditableMesh"/> actor's geometry
    /// (vertices, edges or faces) directly in the viewport.
    /// </summary>
    /// <seealso cref="FlaxEditor.Viewport.Modes.EditorGizmoMode" />
    [HideInEditor]
    public class ModelingGizmoMode : EditorGizmoMode
    {
        /// <summary>
        /// The kind of mesh element the tool currently selects/edits.
        /// </summary>
        public enum ElementType
        {
            /// <summary>
            /// Individual vertices.
            /// </summary>
            Vertex,

            /// <summary>
            /// Edges (pairs of adjacent vertices).
            /// </summary>
            Edge,

            /// <summary>
            /// Whole faces.
            /// </summary>
            Face,
        }

        private ElementType _elementType = ElementType.Face;

        /// <summary>
        /// The modeling gizmo.
        /// </summary>
        public ModelingGizmo Gizmo;

        /// <summary>
        /// The <see cref="EditableMesh"/> actor currently being edited, or null if none.
        /// </summary>
        public EditableMesh SelectedMesh { get; private set; }

        /// <summary>
        /// The currently selected face index (only meaningful when <see cref="SelectionType"/> is <see cref="ElementType.Face"/>), or -1 if none.
        /// </summary>
        public int SelectedFaceIndex { get; private set; } = -1;

        /// <summary>
        /// The <see cref="EditableMeshData.Positions"/> indices of the currently selected element (1 for a vertex,
        /// 2 for an edge, or the whole face loop for a face).
        /// </summary>
        public List<int> SelectedVertexIndices { get; } = new List<int>();

        /// <summary>
        /// Gets or sets the kind of element the tool selects/edits. Changing this clears the current selection.
        /// </summary>
        public ElementType SelectionType
        {
            get => _elementType;
            set
            {
                if (_elementType == value)
                    return;
                _elementType = value;
                ClearSelection();
                SelectionTypeChanged?.Invoke();
            }
        }

        /// <summary>
        /// Occurs when <see cref="SelectionType"/> changes.
        /// </summary>
        public event Action SelectionTypeChanged;

        /// <summary>
        /// Occurs when the selected element (or the edited mesh) changes.
        /// </summary>
        public event Action SelectionChanged;

        /// <inheritdoc />
        public override void Init(IGizmoOwner owner)
        {
            base.Init(owner);
            Gizmo = new ModelingGizmo(owner, this);
        }

        /// <inheritdoc />
        public override void OnActivated()
        {
            base.OnActivated();
            Owner.Gizmos.Active = Gizmo;
        }

        /// <summary>
        /// Sets the actor being edited. Clears the current selection if it changed.
        /// </summary>
        public void SetSelectedMesh(EditableMesh mesh)
        {
            if (SelectedMesh == mesh)
                return;
            SelectedMesh = mesh;
            ClearSelection();
        }

        /// <summary>
        /// Selects a whole face (used by <see cref="ElementType.Face"/> mode).
        /// </summary>
        public void SelectFace(int faceIndex, List<int> loop)
        {
            SelectedFaceIndex = faceIndex;
            SelectedVertexIndices.Clear();
            SelectedVertexIndices.AddRange(loop);
            SelectionChanged?.Invoke();
        }

        /// <summary>
        /// Selects a specific set of data vertices (used by <see cref="ElementType.Vertex"/> and <see cref="ElementType.Edge"/> modes).
        /// </summary>
        public void SelectVertices(IEnumerable<int> vertexIndices)
        {
            SelectedFaceIndex = -1;
            SelectedVertexIndices.Clear();
            SelectedVertexIndices.AddRange(vertexIndices);
            SelectionChanged?.Invoke();
        }

        /// <summary>
        /// Clears the current element selection.
        /// </summary>
        public void ClearSelection()
        {
            if (SelectedFaceIndex == -1 && SelectedVertexIndices.Count == 0)
                return;
            SelectedFaceIndex = -1;
            SelectedVertexIndices.Clear();
            SelectionChanged?.Invoke();
        }
    }
}
