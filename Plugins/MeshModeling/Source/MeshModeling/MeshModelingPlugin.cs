// Copyright (c) Wojciech Figat. All rights reserved.

using System;
using FlaxEngine;

namespace MeshModeling
{
    /// <summary>
    /// The runtime descriptor for the Mesh Modeling plugin. Carries no runtime logic of its own - the
    /// <see cref="EditableMesh"/> actor works standalone; this class only exists to register the plugin with the
    /// engine's plugin system (required for <see cref="MeshModelingEditor.MeshModelingEditorPlugin"/> to attach to).
    /// </summary>
    public class MeshModelingPlugin : GamePlugin
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MeshModelingPlugin"/> class.
        /// </summary>
        public MeshModelingPlugin()
        {
            _description = new PluginDescription
            {
                Name = "Mesh Modeling",
                Category = "Other",
                Author = "",
                Description = "In-editor mesh modeling: create primitives and edit vertices/edges/faces (move, extrude, delete) directly in the viewport, ProBuilder-style.",
                Version = new Version(0, 1, 0),
                IsAlpha = true,
            };
        }
    }
}
