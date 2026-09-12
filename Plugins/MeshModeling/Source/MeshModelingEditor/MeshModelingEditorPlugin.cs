// Copyright (c) Wojciech Figat. All rights reserved.

using System;
using FlaxEditor;
using MeshModeling;

namespace MeshModelingEditor
{
    /// <summary>
    /// The editor-side plugin: registers the Modeling viewport tool, its toolbox tab, and the
    /// <see cref="EditableMeshData"/> content-browser asset type. Mirrors how Terrain/Foliage are wired into the
    /// editor, just done at runtime (via the public registration APIs) instead of at engine build time.
    /// </summary>
    public class MeshModelingEditorPlugin : EditorPlugin
    {
        private ModelingGizmoMode _gizmoMode;
        private ModelingTab _tab;
        private EditableMeshDataProxy _proxy;

        /// <inheritdoc />
        public override Type GamePluginType => typeof(MeshModelingPlugin);

        /// <inheritdoc />
        public override void InitializeEditor()
        {
            base.InitializeEditor();

            var viewport = Editor.Windows.EditWin.Viewport;
            viewport.Gizmos.AddMode(_gizmoMode = new ModelingGizmoMode());

            _proxy = new EditableMeshDataProxy();
            Editor.ContentDatabase.AddProxy(_proxy, true);

            _tab = new ModelingTab(Editor.Icons.Terrain96, Editor, _gizmoMode);
            Editor.Windows.ToolboxWin.TabsControl.AddTab(_tab);
        }

        /// <inheritdoc />
        public override void DeinitializeEditor()
        {
            // Best-effort: DeinitializeEditor can run after the editor has already torn down the windows/viewport
            // it belongs to (e.g. on application exit, as opposed to a plugin being disabled while the editor keeps
            // running), in which case these registries are already cleared and RemoveMode/RemoveProxy throw
            // "Not added." Nothing further needs unregistering once the editor itself is going away, so swallow
            // that case here rather than letting it surface as a "Failed to shutdown editor!" error dialog.
            try
            {
                _tab?.Dispose();
            }
            catch (Exception)
            {
            }
            _tab = null;

            try
            {
                if (_proxy != null)
                    Editor.ContentDatabase.RemoveProxy(_proxy);
            }
            catch (Exception)
            {
            }
            _proxy = null;

            try
            {
                if (_gizmoMode != null)
                    Editor.Windows.EditWin.Viewport.Gizmos.RemoveMode(_gizmoMode);
            }
            catch (Exception)
            {
            }
            _gizmoMode = null;

            base.DeinitializeEditor();
        }
    }
}
