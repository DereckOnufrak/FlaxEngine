// Copyright (c) Wojciech Figat. All rights reserved.

using System;
using System.IO;
using FlaxEditor;
using FlaxEditor.GUI.Tabs;
using FlaxEditor.SceneGraph;
using FlaxEngine;
using FlaxEngine.GUI;
using MeshModeling;

namespace MeshModelingEditor
{
    /// <summary>
    /// Toolbox tab for the in-editor mesh modeling tool: create primitives, choose what kind of element to select
    /// (vertex/edge/face), and run whole-face operations (extrude, delete).
    /// </summary>
    /// <seealso cref="Tab" />
    [HideInEditor]
    public class ModelingTab : Tab
    {
        private const float ExtrudeDistance = 50.0f;

        private readonly Editor _editor;
        private readonly ModelingGizmoMode _gizmo;
        private readonly Label _infoLabel;
        private readonly ComboBox _selectionTypeComboBox;
        private readonly Button _extrudeButton;
        private readonly Button _deleteFaceButton;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModelingTab"/> class.
        /// </summary>
        /// <param name="icon">The icon.</param>
        /// <param name="editor">The editor instance.</param>
        /// <param name="gizmo">The modeling gizmo mode.</param>
        public ModelingTab(SpriteHandle icon, Editor editor, ModelingGizmoMode gizmo)
        : base(string.Empty, icon)
        {
            _editor = editor;
            _gizmo = gizmo;
            _gizmo.SelectionChanged += UpdateUI;
            _gizmo.SelectionTypeChanged += UpdateUI;
            _editor.SceneEditing.SelectionChanged += OnSceneSelectionChanged;

            var panel = new Panel(ScrollBars.Vertical)
            {
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = Margin.Zero,
                Parent = this,
            };

            var createCubeButton = new Button(4, 4)
            {
                Text = "Create Cube",
                Parent = panel,
            };
            createCubeButton.Clicked += () => CreatePrimitive(EditableMeshBuilder.CreateCube());

            var createPlaneButton = new Button(createCubeButton.X, createCubeButton.Bottom + 4)
            {
                Text = "Create Plane",
                Parent = panel,
            };
            createPlaneButton.Clicked += () => CreatePrimitive(EditableMeshBuilder.CreatePlane());

            var selectionTypeLabel = new Label(createCubeButton.X, createPlaneButton.Bottom + 8, 60, 18)
            {
                HorizontalAlignment = TextAlignment.Near,
                Text = "Select:",
                Parent = panel,
            };
            _selectionTypeComboBox = new ComboBox(selectionTypeLabel.Right + 4, selectionTypeLabel.Y, 100)
            {
                Parent = panel,
            };
            _selectionTypeComboBox.AddItem("Vertex");
            _selectionTypeComboBox.AddItem("Edge");
            _selectionTypeComboBox.AddItem("Face");
            _selectionTypeComboBox.SelectedIndex = (int)_gizmo.SelectionType;
            _selectionTypeComboBox.SelectedIndexChanged += combo => _gizmo.SelectionType = (ModelingGizmoMode.ElementType)combo.SelectedIndex;

            _extrudeButton = new Button(createCubeButton.X, selectionTypeLabel.Bottom + 8)
            {
                Text = "Extrude Face",
                Parent = panel,
            };
            _extrudeButton.Clicked += OnExtrudeClicked;

            _deleteFaceButton = new Button(createCubeButton.X, _extrudeButton.Bottom + 4)
            {
                Text = "Delete Face",
                Parent = panel,
            };
            _deleteFaceButton.Clicked += OnDeleteFaceClicked;

            _infoLabel = new Label(createCubeButton.X, _deleteFaceButton.Bottom + 8, 260, 40)
            {
                HorizontalAlignment = TextAlignment.Near,
                VerticalAlignment = TextAlignment.Near,
                Parent = panel,
            };

            UpdateUI();
        }

        /// <inheritdoc />
        public override void OnSelected()
        {
            base.OnSelected();
            _editor.Windows.EditWin.Viewport.Gizmos.SetActiveMode<ModelingGizmoMode>();
        }

        private void OnSceneSelectionChanged()
        {
            EditableMesh mesh = null;
            if (_editor.SceneEditing.SelectionCount == 1 && _editor.SceneEditing.Selection[0] is ActorNode actorNode)
                mesh = actorNode.Actor as EditableMesh;
            _gizmo.SetSelectedMesh(mesh);
            UpdateUI();
        }

        private void CreatePrimitive(EditableMeshData data)
        {
            if (!Level.IsAnySceneLoaded)
                return;

            var folder = Path.Combine(Globals.ProjectContentFolder, "Meshes");
            Directory.CreateDirectory(folder);
            var dataPath = Path.Combine(folder, $"EditableMesh_{Guid.NewGuid():N}.json");
            if (Editor.SaveJsonAsset(dataPath, data))
            {
                Editor.LogError($"Failed to save editable mesh data to '{dataPath}'.");
                return;
            }
            var asset = FlaxEngine.Content.Load<JsonAsset>(dataPath);
            if (asset == null)
            {
                Editor.LogError($"Failed to load newly saved editable mesh data at '{dataPath}'.");
                return;
            }

            var actor = new EditableMesh
            {
                Name = "Editable Mesh",
                Mesh = new JsonAssetReference<EditableMeshData>(asset),
            };
            _editor.SceneEditing.Spawn(actor);
            ModelingUtils.CommitEdit(actor);
        }

        private void OnExtrudeClicked()
        {
            var mesh = _gizmo.SelectedMesh;
            var data = mesh ? mesh.Mesh.Instance : null;
            if (data == null || _gizmo.SelectionType != ModelingGizmoMode.ElementType.Face || _gizmo.SelectedFaceIndex < 0)
                return;

            var before = data.Clone();
            int newFace = EditableMeshBuilder.ExtrudeFace(data, _gizmo.SelectedFaceIndex, ExtrudeDistance);
            ModelingUtils.CommitEdit(mesh);
            if (newFace >= 0)
                _gizmo.SelectFace(newFace, data.GetFaceLoop(newFace));
            else
                _gizmo.ClearSelection();
            Editor.Instance.Undo.AddAction(new EditGeometryAction(mesh, before, data.Clone()));
        }

        private void OnDeleteFaceClicked()
        {
            var mesh = _gizmo.SelectedMesh;
            var data = mesh ? mesh.Mesh.Instance : null;
            if (data == null || _gizmo.SelectionType != ModelingGizmoMode.ElementType.Face || _gizmo.SelectedFaceIndex < 0)
                return;

            var before = data.Clone();
            EditableMeshBuilder.DeleteFace(data, _gizmo.SelectedFaceIndex);
            ModelingUtils.CommitEdit(mesh);
            _gizmo.ClearSelection();
            Editor.Instance.Undo.AddAction(new EditGeometryAction(mesh, before, data.Clone()));
        }

        private void UpdateUI()
        {
            var mesh = _gizmo.SelectedMesh;
            bool hasFaceSelected = mesh && _gizmo.SelectionType == ModelingGizmoMode.ElementType.Face && _gizmo.SelectedFaceIndex >= 0;
            _extrudeButton.Enabled = hasFaceSelected;
            _deleteFaceButton.Enabled = hasFaceSelected;

            if (!mesh)
                _infoLabel.Text = "Select an Editable Mesh actor to edit it,\nor create a new one above.";
            else if (_gizmo.SelectionType == ModelingGizmoMode.ElementType.Face)
                _infoLabel.Text = hasFaceSelected ? $"Editing '{mesh.Name}'\nFace #{_gizmo.SelectedFaceIndex} selected" : $"Editing '{mesh.Name}'\nClick a face to select it";
            else
                _infoLabel.Text = $"Editing '{mesh.Name}'\n{_gizmo.SelectedVertexIndices.Count} vertex/vertices selected";
        }
    }
}
