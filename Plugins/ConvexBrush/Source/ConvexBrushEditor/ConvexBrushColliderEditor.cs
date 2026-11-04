using FlaxEditor;
using FlaxEditor.CustomEditors;
using FlaxEditor.CustomEditors.Dedicated;
using FlaxEditor.CustomEditors.Elements;
using FlaxEngine;

namespace ConvexBrush
{
    /// <summary>
    /// Custom inspector for <see cref="ConvexBrushCollider"/> with brush editing tools.
    /// </summary>
    [CustomEditor(typeof(ConvexBrushCollider))]
    public class ConvexBrushColliderEditor : ActorEditor
    {
        private LabelElement _status;

        /// <inheritdoc />
        public override void Initialize(LayoutElementsContainer layout)
        {
            base.Initialize(layout);

            var group = layout.Group("Brush Tools");
            _status = group.Label(string.Empty);
            group.Button("Add Face Facing Camera", "Adds a new face oriented towards the editor camera, touching the current hull. Select it in the viewport and move it inwards to cut the brush.").Button.Clicked += OnAddFaceFacingCamera;
            group.Button("Reset To Box", "Resets the brush faces into a box.").Button.Clicked += OnResetToBox;
            group.Label("Tip: click the brush, then click a face to select it.\nMove = push/pull face, Rotate = tilt face.\nRight-click a face to duplicate (bevel) or remove it.", TextAlignment.Near);
            group.Space(0);
            UpdateStatus();
        }

        /// <inheritdoc />
        public override void Refresh()
        {
            base.Refresh();

            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (_status == null)
                return;
            if (Values.Count != 1 || !(Values[0] is ConvexBrushCollider brush) || !brush)
            {
                _status.Label.Text = string.Empty;
                return;
            }
            var geometry = brush.Geometry;
            _status.Label.Text = geometry.IsValid
                                 ? $"Faces: {brush.FacesCount}, Vertices: {geometry.Vertices.Length}"
                                 : "Invalid brush: faces don't form a closed convex volume";
        }

        private void OnAddFaceFacingCamera()
        {
            var viewport = Editor.Instance.Windows.EditWin?.Viewport;
            if (viewport == null)
                return;
            var viewDir = viewport.ViewDirection;
            Edit("Add brush face", brush =>
            {
                var localNormal = brush.Transform.WorldToLocalVector(-viewDir);
                if (localNormal.LengthSquared > 1e-12f)
                    brush.AddFace(localNormal);
            });
        }

        private void OnResetToBox()
        {
            Edit("Reset brush to box", brush => brush.ResetToBox(ConvexBrushCollider.DefaultSize));
        }

        private void Edit(string actionName, System.Action<ConvexBrushCollider> edit)
        {
            foreach (var value in Values)
            {
                if (value is ConvexBrushCollider brush && brush)
                {
                    using (new UndoBlock(Presenter.Undo, brush, actionName))
                        edit(brush);
                    Editor.Instance.Scene.MarkSceneEdited(brush.Scene);
                }
            }
            UpdateStatus();
        }
    }
}
