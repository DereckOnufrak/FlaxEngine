using System;
using System.Collections.Generic;
using FlaxEditor;
using FlaxEditor.SceneGraph;
using FlaxEngine;

namespace ConvexBrush
{
    /// <summary>
    /// Editor plugin that adds viewport face editing for <see cref="ConvexBrushCollider"/> (no engine rebuild required).
    /// </summary>
    public sealed class ConvexBrushEditorPlugin : EditorPlugin
    {
        private readonly List<ConvexBrushCollider> _detached = new List<ConvexBrushCollider>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ConvexBrushEditorPlugin"/> class.
        /// </summary>
        public ConvexBrushEditorPlugin()
        {
            _description = new PluginDescription
            {
                Name = "Convex Brush",
                Category = "Physics",
                Description = "Convex collider editable per-face in the viewport, like CSG brushes.",
                Author = "",
                Version = new Version(1, 0),
            };
        }

        /// <inheritdoc />
        public override void InitializeEditor()
        {
            base.InitializeEditor();

            // Register custom scene graph node (face sub-nodes selectable in viewport)
            SceneGraphFactory.CustomNodesTypes[typeof(ConvexBrushCollider)] = typeof(ConvexBrushColliderNode);

            Level.SceneSaving += OnSceneSaving;
            Level.SceneSaved += OnSceneSaveEnd;
            Level.SceneSaveError += OnSceneSaveEnd;
            Editor.Undo.UndoDone += OnUndoEvent;
            Editor.Undo.RedoDone += OnUndoEvent;
            Editor.Undo.ActionDone += OnUndoEvent;
        }

        /// <inheritdoc />
        public override void DeinitializeEditor()
        {
            Editor.Undo.UndoDone -= OnUndoEvent;
            Editor.Undo.RedoDone -= OnUndoEvent;
            Editor.Undo.ActionDone -= OnUndoEvent;
            Level.SceneSaving -= OnSceneSaving;
            Level.SceneSaved -= OnSceneSaveEnd;
            Level.SceneSaveError -= OnSceneSaveEnd;
            SceneGraphFactory.CustomNodesTypes.Remove(typeof(ConvexBrushCollider));

            base.DeinitializeEditor();
        }

        private void OnSceneSaving(Scene scene, Guid sceneId)
        {
            // Runtime-cooked collision lives in a virtual asset (not on disk) so don't save a reference to it
            foreach (var brush in Level.GetActors<ConvexBrushCollider>())
            {
                if (brush.Scene == scene && brush.DetachRuntimeCollision())
                    _detached.Add(brush);
            }
        }

        private void OnSceneSaveEnd(Scene scene, Guid sceneId)
        {
            for (int i = _detached.Count - 1; i >= 0; i--)
            {
                var brush = _detached[i];
                if (!brush)
                {
                    _detached.RemoveAt(i);
                }
                else if (brush.Scene == scene)
                {
                    brush.RestoreRuntimeCollision();
                    _detached.RemoveAt(i);
                }
            }
        }

        private void OnUndoEvent(IUndoAction action)
        {
            // Undo/redo may modify faces array in-place, so sync brushes geometry and collision
            foreach (var brush in Level.GetActors<ConvexBrushCollider>())
                brush.SyncChanges();
        }
    }
}
