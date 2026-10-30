using System;
using FlaxEngine;
using FlaxEngine.GUI;

namespace ViewportOverlays
{
    /// <summary>
    /// A single row in an overlay list. Supports click, double-click, drag start and a custom icon drawer.
    /// </summary>
    public class OverlayListItem : Control
    {
        /// <summary>
        /// The default row height.
        /// </summary>
        public const float DefaultHeight = 20.0f;

        private bool _isMouseDown;
        private Float2 _mouseDownPos;

        /// <summary>
        /// The row text.
        /// </summary>
        public string Text;

        /// <summary>
        /// The text color. Uses the style foreground color if transparent.
        /// </summary>
        public Color TextColor = Color.Transparent;

        /// <summary>
        /// The icon sprite (optional).
        /// </summary>
        public SpriteHandle Icon;

        /// <summary>
        /// Custom icon drawing (optional, overrides <see cref="Icon"/>). Receives the icon rectangle.
        /// </summary>
        public Action<Rectangle> DrawIcon;

        /// <summary>
        /// True if the row is selected (highlighted).
        /// </summary>
        public bool IsSelected;

        /// <summary>
        /// Called on left mouse click.
        /// </summary>
        public event Action<OverlayListItem> Clicked;

        /// <summary>
        /// Called on left mouse double-click.
        /// </summary>
        public event Action<OverlayListItem> DoubleClicked;

        /// <summary>
        /// Called when user starts dragging the row. Return drag data to start a drag and drop operation, or null to skip.
        /// </summary>
        public Func<OverlayListItem, DragData> StartDrag;

        /// <summary>
        /// Initializes a new instance of the <see cref="OverlayListItem"/> class.
        /// </summary>
        /// <param name="text">The row text.</param>
        public OverlayListItem(string text)
        : base(0, 0, 100, DefaultHeight)
        {
            Text = text;
        }

        /// <inheritdoc />
        public override void Draw()
        {
            base.Draw();

            var style = Style.Current;
            var rect = new Rectangle(Float2.Zero, Size);
            if (IsSelected)
                Render2D.FillRectangle(rect, style.BackgroundSelected);
            else if (IsMouseOver)
                Render2D.FillRectangle(rect, style.BackgroundHighlighted);

            float textX = 4;
            var iconRect = new Rectangle(2, 2, Height - 4, Height - 4);
            if (DrawIcon != null)
            {
                DrawIcon(iconRect);
                textX = iconRect.Right + 4;
            }
            else if (Icon.IsValid)
            {
                Render2D.DrawSprite(Icon, iconRect, style.Foreground);
                textX = iconRect.Right + 4;
            }

            var color = TextColor.A > 0 ? TextColor : style.Foreground;
            Render2D.DrawText(style.FontMedium, Text, new Rectangle(textX, 0, Width - textX - 2, Height), color, TextAlignment.Near, TextAlignment.Center);
        }

        /// <inheritdoc />
        public override bool OnMouseDown(Float2 location, MouseButton button)
        {
            if (button == MouseButton.Left)
            {
                _isMouseDown = true;
                _mouseDownPos = location;
                return true;
            }

            return base.OnMouseDown(location, button);
        }

        /// <inheritdoc />
        public override bool OnMouseUp(Float2 location, MouseButton button)
        {
            if (button == MouseButton.Left && _isMouseDown)
            {
                _isMouseDown = false;
                Clicked?.Invoke(this);
                return true;
            }

            return base.OnMouseUp(location, button);
        }

        /// <inheritdoc />
        public override bool OnMouseDoubleClick(Float2 location, MouseButton button)
        {
            if (button == MouseButton.Left)
            {
                DoubleClicked?.Invoke(this);
                return true;
            }

            return base.OnMouseDoubleClick(location, button);
        }

        /// <inheritdoc />
        public override void OnMouseMove(Float2 location)
        {
            if (_isMouseDown && Float2.Distance(_mouseDownPos, location) > 8.0f)
                TryStartDrag();

            base.OnMouseMove(location);
        }

        /// <inheritdoc />
        public override void OnMouseLeave()
        {
            // Leaving the row with the button held also counts as a drag
            if (_isMouseDown)
                TryStartDrag();

            base.OnMouseLeave();
        }

        private void TryStartDrag()
        {
            _isMouseDown = false;
            var data = StartDrag?.Invoke(this);
            if (data != null)
                DoDragDrop(data);
        }
    }
}
