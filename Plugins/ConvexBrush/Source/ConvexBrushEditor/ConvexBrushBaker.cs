using System;
using System.IO;
using System.Threading.Tasks;
using FlaxEditor;
using FlaxEngine;
using Object = FlaxEngine.Object;

namespace ConvexBrush
{
    /// <summary>
    /// Bakes <see cref="ConvexBrushCollider"/> geometry into <see cref="CollisionData"/> assets saved in the project content.
    /// </summary>
    public static class ConvexBrushBaker
    {
        /// <summary>
        /// The project content sub-folder for baked brushes.
        /// </summary>
        public const string BakeFolder = "ConvexBrush";

        /// <summary>
        /// Bakes the brush collision (async). Reuses the brush's existing baked asset file if it was baked by the same brush.
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="undo">The undo to record the change with (optional).</param>
        /// <param name="completed">Callback invoked on the main thread after baking (true if succeeded).</param>
        public static void Bake(ConvexBrushCollider brush, Undo undo = null, Action<bool> completed = null)
        {
            if (!brush)
                return;
            var geometry = brush.Geometry;
            if (!geometry.IsValid)
            {
                Editor.LogError($"Cannot bake Convex Brush Collider '{brush.Name}': faces don't form a closed convex volume.");
                completed?.Invoke(false);
                return;
            }
            var facesHash = brush.FacesHash;
            var path = GetBakePath(brush);
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            // Cooking API takes a model so upload hull into a temporary virtual model
            var model = Content.CreateVirtualAsset<Model>();
            model.SetupLODs(new[] { 1 });
            model.LODs[0].Meshes[0].UpdateMesh(geometry.Vertices, geometry.Triangles);

            // Virtual model data is downloaded from GPU so cook off the main thread
            Task.Run(() =>
            {
                bool failed;
                try
                {
                    failed = Editor.CookMeshCollision(path, CollisionDataType.ConvexMesh, model);
                }
                catch (Exception ex)
                {
                    Editor.LogWarning(ex);
                    failed = true;
                }
                Scripting.InvokeOnUpdate(() =>
                {
                    Object.Destroy(model);
                    if (failed)
                    {
                        Editor.LogError($"Failed to bake Convex Brush Collider '{(brush ? brush.Name : "?")}' to '{path}'.");
                        completed?.Invoke(false);
                        return;
                    }
                    var asset = Content.LoadAsync<CollisionData>(path);
                    if (asset == null || asset.WaitForLoaded())
                    {
                        Editor.LogError($"Failed to load baked collision '{path}'.");
                        completed?.Invoke(false);
                        return;
                    }
                    if (!brush)
                    {
                        completed?.Invoke(false);
                        return;
                    }
                    using (new UndoBlock(undo, brush, "Bake brush collision"))
                        brush.SetBakedCollision(asset, facesHash);
                    if (brush.Scene)
                        Editor.Instance.Scene.MarkSceneEdited(brush.Scene);
                    Editor.Log($"Baked Convex Brush Collider '{brush.Name}' to '{path}'.");
                    completed?.Invoke(true);
                });
            });
        }

        /// <summary>
        /// Gets the output asset path for the brush.
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <returns>The absolute path of the baked asset file.</returns>
        public static string GetBakePath(ConvexBrushCollider brush)
        {
            var idSuffix = "_" + brush.ID.ToString("N").Substring(0, 8);

            // Reuse file only if it belongs to this brush (duplicated brushes share the reference initially and must not overwrite it)
            var baked = brush.BakedCollision;
            if (baked && !baked.IsVirtual)
            {
                var existing = baked.Path;
                if (!string.IsNullOrEmpty(existing) && Path.GetFileNameWithoutExtension(existing).EndsWith(idSuffix, StringComparison.Ordinal))
                    return existing;
            }

            var name = brush.Name ?? "Brush";
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return Path.Combine(Globals.ProjectContentFolder, BakeFolder, name + idSuffix + ".flax");
        }
    }
}
