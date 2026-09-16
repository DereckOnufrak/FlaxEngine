using Flax.Build;

/// <summary>
/// Editor module for the CSG Box Auto Recenter plugin. Contains the editor-only logic that
/// watches for CSG box brush face edits and recenters the brush pivot afterwards.
/// </summary>
public class CSGBoxAutoRecenterEditor : GameEditorModule
{
    /// <inheritdoc />
    public override void Setup(BuildOptions options)
    {
        base.Setup(options);

        options.PublicDependencies.Add("CSGBoxAutoRecenter");
    }
}
