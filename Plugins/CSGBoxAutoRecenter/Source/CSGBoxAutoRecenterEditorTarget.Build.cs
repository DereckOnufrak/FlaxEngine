using Flax.Build;

public class CSGBoxAutoRecenterEditorTarget : GameProjectEditorTarget
{
    /// <inheritdoc />
    public override void Init()
    {
        base.Init();

        Platforms = new[]
        {
            TargetPlatform.Windows,
            TargetPlatform.Linux,
            TargetPlatform.Mac,
        };

        Modules.Add("CSGBoxAutoRecenter");
        Modules.Add("CSGBoxAutoRecenterEditor");
    }
}
