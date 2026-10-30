using System;
using System.Collections.Generic;
using FlaxEditor;
using FlaxEditor.GUI.Input;
using FlaxEditor.GUI.Tree;
using FlaxEditor.SceneGraph;
using FlaxEngine;
using FlaxEngine.GUI;

namespace ViewportOverlays
{
    /// <summary>
    /// Overlay with a searchable actors hierarchy of the loaded scenes. Selection is synchronized with the editor selection.
    /// </summary>
    /// <remarks>
    /// The Scene window tree nodes belong to the scene graph and can have only one parent, so this overlay builds its own lightweight tree.
    /// </remarks>
    public sealed class SceneOverlay : OverlayPanel
    {
        private sealed class ActorTreeNode : TreeNode
        {
            public readonly Guid ActorId;
            public Action<ActorTreeNode> DoubleClicked;

            public ActorTreeNode(Actor actor)
            {
                ActorId = actor.ID;
                Text = actor.Name;
            }

            protected override bool OnMouseDoubleClickHeader(ref Float2 location, MouseButton button)
            {
                if (button == MouseButton.Left && DoubleClicked != null)
                {
                    DoubleClicked(this);
                    return true;
                }
                return base.OnMouseDoubleClickHeader(ref location, button);
            }
        }

        private const float RebuildDelay = 0.25f;

        private readonly SearchBox _searchBox;
        private readonly Panel _scrollPanel;
        private readonly Tree _tree;
        private readonly TreeNode _root;
        private readonly Dictionary<Guid, ActorTreeNode> _nodes = new Dictionary<Guid, ActorTreeNode>();
        private readonly HashSet<Guid> _expanded = new HashSet<Guid>();
        private bool _isBound;
        private bool _isDirty;
        private bool _isSyncingSelection;
        private bool _hasBuilt;
        private float _dirtyTime;

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneOverlay"/> class.
        /// </summary>
        public SceneOverlay()
        : base("Scene")
        {
            _searchBox = new SearchBox
            {
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = new Margin(4, 4, 4, 18),
                Parent = Body,
            };
            _searchBox.TextChanged += Rebuild;

            _scrollPanel = new Panel(ScrollBars.Both)
            {
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = new Margin(0, 0, 26, 0),
                Parent = Body,
            };

            _tree = new Tree(true)
            {
                Margin = new Margin(0.0f, 0.0f, -16.0f, _scrollPanel.ScrollBarsSize), // Hide root node
                IsScrollable = true,
                DrawRootTreeLine = false,
            };
            _root = new TreeNode();
            _root.Expand(true);
            _tree.AddChild(_root);
            _tree.SelectedChanged += OnTreeSelectedChanged;
            _tree.Parent = _scrollPanel;
        }

        /// <inheritdoc />
        public override void OnShown()
        {
            base.OnShown();

            if (!_isBound)
            {
                _isBound = true;
                Level.ActorSpawned += OnActorChanged;
                Level.ActorDeleted += OnActorChanged;
                Level.ActorParentChanged += OnActorParentChanged;
                Level.SceneLoaded += OnSceneChanged;
                Level.SceneUnloaded += OnSceneChanged;
                Editor.Instance.SceneEditing.SelectionChanged += OnEditorSelectionChanged;
            }
            Rebuild();
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
        }

        /// <inheritdoc />
        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            // Batch hierarchy changes (eg. spawning many actors) into a single rebuild
            if (_isDirty)
            {
                _dirtyTime += deltaTime;
                if (_dirtyTime >= RebuildDelay)
                    Rebuild();
            }
        }

        /// <summary>
        /// Rebuilds the tree from the loaded scenes.
        /// </summary>
        public void Rebuild()
        {
            _isDirty = false;
            _dirtyTime = 0;

            // Remember expanded nodes (only when not filtering, filtering expands everything)
            var filter = _searchBox.Text;
            var isFiltering = !string.IsNullOrWhiteSpace(filter);
            if (!isFiltering && _nodes.Count != 0)
            {
                _expanded.Clear();
                foreach (var e in _nodes)
                {
                    if (e.Value.IsExpanded)
                        _expanded.Add(e.Key);
                }
            }

            _tree.LockChildrenRecursive();
            _nodes.Clear();
            _root.DisposeChildren();
            foreach (var scene in Level.Scenes)
            {
                var node = Build(scene, filter, isFiltering, true);
                if (node != null)
                    node.Parent = _root;
            }
            _tree.UnlockChildrenRecursive();
            _tree.PerformLayout();
            _hasBuilt = true;

            OnEditorSelectionChanged();
        }

        private ActorTreeNode Build(Actor actor, string filter, bool isFiltering, bool isScene)
        {
            if (!actor || (actor.HideFlags & HideFlags.HideInHierarchy) != 0)
                return null;

            var node = new ActorTreeNode(actor);
            node.DoubleClicked += OnNodeDoubleClicked;
            var anyChild = false;
            for (int i = 0; i < actor.ChildrenCount; i++)
            {
                var child = Build(actor.GetChild(i), filter, isFiltering, false);
                if (child != null)
                {
                    child.Parent = node;
                    anyChild = true;
                }
            }

            if (isFiltering && !anyChild && !isScene && actor.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) == -1)
            {
                node.Dispose();
                return null;
            }

            if (isFiltering || _expanded.Contains(actor.ID) || (isScene && !_hasBuilt))
                node.Expand(true);
            _nodes[actor.ID] = node;
            return node;
        }

        private void OnTreeSelectedChanged(List<TreeNode> before, List<TreeNode> after)
        {
            if (_isSyncingSelection)
                return;

            var selection = new List<SceneGraphNode>(after.Count);
            foreach (var e in after)
            {
                if (e is ActorTreeNode actorNode)
                {
                    var sceneNode = SceneGraphFactory.FindNode(actorNode.ActorId);
                    if (sceneNode != null)
                        selection.Add(sceneNode);
                }
            }
            Editor.Instance.SceneEditing.Select(selection);
        }

        private void OnEditorSelectionChanged()
        {
            var selection = Editor.Instance.SceneEditing.Selection;
            var nodes = new List<TreeNode>(selection.Count);
            foreach (var e in selection)
            {
                if (_nodes.TryGetValue(e.ID, out var node))
                    nodes.Add(node);
            }

            _isSyncingSelection = true;
            _tree.Select(nodes);
            _isSyncingSelection = false;

            if (nodes.Count != 0)
            {
                nodes[0].ExpandAllParents(true);
                _scrollPanel.ScrollViewTo(nodes[0]);
            }
        }

        private void OnNodeDoubleClicked(ActorTreeNode node)
        {
            var sceneNode = SceneGraphFactory.FindNode(node.ActorId);
            if (sceneNode == null)
                return;
            Editor.Instance.SceneEditing.Select(sceneNode);
            Editor.Instance.Windows.EditWin.Viewport.FocusSelection();
        }

        private void OnActorChanged(Actor actor)
        {
            _isDirty = true;
        }

        private void OnActorParentChanged(Actor actor, Actor prevParent)
        {
            _isDirty = true;
        }

        private void OnSceneChanged(Scene scene, Guid sceneId)
        {
            _isDirty = true;
        }

        private void Unbind()
        {
            if (!_isBound)
                return;
            _isBound = false;
            Level.ActorSpawned -= OnActorChanged;
            Level.ActorDeleted -= OnActorChanged;
            Level.ActorParentChanged -= OnActorParentChanged;
            Level.SceneLoaded -= OnSceneChanged;
            Level.SceneUnloaded -= OnSceneChanged;
            Editor.Instance.SceneEditing.SelectionChanged -= OnEditorSelectionChanged;
        }

        /// <inheritdoc />
        public override void OnDestroy()
        {
            Unbind();

            base.OnDestroy();
        }
    }
}
