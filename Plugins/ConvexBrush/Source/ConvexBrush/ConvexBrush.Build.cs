using Flax.Build;
using Flax.Build.NativeCpp;

/// <summary>
/// Convex Brush runtime module (C# only, no native code so no engine rebuild is required).
/// </summary>
public class ConvexBrush : GameModule
{
    /// <inheritdoc />
    public override void Init()
    {
        base.Init();

        // C#-only scripting
        BuildNativeCode = false;
    }

    /// <inheritdoc />
    public override void Setup(BuildOptions options)
    {
        base.Setup(options);

        options.ScriptingAPI.IgnoreMissingDocumentationWarnings = true;
    }
}
