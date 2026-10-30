using System.Collections.Generic;
using System.Linq;
using FlaxEditor;
using FlaxEditor.CustomEditors;
using FlaxEditor.GUI.Input;
using FlaxEditor.SceneGraph;
using FlaxEditor.Viewport;
using FlaxEngine;
using FlaxEngine.GUI;

namespace ViewportOverlays
{
    /// <summary>
    /// Overlay with the properties of the selected scene objects. Uses the same <see cref="CustomEditorPresenter"/> as the Properties window.
    /// </summary>
    public sealed class PropertiesOverlay : OverlayPanel, IPresenterOwner
    {
        private readonly SearchBox _searchBox;
        private IEnumerable<object> _undoObjects;
        private bool _lockSelection;
        private bool _isBound;

        /// <summary>
        /// The properties presenter.
        /// </summary>
        public readonly CustomEditorPresenter Presenter;

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertiesOverlay"/> class.
        /// </summary>
        public PropertiesOverlay()
        : base("Properties")
        {
            var editor = Editor.Instance;

            _searchBox = new SearchBox
            {
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = new Margin(4, 4, 4, 18),
                Parent = Body,
            };
            _searchBox.TextChanged += ApplySearchFilter;

            var scrollPanel = new Panel(ScrollBars.Vertical)
            {
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = new Margin(0, 0, 26, 0),
                Parent = Body,
            };

            Presenter = new CustomEditorPresenter(editor.Undo, "No selection", this);
            Presenter.Panel.Parent = scrollPanel;
            Presenter.GetUndoObjects = presenter => _undoObjects;
            Presenter.Features |= FeatureFlags.CacheExpandedGroups;
            Presenter.AfterLayout += layout => ApplySearchFilter();
        }

        /// <inheritdoc />
        public override void OnShown()
        {
            base.OnShown();

            // Track selection only while visible to avoid rebuilding a hidden layout on every selection change
            if (!_isBound)
            {
                _isBound = true;
                Editor.Instance.SceneEditing.SelectionChanged += OnSelectionChanged;
            }
            OnSelectionChanged();
        }

        /// <inheritdoc />
        public override void OnHidden()
        {
            // Hidden controls still get updated, so don't keep editing objects that can get deleted meanwhile
            Unbind();
            Presenter.Deselect();

            base.OnHidden();
        }

        /// <inheritdoc />
        public override void FocusContent()
        {
            _searchBox.Focus();
        }

        private void Unbind()
        {
            if (_isBound)
            {
                _isBound = false;
                Editor.Instance.SceneEditing.SelectionChanged -= OnSelectionChanged;
            }
        }

        private void OnSelectionChanged()
        {
            if (_lockSelection)
                return;
            var selection = Editor.Instance.SceneEditing.Selection;
            _undoObjects = selection.ConvertAll(x => x.UndoRecordObject).Distinct();
            Presenter.Select(selection.ConvertAll(x => x.EditableObject).Distinct());
        }

        private void ApplySearchFilter()
        {
            Presenter.ApplySearchFilter(_searchBox.Text);
        }

        /// <inheritdoc />
        public EditorViewport PresenterViewport => Editor.Instance.Windows.EditWin.Viewport;

        /// <inheritdoc />
        public ISceneEditingContext SceneContext => Editor.Instance.Windows.EditWin;

        /// <inheritdoc />
        public List<SceneGraphNode> Selection => Editor.Instance.SceneEditing.Selection;

        /// <inheritdoc />
        public bool LockSelection
        {
            get => _lockSelection;
            set
            {
                if (value == _lockSelection)
                    return;
                _lockSelection = value;
                if (!value)
                    OnSelectionChanged();
            }
        }

        /// <inheritdoc />
        public void Select(List<SceneGraphNode> nodes)
        {
            Editor.Instance.SceneEditing.Select(nodes);
        }

        /// <inheritdoc />
        public override void OnDestroy()
        {
            Unbind();
            Presenter.Deselect();

            base.OnDestroy();
        }
    }
}
