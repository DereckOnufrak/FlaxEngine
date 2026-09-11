// Copyright (c) Wojciech Figat. All rights reserved.

using Flax.Build;

/// <summary>
/// The Mesh Modeling plugin build target for the standalone game (runtime-only, no editor tooling).
/// </summary>
public class MeshModelingTarget : GameProjectTarget
{
    /// <inheritdoc />
    public override void Init()
    {
        base.Init();

        Modules.Add("MeshModeling");
    }
}
