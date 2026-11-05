# Convex Brush Collider (Flax plugin)

C#-only plugin that adds **Convex Brush Collider**: a convex collider you shape in the viewport by selecting and moving its faces, the same way as CSG Box Brush surfaces. No native code, so no engine rebuild is needed.

## Install

1. Copy this `ConvexBrush` folder into your project's `Plugins/` folder.
2. Add a reference in your project's `.flaxproj`:
   ```json
   "References": [
     { "Name": "$(EnginePath)/Flax.flaxproj" },
     { "Name": "$(ProjectPath)/Plugins/ConvexBrush/ConvexBrush.flaxproj" }
   ]
   ```
3. Reference the runtime module from your game module (`Source/Game/Game.Build.cs`), only needed if your game scripts use the type:
   ```cs
   options.PublicDependencies.Add("ConvexBrush");
   ```
4. Restart the editor (it compiles the plugin scripts).

## Usage

- Spawn: **Toolbox → Physics → Convex Brush Collider**, or right-click in the scene tree **New → Physics → Colliders → Convex Brush Collider**.
- Click the brush to select it, then click one of its faces to select the face (highlighted in orange).
  - **Translate gizmo** pushes/pulls the face along its normal.
  - **Rotate gizmo** tilts the face around its center.
  - Inspector shows the face `Normal` and `Distance` for precise values.
- Right-click a selected face:
  - **Duplicate face (bevel)**: adds a coplanar copy; rotate it to make a bevel/chamfer.
  - **Remove face**.
- Actor inspector, **Brush Tools** group:
  - **Bake Collision**: cooks the brush into `Content/ConvexBrush/<Name>_<id>.flax` (Collision Data asset) and links it to the brush. The status line shows `baked`, `bake OUT OF DATE` or `NOT baked`. Re-bake after editing faces. Also available from the brush right-click menu.
  - **Add Face Facing Camera**: adds a face pointing at the editor camera that touches the hull; select it and move it inwards to cut a corner/edge.
  - **Reset To Box**.
- All edits support undo/redo. The full face list is also editable in the `Faces` array.

The brush is the intersection of all face planes (actor local space), so it's always convex. If faces don't enclose a closed volume, edges are drawn red and the collision is not updated.

The actor derives from `MeshCollider`, so `IsTrigger`, physical material, layers, attaching to a `RigidBody` (as a direct child) and so on all work as usual.

## How it works

- Faces → hull vertices (3-plane intersections inside all planes) → per-face polygons → triangles.
- **Editor**: while the bake is missing or out of date, a live preview collision is cooked in memory after every edit so play-in-editor matches what you see. When the bake is up to date, the baked asset is used directly.
- **Game**: only the baked asset is used. No runtime cooking, so **Support Cooking At Runtime** is not needed. A warning is logged if a brush was never baked or its faces changed after baking.
- Bake: the hull is uploaded to a temporary in-memory model and cooked with `Editor.CookMeshCollision` (convex mesh) on a background thread. The output file name includes the brush ID, so duplicated brushes bake to their own file instead of overwriting the original's.
- Up-to-date check: a deterministic hash of the faces is stored with the bake.
- The editor plugin registers a custom scene-graph node (`ConvexBrushColliderNode`) whose child nodes are the faces, which is what makes per-face selection work.
- On scene save the in-memory preview reference is swapped for the baked asset (or none), and a warning lists brushes that still need baking.

## Notes / limitations

- Changing `Faces` from game scripts at runtime updates the geometry but not the collision (it's baked).
- Deleting a brush doesn't delete its baked asset; clean up `Content/ConvexBrush/` manually if needed.
- PhysX convex meshes are limited to 255 vertices (far more than practical brush shapes).
