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
  - **Add Face Facing Camera**: adds a face pointing at the editor camera that touches the hull; select it and move it inwards to cut a corner/edge.
  - **Reset To Box**.
- All edits support undo/redo. The full face list is also editable in the `Faces` array.

The brush is the intersection of all face planes (actor local space), so it's always convex. If faces don't enclose a closed volume, edges are drawn red and the collision is not updated.

The actor derives from `MeshCollider`, so `IsTrigger`, physical material, layers, attaching to a `RigidBody` (as a direct child) and so on all work as usual.

## How it works

- Faces → hull vertices (3-plane intersections inside all planes) → per-face polygons → triangles.
- The hull is cooked as a **convex mesh** into a virtual `CollisionData` asset on `OnBeginPlay` and after each edit. Nothing is written to `Content/`.
- The editor plugin registers a custom scene-graph node (`ConvexBrushColliderNode`) whose child nodes are the faces, which is what makes per-face selection work.
- On scene save the virtual asset reference is detached so scenes don't reference a non-existent asset.

## Notes / limitations

- **Cooked games**: enable **Physics Settings → Support Cooking At Runtime**, otherwise the collision can't be generated at runtime (an error is logged).
- Saving the brush inside a **prefab** stores the virtual asset ID; it's regenerated on spawn but a "missing asset" warning may be logged when the prefab loads.
- PhysX convex meshes are limited to 255 vertices (far more than practical brush shapes).
