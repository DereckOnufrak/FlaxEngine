using Flax.Build;

/// <summary>
/// Editor-only C# module with the viewport overlay panels.
/// </summary>
public class ViewportOverlays : GameEditorModule
{
    /// <inheritdoc />
    public override void Init()
    {
        base.Init();

        // C#-only module
        BuildNativeCode = false;
    }
}
