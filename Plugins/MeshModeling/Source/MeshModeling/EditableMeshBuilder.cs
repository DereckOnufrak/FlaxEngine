// Copyright (c) Wojciech Figat. All rights reserved.

using System;
using System.Collections.Generic;
using FlaxEngine;

namespace MeshModeling
{
    /// <summary>
    /// Geometry algorithms for creating and editing <see cref="EditableMeshData"/>: primitive generation,
    /// triangulation for rendering, and basic modeling operations (extrude, delete, move).
    /// </summary>
    /// <remarks>
    /// Face loops are stored counter-clockwise when viewed from outside the solid (the usual convention). Only
    /// <see cref="Triangulate"/> flips this to the clockwise winding <see cref="Mesh.UpdateMesh"/> expects, so all
    /// the topology-editing code below only ever has to reason about one winding convention.
    /// </remarks>
    public static class EditableMeshBuilder
    {
        /// <summary>
        /// Creates a new box-shaped mesh centered on the origin.
        /// </summary>
        /// <param name="size">The length of each side.</param>
        public static EditableMeshData CreateCube(float size = 100.0f)
        {
            float h = size * 0.5f;
            var positions = new[]
            {
                new Float3(-h, -h, -h), // 0
                new Float3(h, -h, -h),  // 1
                new Float3(h, h, -h),   // 2
                new Float3(-h, h, -h),  // 3
                new Float3(-h, -h, h),  // 4
                new Float3(h, -h, h),   // 5
                new Float3(h, h, h),    // 6
                new Float3(-h, h, h),   // 7
            };
            var faces = new[]
            {
                new[] { 0, 3, 2, 1 }, // -Z (back)
                new[] { 4, 5, 6, 7 }, // +Z (front)
                new[] { 0, 4, 7, 3 }, // -X (left)
                new[] { 1, 2, 6, 5 }, // +X (right)
                new[] { 0, 1, 5, 4 }, // -Y (bottom)
                new[] { 3, 7, 6, 2 }, // +Y (top)
            };
            var data = new EditableMeshData();
            var loops = new List<List<int>>();
            var slots = new List<int>();
            foreach (var face in faces)
            {
                loops.Add(new List<int>(face));
                slots.Add(0);
            }
            data.MaterialSlots.Add(new EditableMeshData.MaterialSlot { Name = "Material" });
            RebuildTopology(data, positions, loops, slots);
            return data;
        }

        /// <summary>
        /// Creates a new single-quad flat plane, centered on the origin, facing up (+Y).
        /// </summary>
        /// <param name="size">The length of each side.</param>
        public static EditableMeshData CreatePlane(float size = 100.0f)
        {
            float h = size * 0.5f;
            var positions = new[]
            {
                new Float3(-h, 0, -h),
                new Float3(-h, 0, h),
                new Float3(h, 0, h),
                new Float3(h, 0, -h),
            };
            var data = new EditableMeshData();
            data.MaterialSlots.Add(new EditableMeshData.MaterialSlot { Name = "Material" });
            RebuildTopology(data, positions, new List<List<int>> { new List<int> { 0, 1, 2, 3 } }, new List<int> { 0 });
            return data;
        }

        /// <summary>
        /// Triangulates the mesh into flat-shaded render data. Each face's vertices are duplicated so faces don't
        /// share normals/UVs across their shared edges (hard-edge/flat shading).
        /// </summary>
        /// <param name="data">The mesh to triangulate.</param>
        /// <param name="positions">The output (duplicated) vertex positions.</param>
        /// <param name="triangles">The output triangle indices (clockwise, 3 per triangle).</param>
        /// <param name="normals">The output per-vertex (flat) normals.</param>
        /// <param name="uv">The output per-vertex UVs.</param>
        /// <param name="triangleFaces">
        /// For each output triangle, the <see cref="EditableMeshData.Faces"/> index it belongs to. Used to map a
        /// raycast hit back to the mesh data it came from (see <see cref="EditableMeshData.GetFaceLoop"/>).
        /// </param>
        public static void Triangulate(EditableMeshData data, out Float3[] positions, out int[] triangles, out Float3[] normals, out Float2[] uv, out int[] triangleFaces)
        {
            var outPositions = new List<Float3>();
            var outTriangles = new List<int>();
            var outNormals = new List<Float3>();
            var outUv = new List<Float2>();
            var outTriangleFaces = new List<int>();

            for (int f = 0; f < data.Faces.Count; f++)
            {
                var loop = data.GetFaceLoop(f);
                if (loop.Count < 3)
                    continue;

                var facePositions = new Float3[loop.Count];
                for (int i = 0; i < loop.Count; i++)
                    facePositions[i] = data.Positions[loop[i]];

                var normal = ComputeFaceNormal(facePositions);
                var baseVertex = outPositions.Count;

                for (int i = 0; i < facePositions.Length; i++)
                {
                    outPositions.Add(facePositions[i]);
                    outNormals.Add(normal);
                    outUv.Add(ProjectUv(facePositions[i], normal));
                }

                // Fan-triangulate the (assumed convex) polygon, in the loop's own order. (A prior version reversed
                // this on the assumption Mesh.UpdateMesh needed clockwise-from-outside triangles; a live build
                // showed that was backwards - the mesh rendered inside-out/flipped regardless of the explicit
                // vertex normal's sign, which only makes sense if winding, not the normal attribute, was driving
                // the visible culling/shading. Natural order fixed it.)
                for (int i = 1; i < facePositions.Length - 1; i++)
                {
                    outTriangles.Add(baseVertex);
                    outTriangles.Add(baseVertex + i);
                    outTriangles.Add(baseVertex + i + 1);
                    outTriangleFaces.Add(f);
                }
            }

            positions = outPositions.ToArray();
            triangles = outTriangles.ToArray();
            normals = outNormals.ToArray();
            uv = outUv.ToArray();
            triangleFaces = outTriangleFaces.ToArray();
        }

        /// <summary>
        /// Convenience overload of <see cref="Triangulate(EditableMeshData,out Float3[],out int[],out Float3[],out Float2[],out int[])"/>
        /// for callers that only need render data (e.g. baking), not the picking-support mapping.
        /// </summary>
        public static void Triangulate(EditableMeshData data, out Float3[] positions, out int[] triangles, out Float3[] normals, out Float2[] uv)
        {
            Triangulate(data, out positions, out triangles, out normals, out uv, out _);
        }

        /// <summary>
        /// Extrudes a face outward along its normal by the given distance, replacing it with a cap face and a ring
        /// of new side faces.
        /// </summary>
        /// <returns>The index of the new cap face (the extruded replacement for <paramref name="faceIndex"/>), or -1 if the face was degenerate.</returns>
        public static int ExtrudeFace(EditableMeshData data, int faceIndex, float distance)
        {
            var loop = data.GetFaceLoop(faceIndex);
            if (loop.Count < 3)
                return -1;

            var facePositions = new Float3[loop.Count];
            for (int i = 0; i < loop.Count; i++)
                facePositions[i] = data.Positions[loop[i]];
            var normal = ComputeFaceNormal(facePositions);
            var offset = normal * distance;

            data.GetAllFaceLoops(out var loops, out var slots);
            int extrudedSlot = data.Faces[faceIndex].MaterialSlot;

            // Remove the original face; it's replaced by the new cap below.
            loops.RemoveAt(faceIndex);
            slots.RemoveAt(faceIndex);

            var positions = new List<Float3>(data.Positions);
            var newIndices = new int[loop.Count];
            for (int i = 0; i < loop.Count; i++)
            {
                newIndices[i] = positions.Count;
                positions.Add(facePositions[i] + offset);
            }

            // New cap face, in the same winding order as the original.
            int capFaceIndex = loops.Count;
            loops.Add(new List<int>(newIndices));
            slots.Add(extrudedSlot);

            // Side faces connecting the old ring to the new ring.
            for (int i = 0; i < loop.Count; i++)
            {
                int next = (i + 1) % loop.Count;
                loops.Add(new List<int> { loop[i], loop[next], newIndices[next], newIndices[i] });
                slots.Add(extrudedSlot);
            }

            RebuildTopology(data, positions.ToArray(), loops, slots);
            return capFaceIndex;
        }

        /// <summary>
        /// Removes a face from the mesh. Any vertices left unused by every remaining face are discarded.
        /// </summary>
        public static void DeleteFace(EditableMeshData data, int faceIndex)
        {
            data.GetAllFaceLoops(out var loops, out var slots);
            loops.RemoveAt(faceIndex);
            slots.RemoveAt(faceIndex);
            RebuildTopology(data, data.Positions.ToArray(), loops, slots);
        }

        /// <summary>
        /// Moves the given vertices by a delta, without changing topology.
        /// </summary>
        public static void MoveVertices(EditableMeshData data, IList<int> vertexIndices, Vector3 delta)
        {
            var d = (Float3)delta;
            for (int i = 0; i < vertexIndices.Count; i++)
            {
                int idx = vertexIndices[i];
                data.Positions[idx] += d;
            }
        }

        /// <summary>
        /// Moves every vertex in the mesh by a delta, without changing topology. Used to re-center the mesh's local
        /// origin (see <see cref="ComputeBoundsCenter"/>) - shifting every vertex by -offset is the geometry half
        /// of that operation; the caller is responsible for compensating the owning actor's position so the mesh
        /// doesn't appear to move in the world.
        /// </summary>
        public static void OffsetAllVertices(EditableMeshData data, Vector3 delta)
        {
            var d = (Float3)delta;
            for (int i = 0; i < data.Positions.Count; i++)
                data.Positions[i] += d;
        }

        /// <summary>
        /// Computes the center of the mesh's local-space bounding box (the midpoint between its min and max
        /// extents on each axis) - the usual choice for "center the origin on the geometry".
        /// </summary>
        public static Float3 ComputeBoundsCenter(EditableMeshData data)
        {
            if (data.Positions.Count == 0)
                return Float3.Zero;
            var min = data.Positions[0];
            var max = min;
            for (int i = 1; i < data.Positions.Count; i++)
            {
                min = Float3.Min(min, data.Positions[i]);
                max = Float3.Max(max, data.Positions[i]);
            }
            return (min + max) * 0.5f;
        }

        /// <summary>
        /// Computes a flat face normal from its (assumed planar, convex) vertex loop. Face loops are stored
        /// counter-clockwise as viewed from outside the solid, so this is the standard right-hand-rule normal.
        /// </summary>
        private static Float3 ComputeFaceNormal(Float3[] facePositions)
        {
            var v0 = facePositions[0];
            var v1 = facePositions[1];
            var v2 = facePositions[2];
            // Standard right-hand-rule normal for the loop's own (CCW-from-outside) winding - see the note on the
            // fan-triangulation below for why this, not the winding, turned out not to matter for the flipped-look
            // bug: the default material's shading/culling is apparently driven by winding, not this explicit
            // vertex normal. Kept mathematically consistent with the loop anyway, since other materials may use it.
            var normal = Float3.Cross(v1 - v0, v2 - v0);
            float len = normal.Length;
            return len > 1e-8f ? normal / len : Float3.UnitZ;
        }

        /// <summary>
        /// Basic planar/box UV projection: picks the axis the face normal is most aligned with, and projects the
        /// other two position components as UV coordinates (scaled so 100 world units map to one UV tile).
        /// </summary>
        private static Float2 ProjectUv(Float3 position, Float3 normal)
        {
            const float scale = 1.0f / 100.0f;
            float ax = Mathf.Abs(normal.X), ay = Mathf.Abs(normal.Y), az = Mathf.Abs(normal.Z);
            if (ax >= ay && ax >= az)
                return new Float2(position.Y, position.Z) * scale;
            if (ay >= ax && ay >= az)
                return new Float2(position.X, position.Z) * scale;
            return new Float2(position.X, position.Y) * scale;
        }

        /// <summary>
        /// Rebuilds <see cref="EditableMeshData.HalfEdges"/> and <see cref="EditableMeshData.Faces"/> (and compacts
        /// <see cref="EditableMeshData.Positions"/>) from a flat list of face loops. This is the single place that
        /// constructs/mutates the half-edge structure, so every editing operation stays correct-by-construction
        /// instead of hand-patching Next/Prev/Twin pointers in place.
        /// </summary>
        private static void RebuildTopology(EditableMeshData data, Float3[] positions, List<List<int>> faceLoops, List<int> faceMaterialSlots)
        {
            // Compact: keep only vertices referenced by a surviving face loop.
            var used = new bool[positions.Length];
            foreach (var loop in faceLoops)
                foreach (var idx in loop)
                    used[idx] = true;
            var remap = new int[positions.Length];
            var newPositions = new List<Float3>();
            for (int i = 0; i < positions.Length; i++)
            {
                if (used[i])
                {
                    remap[i] = newPositions.Count;
                    newPositions.Add(positions[i]);
                }
                else
                {
                    remap[i] = -1;
                }
            }

            var halfEdges = new List<EditableMeshData.HalfEdge>();
            var faces = new List<EditableMeshData.Face>();
            var edgeLookup = new Dictionary<(int, int), int>();

            for (int f = 0; f < faceLoops.Count; f++)
            {
                var loop = faceLoops[f];
                int n = loop.Count;
                int firstHalfEdge = halfEdges.Count;
                for (int i = 0; i < n; i++)
                {
                    var he = new EditableMeshData.HalfEdge
                    {
                        Origin = remap[loop[i]],
                        Face = f,
                        Twin = -1,
                        Next = firstHalfEdge + (i + 1) % n,
                        Prev = firstHalfEdge + (i - 1 + n) % n,
                    };
                    edgeLookup[(remap[loop[i]], remap[loop[(i + 1) % n]])] = halfEdges.Count;
                    halfEdges.Add(he);
                }
                faces.Add(new EditableMeshData.Face { FirstHalfEdge = firstHalfEdge, MaterialSlot = faceMaterialSlots[f] });
            }

            // Link twins: the half-edge from a->b is the twin of the half-edge from b->a, if one exists.
            for (int i = 0; i < halfEdges.Count; i++)
            {
                var he = halfEdges[i];
                if (he.Twin != -1)
                    continue;
                int dest = halfEdges[he.Next].Origin;
                if (edgeLookup.TryGetValue((dest, he.Origin), out int twin))
                {
                    he.Twin = twin;
                    halfEdges[i] = he;
                    var twinHe = halfEdges[twin];
                    twinHe.Twin = i;
                    halfEdges[twin] = twinHe;
                }
            }

            data.Positions = newPositions;
            data.HalfEdges = halfEdges;
            data.Faces = faces;
        }
    }
}
