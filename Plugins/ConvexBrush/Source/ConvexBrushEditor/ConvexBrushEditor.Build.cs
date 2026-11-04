using Flax.Build;
using Flax.Build.NativeCpp;

/// <summary>
/// Convex Brush editor module (C# only, editor-only tooling).
/// </summary>
public class ConvexBrushEditor : GameEditorModule
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

        // Reference runtime module to access ConvexBrushCollider type
        options.PublicDependencies.Add("ConvexBrush");
    }
}
