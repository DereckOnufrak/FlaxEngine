using Flax.Build;

/// <summary>
/// The Convex Brush plugin game target.
/// </summary>
public class ConvexBrushTarget : GameProjectTarget
{
    /// <inheritdoc />
    public override void Init()
    {
        base.Init();

        Modules.Add("ConvexBrush");
    }
}
