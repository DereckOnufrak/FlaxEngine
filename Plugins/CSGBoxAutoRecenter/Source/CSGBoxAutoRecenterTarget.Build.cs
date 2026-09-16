using Flax.Build;

public class CSGBoxAutoRecenterTarget : GameProjectTarget
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
    }
}
