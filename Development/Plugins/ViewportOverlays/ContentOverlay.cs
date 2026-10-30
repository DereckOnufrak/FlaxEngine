using System;
using System.Collections.Generic;
using FlaxEditor;
using FlaxEditor.Content;
using FlaxEditor.GUI.Drag;
using FlaxEditor.GUI.Input;
using FlaxEngine;
using FlaxEngine.GUI;

namespace ViewportOverlays
{
    /// <summary>
    /// Overlay with a searchable list of the project assets. Double-click opens the asset editor, dragging into the scene viewport spawns or assigns it.
    /// </summary>
    public sealed class ContentOverlay : OverlayPanel
    {
        /// <summary>
        /// The maximum amount of listed results (keeps the list fast for big projects).
        /// </summary>
        public static int MaxResults = 200;

        private readonly SearchBox _searchBox;
        private readonly Panel _scrollPanel;
        private readonly VerticalPanel _list;
        private readonly Label _status;
        private readonly List<ContentItem> _assets = new List<ContentItem>();
        private OverlayListItem _selected;
        private bool _isBound;
        private bool _isAssetsDirty = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="ContentOverlay"/> class.
        /// </summary>
        public ContentOverlay()
        : base("Content")
        {
            _searchBox = new SearchBox
            {
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = new Margin(4, 4, 4, 18),
                Parent = Body,
            };
            _searchBox.TextChanged += RefreshList;

            _scrollPanel = new Panel(ScrollBars.Vertical)
            {
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = new Margin(0, 0, 26, 18),
                Parent = Body,
            };
            _list = new VerticalPanel
            {
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = Margin.Zero,
                IsScrollable = true,
                Spacing = 0,
                Pivot = Float2.Zero,
                Parent = _scrollPanel,
            };

            _status = new Label
            {
                AnchorPreset = AnchorPresets.HorizontalStretchBottom,
                Offsets = new Margin(4, 4, -18, 18),
                HorizontalAlignment = TextAlignment.Near,
                Parent = Body,
            };
        }

        /// <inheritdoc />
        public override void OnShown()
        {
            base.OnShown();

            if (!_isBound)
            {
                _isBound = true;
                var db = Editor.Instance.ContentDatabase;
                db.ItemAdded += OnItemChanged;
                db.ItemRemoved += OnItemChanged;
                db.WorkspaceModified += OnWorkspaceModified;
            }
            RefreshList();
        }

        /// <inheritdoc />
        public override void OnHidden()
        {
            Unbind();

            base.OnHidden();
        }

        /// <inheritdoc />
        public override void FocusContent()
        {
            _searchBox.Focus();
            _searchBox.SelectAll();
        }

        /// <inheritdoc />
        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            // Pick up imported/removed assets while open
            if (_isAssetsDirty && _isBound)
                RefreshList();
        }

        /// <summary>
        /// Rebuilds the results list using the current search text.
        /// </summary>
        public void RefreshList()
        {
            if (_isAssetsDirty)
                CollectAssets();

            var filter = _searchBox.Text;
            var isFiltering = !string.IsNullOrWhiteSpace(filter);
            _selected = null;
            _list.IsLayoutLocked = true;
            _list.DisposeChildren();
            int count = 0, matches = 0;
            foreach (var item in _assets)
            {
                if (isFiltering && item.ShortName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) == -1)
                    continue;
                matches++;
                if (count >= MaxResults)
                    continue;
                count++;
                AddRow(item);
            }
            _list.IsLayoutLocked = false;
            _list.PerformLayout();
            _status.Text = matches > count ? $"{count} of {matches} assets (refine the search)" : $"{matches} assets";
        }

        private void AddRow(ContentItem item)
        {
            var row = new OverlayListItem(item.ShortName)
            {
                Tag = item,
                TooltipText = item.Path,
                DrawIcon = rect => item.DrawThumbnail(ref rect),
                StartDrag = _ => DragItems.GetDragData(item),
                Parent = _list,
            };
            row.Clicked += OnRowClicked;
            row.DoubleClicked += OnRowDoubleClicked;
        }

        private void OnRowClicked(OverlayListItem row)
        {
            if (_selected != null)
                _selected.IsSelected = false;
            _selected = row;
            row.IsSelected = true;
        }

        private void OnRowDoubleClicked(OverlayListItem row)
        {
            if (row.Tag is ContentItem item)
                Editor.Instance.ContentEditing.Open(item);
        }

        private void CollectAssets()
        {
            _isAssetsDirty = false;
            _assets.Clear();
            var root = Editor.Instance.ContentDatabase.Game?.Content?.Folder;
            if (root != null)
                Collect(root);
            _assets.Sort((a, b) => string.Compare(a.ShortName, b.ShortName, StringComparison.OrdinalIgnoreCase));
        }

        private void Collect(ContentFolder folder)
        {
            foreach (var child in folder.Children)
            {
                if (child is ContentFolder subFolder)
                    Collect(subFolder);
                else if (child.IsAsset)
                    _assets.Add(child);
            }
        }

        private void OnItemChanged(ContentItem item)
        {
            _isAssetsDirty = true;
        }

        private void OnWorkspaceModified()
        {
            _isAssetsDirty = true;
        }

        private void Unbind()
        {
            if (!_isBound)
                return;
            _isBound = false;
            var db = Editor.Instance.ContentDatabase;
            db.ItemAdded -= OnItemChanged;
            db.ItemRemoved -= OnItemChanged;
            db.WorkspaceModified -= OnWorkspaceModified;
        }

        /// <inheritdoc />
        public override void OnDestroy()
        {
            Unbind();

            base.OnDestroy();
        }
    }
}
