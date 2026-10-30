# Viewport Overlays

An editor plugin that adds toggleable overlay panels inside the scene editor viewport and the Game window. You can hide the docked editor windows and still reach the scene tree, the properties, the content and the log from inside the view.

You don't need to change the engine source. The plugin is a C#-only, editor-only module that goes in your game project.

## Panels

| Panel | Shortcut | What it does |
|---|---|---|
| Scene | `Alt+Q` | Searchable actor hierarchy of the loaded scenes, synced with the editor selection. Double-click focuses the scene camera on the actor. |
| Properties | `Alt+W` | Properties of the selected objects. Uses the same `CustomEditorPresenter` as the Properties window, so undo, prefabs and custom editors work. |
| Content | `Alt+E` | Search of the project assets (up to 200 results shown). Double-click opens the asset editor. Drag a row into the scene viewport to spawn or assign it. |
| Log | `Alt+R` | Recent log messages with Info, Warning and Error filters. Click selects the actor or script that logged the message. Double-click copies the message. |
| Hide/Show all | ``Alt+` `` | Hides every visible panel. Press it again to bring them back. |

- Each viewport also gets a **Panels** dropdown in its upper-left corner.
- Panels can be moved by their title bar and resized from the bottom-right corner. `Esc` closes the focused panel.
- While you type in a panel's text field, single-key viewport shortcuts (gizmo modes, focus, delete) are blocked. Shortcuts with `Ctrl` or `Alt` still work.
- **In the Game window during play:** opening a panel frees the cursor, even if the game locks it. Clicking the game area outside the panels, or closing the panel, gives the cursor back to the game.

To change the shortcuts, default placement or sizes, edit `ViewportOverlaysPlugin.Overlays` and `ViewportOverlaysPlugin.ToggleAllBinding`.

## Install

1. Copy this folder into your project as `<Project>/Source/ViewportOverlays/`.
2. Add the module to your editor target, `<Project>/Source/<Project>EditorTarget.Build.cs`:
   ```cs
   public override void Init()
   {
       base.Init();

       Modules.Add("Game");
       Modules.Add("ViewportOverlays");
   }
   ```
3. Regenerate the project files, or let the editor recompile scripts. Flax finds `ViewportOverlaysPlugin` automatically because it derives from `EditorPlugin`.

`ViewportOverlays.Build.cs` declares a `GameEditorModule` with `BuildNativeCode = false`. The module only exists in editor builds and never ships with the game.

## Files

| File | Purpose |
|---|---|
| `ViewportOverlaysPlugin.cs` | Entry point. Defines the panels and shortcuts, and creates one host per viewport. |
| `OverlayHost.cs` | Per-viewport manager: creates panels, binds shortcuts, adds the Panels menu, places panels and handles the cursor in the Game window. |
| `OverlayPanel.cs` | Base class for a panel: title bar, close button, resize grip, input isolation from the viewport. |
| `OverlayListItem.cs` | List row with icon, click, double-click and drag support. |
| `SceneOverlay.cs`, `PropertiesOverlay.cs`, `ContentOverlay.cs`, `LogOverlay.cs` | The panels. |

To add your own panel, derive from `OverlayPanel`, put the UI in `Body`, and add an `OverlayDescriptor` to `ViewportOverlaysPlugin.Overlays`.

## Known limits

- **Panel instances:** each viewport has its own panel instances, so panel positions aren't shared between the editor and game views.
- **Scene panel:** the tree is built by the plugin, because the Scene window's tree nodes can only have one parent. It has no drag-and-drop reparenting, no context menu and no actor icons. It updates on actor spawn, delete or reparent and on scene load or unload. It doesn't update when an actor is renamed; reopen the panel or type in the search box to refresh it.
- **Mouse wheel workaround:** `EditorViewport` stores the mouse wheel delta before passing the event to child controls. The panel resets that delta through reflection on the protected `EditorViewport._input` field, so scrolling a panel doesn't zoom the camera. If that field is renamed in a future engine version, scrolling a panel will zoom the camera again; nothing else breaks.
- **Game input during play:** when a panel in the Game window has focus during play, the game may still receive raw keyboard input from `Input.GetKey`. This hasn't been tested.
- **Content panel:** only lists assets from the game project's `Content` folder, not engine or plugin content.
