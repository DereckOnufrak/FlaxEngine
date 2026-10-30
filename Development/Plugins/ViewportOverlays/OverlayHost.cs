using System;
using System.Collections.Generic;
using FlaxEditor;
using FlaxEditor.GUI.ContextMenu;
using FlaxEditor.Options;
using FlaxEditor.Viewport.Widgets;
using FlaxEditor.Windows;
using FlaxEngine;
using FlaxEngine.GUI;

namespace ViewportOverlays
{
    /// <summary>
    /// Default placement of an overlay inside the viewport (used the first time it's shown).
    /// </summary>
    public enum OverlayDock
    {
        /// <summary>
        /// Left side, full height.
        /// </summary>
        Left,

        /// <summary>
        /// Right side, full height.
        /// </summary>
        Right,

        /// <summary>
        /// Bottom-left corner.
        /// </summary>
        BottomLeft,

        /// <summary>
        /// Bottom-right corner.
        /// </summary>
        BottomRight,
    }

    /// <summary>
    /// Describes an overlay type that can be toggled in the viewports.
    /// </summary>
    public sealed class OverlayDescriptor
    {
        /// <summary>
        /// The display name.
        /// </summary>
        public string Name;

        /// <summary>
        /// The toggle shortcut.
        /// </summary>
        public InputBinding Binding;

        /// <summary>
        /// The default placement.
        /// </summary>
        public OverlayDock Dock;

        /// <summary>
        /// The default size.
        /// </summary>
        public Float2 Size;

        /// <summary>
        /// Creates a new panel instance (each viewport has its own instances).
        /// </summary>
        public Func<OverlayPanel> Create;
    }

    /// <summary>
    /// Owns the overlay panels of a single viewport, binds the toggle shortcuts to its window and adds a "Panels" menu widget.
    /// </summary>
    public sealed class OverlayHost : IDisposable
    {
        private const float WidgetsRowHeight = 32.0f;
        private const float EdgeMargin = 8.0f;

        private readonly IReadOnlyList<OverlayDescriptor> _descriptors;
        private readonly Dictionary<OverlayDescriptor, OverlayPanel> _panels = new Dictionary<OverlayDescriptor, OverlayPanel>();
        private readonly List<InputActionsContainer.Binding> _bindings = new List<InputActionsContainer.Binding>();
        private readonly List<OverlayDescriptor> _hiddenByToggleAll = new List<OverlayDescriptor>();
        private ViewportWidgetsContainer _widget;
        private ContextMenu _menu;
        private bool _isCursorFreed;
        private CursorLockMode _gameCursorLock;
        private bool _gameCursorVisible;

        /// <summary>
        /// The editor window that contains the viewport.
        /// </summary>
        public readonly EditorWindow Window;

        /// <summary>
        /// The viewport control the overlays are placed in.
        /// </summary>
        public readonly ContainerControl Viewport;

        /// <summary>
        /// True if this is the Game window viewport (the mouse gets unlocked in play mode when a panel opens).
        /// </summary>
        public readonly bool IsGameView;

        /// <summary>
        /// Initializes a new instance of the <see cref="OverlayHost"/> class.
        /// </summary>
        /// <param name="window">The editor window.</param>
        /// <param name="viewport">The viewport control.</param>
        /// <param name="isGameView">True if it's the Game window viewport.</param>
        /// <param name="descriptors">The overlay types.</param>
        /// <param name="toggleAllBinding">The shortcut that hides all visible overlays (and shows them again).</param>
        /// <param name="widgetLocation">The location of the "Panels" menu widget.</param>
        public OverlayHost(EditorWindow window, ContainerControl viewport, bool isGameView, IReadOnlyList<OverlayDescriptor> descriptors, InputBinding toggleAllBinding, ViewportWidgetLocation widgetLocation)
        {
            Window = window;
            Viewport = viewport;
            IsGameView = isGameView;
            _descriptors = descriptors;

            // Shortcuts are bound on the window so they work when the viewport or any overlay has focus
            foreach (var desc in descriptors)
            {
                var d = desc;
                AddBinding(d.Binding, () => Toggle(d));
            }
            AddBinding(toggleAllBinding, ToggleAll);

            CreateWidget(widgetLocation);

            if (IsGameView)
                Scripting.Update += OnScriptingUpdate;
        }

        private static bool IsPlaying => Editor.Instance.StateMachine.IsPlayMode;

        private void AddBinding(InputBinding binding, Action callback)
        {
            var b = new InputActionsContainer.Binding(options => binding, callback);
            _bindings.Add(b);
            Window.InputActions.Add(b);
        }

        private void CreateWidget(ViewportWidgetLocation location)
        {
            _menu = new ContextMenu();
            foreach (var desc in _descriptors)
            {
                var d = desc;
                ContextMenuButton button = null;
                button = _menu.AddButton(d.Name, d.Binding, () =>
                {
                    Toggle(d);
                    button.Checked = IsVisible(d);
                });
                button.Tag = d;
                button.CloseMenuOnClick = false;
            }
            _menu.AddSeparator();
            _menu.AddButton("Hide/Show All", () => ToggleAll());
            _menu.VisibleChanged += control =>
            {
                foreach (var child in _menu.ItemsContainer.Children)
                {
                    if (child is ContextMenuButton b && b.Tag is OverlayDescriptor d)
                        b.Checked = IsVisible(d);
                }
            };

            _widget = new ViewportWidgetsContainer(location);
            new ViewportWidgetButton("Panels", SpriteHandle.Invalid, _menu)
            {
                TooltipText = "Toggle overlay panels",
                Parent = _widget,
            };
            _widget.Parent = Viewport;

            // Editor viewports arrange their widgets in layout, plain render output controls (Game window) don't
            ViewportWidgetsContainer.ArrangeWidgets(Viewport);
            Viewport.SizeChanged += OnViewportSizeChanged;
        }

        private void OnViewportSizeChanged(Control control)
        {
            ViewportWidgetsContainer.ArrangeWidgets(Viewport);
        }

        /// <summary>
        /// Gets the panel instance of the given overlay type (null if never shown).
        /// </summary>
        /// <param name="desc">The overlay type.</param>
        /// <returns>The panel or null.</returns>
        public OverlayPanel GetPanel(OverlayDescriptor desc)
        {
            return _panels.TryGetValue(desc, out var panel) ? panel : null;
        }

        /// <summary>
        /// Checks if the given overlay is visible.
        /// </summary>
        /// <param name="desc">The overlay type.</param>
        /// <returns>True if visible.</returns>
        public bool IsVisible(OverlayDescriptor desc)
        {
            var panel = GetPanel(desc);
            return panel != null && panel.Visible;
        }

        /// <summary>
        /// Shows the overlay if hidden, hides it otherwise.
        /// </summary>
        /// <param name="desc">The overlay type.</param>
        public void Toggle(OverlayDescriptor desc)
        {
            if (IsVisible(desc))
                Hide(GetPanel(desc));
            else
                Show(desc);
        }

        /// <summary>
        /// Shows the overlay.
        /// </summary>
        /// <param name="desc">The overlay type.</param>
        /// <returns>The panel.</returns>
        public OverlayPanel Show(OverlayDescriptor desc)
        {
            _hiddenByToggleAll.Clear();
            if (!_panels.TryGetValue(desc, out var panel))
            {
                panel = desc.Create();
                panel.Host = this;
                panel.Visible = false;
                panel.Parent = Viewport;
                _panels.Add(desc, panel);
            }
            if (!panel.WasPlaced)
            {
                panel.WasPlaced = true;
                panel.Bounds = GetDefaultBounds(desc);
            }

            var wasVisible = panel.Visible;
            panel.Visible = true;
            panel.ClampToParent();
            panel.BringToFront();
            if (!wasVisible)
                panel.OnShown();
            panel.FocusContent();

            // Focus entering the Game window re-applies the game cursor lock, so free the cursor after focusing the panel
            if (IsGameView && IsPlaying)
                FreeCursor();

            return panel;
        }

        /// <summary>
        /// Hides the panel.
        /// </summary>
        /// <param name="panel">The panel.</param>
        public void Hide(OverlayPanel panel)
        {
            if (panel == null || !panel.Visible)
                return;
            var hadFocus = panel.ContainsFocus;
            panel.Visible = false;
            panel.OnHidden();
            if (hadFocus)
            {
                if (IsGameView)
                    ReturnInputToGame();
                else
                    Viewport.Focus();
            }
        }

        /// <summary>
        /// Hides all visible overlays. If none is visible, shows again the ones hidden by the previous call.
        /// </summary>
        public void ToggleAll()
        {
            var visible = new List<OverlayDescriptor>();
            foreach (var e in _panels)
            {
                if (e.Value.Visible)
                    visible.Add(e.Key);
            }
            if (visible.Count != 0)
            {
                foreach (var desc in visible)
                    Hide(_panels[desc]);
                _hiddenByToggleAll.Clear();
                _hiddenByToggleAll.AddRange(visible);
            }
            else if (_hiddenByToggleAll.Count != 0)
            {
                var toShow = _hiddenByToggleAll.ToArray();
                foreach (var desc in toShow)
                    Show(desc);
                _hiddenByToggleAll.Clear();
            }
        }

        /// <summary>
        /// Moves visible panels on top of other viewport children (eg. after the game UI got added).
        /// </summary>
        public void BringPanelsToFront()
        {
            foreach (var panel in _panels.Values)
            {
                if (panel.Visible)
                    panel.BringToFront();
            }
            if (_widget != null)
                _widget.IndexInParent = Viewport.ChildrenCount - 1;
        }

        private OverlayPanel GetFocusedPanel()
        {
            foreach (var panel in _panels.Values)
            {
                if (panel.Visible && panel.ContainsFocus)
                    return panel;
            }
            return null;
        }

        private bool IsMouseOverPanel()
        {
            foreach (var panel in _panels.Values)
            {
                if (panel.Visible && panel.IsMouseOver)
                    return true;
            }
            return _widget != null && _widget.IsMouseOver;
        }

        private void FreeCursor()
        {
            if (!_isCursorFreed)
            {
                _isCursorFreed = true;
                _gameCursorLock = Screen.CursorLock;
                _gameCursorVisible = Screen.CursorVisible;
            }
            if (Screen.CursorLock != CursorLockMode.None)
                Screen.CursorLock = CursorLockMode.None;
            if (!Screen.CursorVisible)
                Screen.CursorVisible = true;
        }

        private void ReturnInputToGame()
        {
            // The Game window still contains focus (it was on a child panel) so it won't restore the cursor by itself
            Window.Focus();
            if (_isCursorFreed && IsPlaying)
            {
                Screen.CursorLock = _gameCursorLock;
                Screen.CursorVisible = _gameCursorVisible;
            }
            _isCursorFreed = false;
        }

        private void OnScriptingUpdate()
        {
            if (!IsPlaying)
            {
                _isCursorFreed = false;
                return;
            }
            if (GetFocusedPanel() == null)
                return;

            // Clicking the game area outside the panels gives the input back to the game
            var root = Viewport.Root;
            if (root != null && Viewport.IsMouseOver && !IsMouseOverPanel() && (root.GetMouseButtonDown(MouseButton.Left) || root.GetMouseButtonDown(MouseButton.Right)))
                ReturnInputToGame();
            else
                FreeCursor(); // Games often re-apply the cursor lock every frame
        }

        private Rectangle GetDefaultBounds(OverlayDescriptor desc)
        {
            var area = Viewport.Size;
            var top = WidgetsRowHeight;
            var size = new Float2(Mathf.Min(desc.Size.X, area.X - EdgeMargin * 2), Mathf.Min(desc.Size.Y, area.Y - top - EdgeMargin));
            switch (desc.Dock)
            {
            case OverlayDock.Left:
                return new Rectangle(EdgeMargin, top, size.X, area.Y - top - EdgeMargin);
            case OverlayDock.Right:
                return new Rectangle(area.X - size.X - EdgeMargin, top, size.X, area.Y - top - EdgeMargin);
            case OverlayDock.BottomLeft:
                return new Rectangle(EdgeMargin, area.Y - size.Y - EdgeMargin, size.X, size.Y);
            case OverlayDock.BottomRight:
                return new Rectangle(area.X - size.X - EdgeMargin, area.Y - size.Y - EdgeMargin, size.X, size.Y);
            default:
                return new Rectangle(EdgeMargin, top, size.X, size.Y);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (IsGameView)
                Scripting.Update -= OnScriptingUpdate;
            foreach (var b in _bindings)
                Window.InputActions.Bindings.Remove(b);
            _bindings.Clear();

            Viewport.SizeChanged -= OnViewportSizeChanged;
            foreach (var panel in _panels.Values)
            {
                if (panel.Visible)
                    panel.OnHidden();
                panel.Dispose();
            }
            _panels.Clear();
            _widget?.Dispose();
            _widget = null;
            _menu?.Dispose();
            _menu = null;
        }
    }
}
