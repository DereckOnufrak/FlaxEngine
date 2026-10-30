using System.Collections.Generic;
using FlaxEditor;
using FlaxEditor.Options;
using FlaxEditor.Viewport.Widgets;
using FlaxEngine;

namespace ViewportOverlays
{
    /// <summary>
    /// Editor plugin that adds toggleable overlay panels (Scene, Properties, Content, Log) inside the scene editor viewport and the Game window.
    /// </summary>
    public sealed class ViewportOverlaysPlugin : EditorPlugin
    {
        /// <summary>
        /// The overlay types. Edit shortcuts, default placement and sizes here.
        /// </summary>
        public static readonly OverlayDescriptor[] Overlays =
        {
            new OverlayDescriptor
            {
                Name = "Scene",
                Binding = new InputBinding(KeyboardKeys.Q, KeyboardKeys.Alt),
                Dock = OverlayDock.Left,
                Size = new Float2(280, 600),
                Create = () => new SceneOverlay(),
            },
            new OverlayDescriptor
            {
                Name = "Properties",
                Binding = new InputBinding(KeyboardKeys.W, KeyboardKeys.Alt),
                Dock = OverlayDock.Right,
                Size = new Float2(340, 600),
                Create = () => new PropertiesOverlay(),
            },
            new OverlayDescriptor
            {
                Name = "Content",
                Binding = new InputBinding(KeyboardKeys.E, KeyboardKeys.Alt),
                Dock = OverlayDock.BottomLeft,
                Size = new Float2(360, 320),
                Create = () => new ContentOverlay(),
            },
            new OverlayDescriptor
            {
                Name = "Log",
                Binding = new InputBinding(KeyboardKeys.R, KeyboardKeys.Alt),
                Dock = OverlayDock.BottomRight,
                Size = new Float2(560, 220),
                Create = () => new LogOverlay(),
            },
        };

        /// <summary>
        /// The shortcut that hides all visible overlays (press again to bring them back).
        /// </summary>
        public static InputBinding ToggleAllBinding = new InputBinding(KeyboardKeys.BackQuote, KeyboardKeys.Alt);

        private readonly List<OverlayHost> _hosts = new List<OverlayHost>();

        /// <summary>
        /// Gets the overlay host of the scene editor viewport.
        /// </summary>
        public OverlayHost EditHost { get; private set; }

        /// <summary>
        /// Gets the overlay host of the Game window.
        /// </summary>
        public OverlayHost GameHost { get; private set; }

        /// <inheritdoc />
        public override void InitializeEditor()
        {
            base.InitializeEditor();

            LogBuffer.Start();

            var windows = Editor.Windows;
            if (windows.EditWin != null)
            {
                EditHost = new OverlayHost(windows.EditWin, windows.EditWin.Viewport, false, Overlays, ToggleAllBinding, ViewportWidgetLocation.UpperLeft);
                _hosts.Add(EditHost);
            }
            if (windows.GameWin != null)
            {
                GameHost = new OverlayHost(windows.GameWin, windows.GameWin.Viewport, true, Overlays, ToggleAllBinding, ViewportWidgetLocation.UpperLeft);
                _hosts.Add(GameHost);
            }

            Editor.PlayModeBegin += OnPlayModeBegin;
        }

        private void OnPlayModeBegin()
        {
            // The game UI gets added to the Game window viewport on play, keep the panels above it
            GameHost?.BringPanelsToFront();
        }

        /// <inheritdoc />
        public override void DeinitializeEditor()
        {
            Editor.PlayModeBegin -= OnPlayModeBegin;

            foreach (var host in _hosts)
                host.Dispose();
            _hosts.Clear();
            EditHost = null;
            GameHost = null;

            LogBuffer.Stop();

            base.DeinitializeEditor();
        }
    }
}
