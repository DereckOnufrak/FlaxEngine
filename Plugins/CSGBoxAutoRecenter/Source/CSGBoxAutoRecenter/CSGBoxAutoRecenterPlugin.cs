// Copyright (c) CSGBoxAutoRecenter. All rights reserved.

using System;
using FlaxEngine;

namespace CSGBoxAutoRecenter
{
    /// <summary>
    /// Runtime plugin descriptor. The plugin has no gameplay behavior on its own - it only
    /// exists so the editor-only <see cref="CSGBoxAutoRecenterEditorPlugin"/> has a
    /// <see cref="GamePlugin"/> to attach to.
    /// </summary>
    public class CSGBoxAutoRecenterPlugin : GamePlugin
    {
        /// <inheritdoc />
        public CSGBoxAutoRecenterPlugin()
        {
            _description = new PluginDescription
            {
                Name = "CSG Box Auto Recenter",
                Category = "Other",
                Author = string.Empty,
                AuthorUrl = null,
                HomepageUrl = null,
                RepositoryUrl = string.Empty,
                Description = "Recenters a CSG Box Brush's pivot after resizing one of its faces with the gizmo, keeping the brush geometry fixed in the scene.",
                Version = new Version(1, 0),
                IsAlpha = false,
                IsBeta = false,
            };
        }
    }
}
