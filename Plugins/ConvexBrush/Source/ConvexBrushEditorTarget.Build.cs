using Flax.Build;

/// <summary>
/// The Convex Brush plugin editor target.
/// </summary>
public class ConvexBrushEditorTarget : GameProjectEditorTarget
{
    /// <inheritdoc />
    public override void Init()
    {
        base.Init();

        Modules.Add("ConvexBrush");
        Modules.Add("ConvexBrushEditor");
    }
}
