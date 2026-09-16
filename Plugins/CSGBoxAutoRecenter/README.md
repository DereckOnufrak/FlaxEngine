# CSG Box Auto Recenter

Editor plugin for Flax Engine. When you edit a `BoxBrush` (CSG Box Brush) by clicking one of
its face handles and dragging it with the transform gizmo to resize the brush, the brush's
pivot (`Center`) drifts away from the actor's origin. This plugin detects the end of that drag
and automatically recenters the pivot back to `(0, 0, 0)`, compensating the actor's transform
so the brush geometry stays exactly where it was in the scene.

## How it works

- `Source/CSGBoxAutoRecenter` is a minimal runtime module holding the `GamePlugin` descriptor.
- `Source/CSGBoxAutoRecenterEditor` is the editor-only module with the actual logic
  (`CSGBoxAutoRecenterEditorPlugin`).
- The plugin subscribes to `Editor.Undo.ActionDone`. When a `TransformObjectsAction` is
  recorded whose selection includes a `BoxBrushNode.SideLinkNode` (the scene graph node for a
  brush face handle), it recenters the owning `BoxBrush`:
  1. Computes the world-space offset of the current local `Center` via
     `Transform.LocalToWorldVector`.
  2. Adds that offset to the actor's `Transform.Translation`.
  3. Resets `Center` to `Vector3.Zero`.
- The recenter itself is recorded through `Editor.Undo.RecordAction`, so it is its own
  undoable/redoable step (Ctrl+Z once undoes the recenter, twice undoes the resize).

## Installing into a game project

1. Copy the `CSGBoxAutoRecenter` folder into your project's `Plugins` folder (create one next
   to your project's `.flaxproj` if it doesn't exist yet).
2. Add a reference to it in your project's `.flaxproj`:
   ```json
   "References": [
     { "Name": "$(EnginePath)/Flax.flaxproj" },
     { "Name": "$(ProjectPath)/Plugins/CSGBoxAutoRecenter/CSGBoxAutoRecenter.flaxproj" }
   ]
   ```
3. Regenerate project files and rebuild scripts. The plugin loads automatically - no extra
   setup is required in-editor.
