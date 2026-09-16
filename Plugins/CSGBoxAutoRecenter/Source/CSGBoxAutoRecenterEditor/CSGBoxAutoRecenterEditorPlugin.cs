// Copyright (c) CSGBoxAutoRecenter. All rights reserved.

using System;
using System.Collections.Generic;
using FlaxEditor;
using FlaxEditor.SceneGraph.Actors;
using FlaxEngine;

namespace CSGBoxAutoRecenter
{
    /// <summary>
    /// Editor plugin that watches for CSG Box Brush face edits done with the transform gizmo
    /// (click a face side-link handle, drag it to resize the brush) and, once the drag ends,
    /// recenters the brush pivot (<see cref="BoxBrush.Center"/>) back to zero while shifting
    /// the actor's transform by the same amount so the brush geometry does not move in the
    /// scene.
    /// </summary>
    public class CSGBoxAutoRecenterEditorPlugin : EditorPlugin
    {
        /// <inheritdoc />
        public override Type GamePluginType => typeof(CSGBoxAutoRecenterPlugin);

        /// <inheritdoc />
        public override void InitializeEditor()
        {
            base.InitializeEditor();

            Editor.Undo.ActionDone += OnUndoActionDone;
        }

        /// <inheritdoc />
        public override void DeinitializeEditor()
        {
            Editor.Undo.ActionDone -= OnUndoActionDone;

            base.DeinitializeEditor();
        }

        private void OnUndoActionDone(IUndoAction action)
        {
            // The transform gizmo records a TransformObjectsAction when a drag ends. Dragging
            // one of a BoxBrush's face handles (BoxBrushNode.SideLinkNode) goes through this
            // same path and ends up changing the brush's Size and Center.
            if (!(action is TransformObjectsAction transformAction))
                return;

            HashSet<BoxBrush> brushes = null;
            var selection = transformAction.Data.Selection;
            if (selection == null)
                return;
            for (int i = 0; i < selection.Length; i++)
            {
                if (selection[i] is BoxBrushNode.SideLinkNode sideLink && sideLink.Brush != null)
                {
                    brushes ??= new HashSet<BoxBrush>();
                    brushes.Add(sideLink.Brush);
                }
            }
            if (brushes == null)
                return;

            foreach (var brush in brushes)
                Recenter(brush);
        }

        private void Recenter(BoxBrush brush)
        {
            var center = brush.Center;
            if (center.IsZero)
                return;

            // Record as its own undo step so the recenter can be undone/redone independently
            // of the resize that triggered it.
            Editor.Undo.RecordAction(brush, "Recenter CSG box pivot", target =>
            {
                var b = (BoxBrush)target;
                var localOffset = b.Center;

                // World-space displacement of the local pivot point, so shifting the actor's
                // translation by it keeps every vertex at the same world position.
                var worldOffset = b.Transform.LocalToWorldVector(localOffset);

                var transform = b.Transform;
                transform.Translation += worldOffset;
                b.Transform = transform;

                b.Center = Vector3.Zero;
            });
        }
    }
}
