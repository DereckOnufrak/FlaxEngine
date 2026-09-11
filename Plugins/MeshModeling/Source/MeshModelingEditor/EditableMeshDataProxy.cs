// Copyright (c) Wojciech Figat. All rights reserved.

using FlaxEditor.Content;
using FlaxEngine;
using MeshModeling;

namespace MeshModelingEditor
{
    /// <summary>
    /// Content proxy for <see cref="EditableMeshData"/> assets (the topology data behind an <see cref="EditableMesh"/> actor).
    /// </summary>
    /// <seealso cref="SpawnableJsonAssetProxy{T}" />
    public class EditableMeshDataProxy : SpawnableJsonAssetProxy<EditableMeshData>
    {
        /// <inheritdoc />
        public override string Name => "Editable Mesh Data";

        /// <inheritdoc />
        public override Color AccentColor => Color.FromRGB(0x00ADA4);
    }
}
