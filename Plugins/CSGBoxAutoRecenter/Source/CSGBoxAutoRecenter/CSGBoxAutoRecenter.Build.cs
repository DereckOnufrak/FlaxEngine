using Flax.Build;

/// <summary>
/// Runtime module for the CSG Box Auto Recenter plugin. Holds the plugin descriptor only;
/// all of the actual behavior lives in the editor-only CSGBoxAutoRecenterEditor module.
/// </summary>
public class CSGBoxAutoRecenter : GameModule
{
    /// <inheritdoc />
    public override void Setup(BuildOptions options)
    {
        base.Setup(options);
    }
}
