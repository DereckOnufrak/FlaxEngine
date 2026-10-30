using System.Reflection;
using FlaxEditor.Viewport;
using FlaxEngine;
using FlaxEngine.GUI;

namespace ViewportOverlays
{
    /// <summary>
    /// Base class for a floating panel drawn on top of a viewport. Has a title bar for moving, a close button and a resize grip.
    /// </summary>
    public abstract class OverlayPanel : ContainerControl
    {
        /// <summary>
        /// The height of the title bar.
        /// </summary>
        public const float HeaderHeight = 22.0f;

        /// <summary>
        /// The size of the resize grip in the bottom-right corner.
        /// </summary>
        public const float GripSize = 14.0f;

        /// <summary>
        /// The minimum panel size.
        /// </summary>
        public static readonly Float2 MinSize = new Float2(180, 120);

        private static readonly FieldInfo ViewportInputField = typeof(EditorViewport).GetField("_input", BindingFlags.Instance | BindingFlags.NonPublic);

        private enum DragMode
        {
            None,
            Move,
            Resize,
        }

        private DragMode _dragMode;
        private Float2 _dragStartMouse;
        private Rectangle _dragStartBounds;
        private bool _closeHovered;
        private bool _closePressed;

        /// <summary>
        /// The container for the panel contents (area below the title bar).
        /// </summary>
        public readonly ContainerControl Body;

        /// <summary>
        /// The panel title.
        /// </summary>
        public string Title;

        /// <summary>
        /// The background opacity (0-1).
        /// </summary>
        public float Opacity = 0.94f;

        /// <summary>
        /// Gets the host that owns this panel.
        /// </summary>
        public OverlayHost Host { get; internal set; }

        /// <summary>
        /// True if the panel was placed in the viewport at least once (default placement is applied only once).
        /// </summary>
        internal bool WasPlaced;

        private Rectangle CloseButtonRect => new Rectangle(Width - HeaderHeight, 0, HeaderHeight, HeaderHeight);

        private Rectangle GripRect => new Rectangle(Width - GripSize, Height - GripSize, GripSize, GripSize);

        /// <summary>
        /// Initializes a new instance of the <see cref="OverlayPanel"/> class.
        /// </summary>
        /// <param name="title">The panel title.</param>
        protected OverlayPanel(string title)
        {
            Title = title;
            Size = new Float2(300, 400);
            ClipChildren = true;
            Body = new ContainerControl
            {
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = new Margin(1, 1, HeaderHeight, 1),
                ClipChildren = true,
                Parent = this,
            };
        }

        /// <summary>
        /// Called when the panel gets shown by the host.
        /// </summary>
        public virtual void OnShown()
        {
        }

        /// <summary>
        /// Called when the panel gets hidden by the host.
        /// </summary>
        public virtual void OnHidden()
        {
        }

        /// <summary>
        /// Focuses the primary input of the panel (eg. search box). Called after the panel is shown.
        /// </summary>
        public virtual void FocusContent()
        {
            Focus();
        }

        /// <summary>
        /// Keeps the panel inside the parent area.
        /// </summary>
        public void ClampToParent()
        {
            if (Parent == null)
                return;
            var area = Parent.Size;
            var size = new Float2(Mathf.Clamp(Width, MinSize.X, Mathf.Max(area.X, MinSize.X)), Mathf.Clamp(Height, MinSize.Y, Mathf.Max(area.Y, MinSize.Y)));
            if (size != Size)
                Size = size;
            var location = new Float2(Mathf.Clamp(X, 0, Mathf.Max(area.X - Width, 0)), Mathf.Clamp(Y, 0, Mathf.Max(area.Y - Height, 0)));
            if (location != Location)
                Location = location;
        }

        /// <summary>
        /// Moves the panel on top of the other viewport children.
        /// </summary>
        public void BringToFront()
        {
            if (Parent != null)
                IndexInParent = Parent.ChildrenCount - 1;
        }

        /// <inheritdoc />
        public override void DrawSelf()
        {
            base.DrawSelf();

            var style = Style.Current;
            var bounds = new Rectangle(Float2.Zero, Size);
            Render2D.FillRectangle(bounds, style.Background.AlphaMultiplied(Opacity));

            var header = new Rectangle(0, 0, Width, HeaderHeight);
            Render2D.FillRectangle(header, ContainsFocus ? style.BackgroundSelected : style.LightBackground);
            Render2D.DrawText(style.FontMedium, Title, new Rectangle(6, 0, Width - HeaderHeight - 6, HeaderHeight), style.Foreground, TextAlignment.Near, TextAlignment.Center);
            Render2D.DrawSprite(style.Cross, CloseButtonRect.MakeExpanded(-5), _closeHovered ? style.Foreground : style.ForegroundGrey);
        }

        /// <inheritdoc />
        public override void Draw()
        {
            base.Draw();

            // Border and resize grip on top of the contents
            var style = Style.Current;
            Render2D.DrawRectangle(new Rectangle(Float2.Zero, Size), ContainsFocus ? style.BackgroundSelected : style.BorderNormal);
            var grip = GripRect;
            var gripColor = style.ForegroundGrey.AlphaMultiplied(0.6f);
            Render2D.DrawLine(new Float2(grip.Right - 3, grip.Top + 3), new Float2(grip.Left + 3, grip.Bottom - 3), gripColor);
            Render2D.DrawLine(new Float2(grip.Right - 3, grip.Top + 8), new Float2(grip.Left + 8, grip.Bottom - 3), gripColor);
        }

        /// <inheritdoc />
        public override bool OnMouseDown(Float2 location, MouseButton button)
        {
            BringToFront();
            if (button == MouseButton.Left)
            {
                if (CloseButtonRect.Contains(location))
                {
                    _closePressed = true;
                    return true;
                }
                if (GripRect.Contains(location))
                    _dragMode = DragMode.Resize;
                else if (location.Y < HeaderHeight)
                    _dragMode = DragMode.Move;
                if (_dragMode != DragMode.None)
                {
                    Focus();
                    _dragStartMouse = PointToParent(location);
                    _dragStartBounds = Bounds;
                    StartMouseCapture();
                    return true;
                }
            }

            if (base.OnMouseDown(location, button))
                return true;

            // Consume the click so it doesn't reach the viewport below
            Focus();
            return true;
        }

        /// <inheritdoc />
        public override bool OnMouseUp(Float2 location, MouseButton button)
        {
            if (button == MouseButton.Left)
            {
                if (_dragMode != DragMode.None)
                {
                    _dragMode = DragMode.None;
                    EndMouseCapture();
                    return true;
                }
                if (_closePressed)
                {
                    _closePressed = false;
                    if (CloseButtonRect.Contains(location))
                        Host?.Hide(this);
                    return true;
                }
            }

            base.OnMouseUp(location, button);
            return true;
        }

        /// <inheritdoc />
        public override bool OnMouseDoubleClick(Float2 location, MouseButton button)
        {
            base.OnMouseDoubleClick(location, button);
            return true;
        }

        /// <inheritdoc />
        public override bool OnMouseWheel(Float2 location, float delta)
        {
            base.OnMouseWheel(location, delta);
            ConsumeViewportWheel();
            return true;
        }

        private void ConsumeViewportWheel()
        {
            // EditorViewport accumulates the wheel delta before routing the event to its children and doesn't reset it when the mouse is over a child,
            // so scrolling a panel would also zoom the camera. Reset the pending delta (protected field, no public API for it).
            if (Parent is EditorViewport viewport && ViewportInputField != null)
            {
                var input = (EditorViewport.Input)ViewportInputField.GetValue(viewport);
                input.MouseWheelDelta = 0;
                ViewportInputField.SetValue(viewport, input);
            }
        }

        /// <inheritdoc />
        public override void OnMouseMove(Float2 location)
        {
            _closeHovered = CloseButtonRect.Contains(location);
            if (_dragMode != DragMode.None)
            {
                var delta = PointToParent(location) - _dragStartMouse;
                if (_dragMode == DragMode.Move)
                    Location = _dragStartBounds.Location + delta;
                else
                    Size = Float2.Max(MinSize, _dragStartBounds.Size + delta);
                ClampToParent();
                return;
            }
            Cursor = GripRect.Contains(location) ? CursorType.SizeNWSE : CursorType.Default;

            base.OnMouseMove(location);
        }

        /// <inheritdoc />
        public override void OnMouseLeave()
        {
            _closeHovered = false;
            _closePressed = false;
            Cursor = CursorType.Default;

            base.OnMouseLeave();
        }

        /// <inheritdoc />
        public override void OnEndMouseCapture()
        {
            _dragMode = DragMode.None;

            base.OnEndMouseCapture();
        }

        /// <inheritdoc />
        public override bool OnKeyDown(KeyboardKeys key)
        {
            if (base.OnKeyDown(key))
                return true;

            if (key == KeyboardKeys.Escape)
            {
                Host?.Hide(this);
                return true;
            }

            // Swallow plain typing while a text box inside has focus so viewport shortcuts (gizmo modes, focus, delete) don't fire.
            // Keys with Control/Alt still pass through so editor shortcuts (save, undo, overlay toggles) keep working.
            var root = Root;
            if (root != null && root.FocusedControl is TextBoxBase && !root.GetKey(KeyboardKeys.Control) && !root.GetKey(KeyboardKeys.Alt))
                return true;

            return false;
        }

        /// <inheritdoc />
        public override void OnParentResized()
        {
            base.OnParentResized();

            ClampToParent();
        }
    }
}
