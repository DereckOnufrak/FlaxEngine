// Copyright (c) Wojciech Figat. All rights reserved.

using Flax.Build;
using Flax.Build.NativeCpp;

/// <summary>
/// The Mesh Modeling plugin editor module: the Modeling viewport tool, toolbox tab, undo actions and content-browser
/// asset type. Editor-only, not included in packaged games.
/// </summary>
public class MeshModelingEditor : GameEditorModule
{
    /// <inheritdoc />
    public override void Setup(BuildOptions options)
    {
        base.Setup(options);

        options.PublicDependencies.Add("MeshModeling");
    }
}
