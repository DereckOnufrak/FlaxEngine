// Copyright (c) Wojciech Figat. All rights reserved.

using Flax.Build;

/// <summary>
/// The Mesh Modeling plugin build target for the editor (includes the Modeling viewport tool and toolbox tab).
/// </summary>
public class MeshModelingEditorTarget : GameProjectEditorTarget
{
    /// <inheritdoc />
    public override void Init()
    {
        base.Init();

        Modules.Add("MeshModeling");
        Modules.Add("MeshModelingEditor");
    }
}
