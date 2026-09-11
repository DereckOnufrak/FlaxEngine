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
            if (_tab != null)
            {
                _tab.Dispose();
                _tab = null;
            }
            if (_proxy != null)
            {
                Editor.ContentDatabase.RemoveProxy(_proxy);
                _proxy = null;
            }
            if (_gizmoMode != null)
            {
                Editor.Windows.EditWin.Viewport.Gizmos.RemoveMode(_gizmoMode);
                _gizmoMode = null;
            }

            base.DeinitializeEditor();
        }
    }
}
