using System;
using System.Collections.Generic;
using FlaxEngine;

namespace ConvexBrush
{
    /// <summary>
    /// Convex polyhedron built from a set of <see cref="BrushFace"/> planes (intersection of half-spaces). All data is in actor local space.
    /// </summary>
    public sealed class ConvexBrushGeometry
    {
        private static readonly int[] EmptyPolygon = Array.Empty<int>();

        /// <summary>
        /// The unique hull vertices.
        /// </summary>
        public readonly Float3[] Vertices;

        /// <summary>
        /// The ordered polygon (indices into <see cref="Vertices"/>) per input face. Empty for faces that don't touch the hull (redundant, degenerate or invalid).
        /// </summary>
        public readonly int[][] Polygons;

        /// <summary>
        /// The triangle list (indices into <see cref="Vertices"/>) of the whole hull.
        /// </summary>
        public readonly int[] Triangles;

        /// <summary>
        /// The normalized face normals (zero for invalid faces).
        /// </summary>
        public readonly Float3[] Normals;

        /// <summary>
        /// The face plane distances (matching <see cref="Normals"/>).
        /// </summary>
        public readonly float[] Distances;

        /// <summary>
        /// The tolerance used to build the geometry.
        /// </summary>
        public readonly float Epsilon;

        /// <summary>
        /// True if the faces form a closed, non-degenerate convex volume.
        /// </summary>
        public readonly bool IsValid;

        private ConvexBrushGeometry(Float3[] vertices, int[][] polygons, int[] triangles, Float3[] normals, float[] distances, float epsilon, bool isValid)
        {
            Vertices = vertices;
            Polygons = polygons;
            Triangles = triangles;
            Normals = normals;
            Distances = distances;
            Epsilon = epsilon;
            IsValid = isValid;
        }

        /// <summary>
        /// Builds the convex geometry from the given faces.
        /// </summary>
        /// <param name="faces">The brush faces.</param>
        /// <returns>The geometry (check <see cref="IsValid"/>).</returns>
        public static ConvexBrushGeometry Build(BrushFace[] faces)
        {
            faces ??= Array.Empty<BrushFace>();
            int count = faces.Length;
            var normals = new Float3[count];
            var distances = new float[count];
            var valid = new bool[count];
            float maxDistance = 0.0f;
            for (int i = 0; i < count; i++)
            {
                var face = faces[i];
                if (!face.IsValid || float.IsNaN(face.Distance) || float.IsInfinity(face.Distance))
                    continue;
                valid[i] = true;
                normals[i] = face.Normal.Normalized;
                distances[i] = face.Distance;
                maxDistance = Math.Max(maxDistance, Math.Abs(face.Distance));
            }
            float epsilon = Math.Max(1e-4f, maxDistance * 1e-4f);

            // Find hull vertices as the intersections of every 3 planes that lie inside all other planes
            var vertices = new List<Float3>();
            for (int i = 0; i < count; i++)
            {
                if (!valid[i])
                    continue;
                for (int j = i + 1; j < count; j++)
                {
                    if (!valid[j])
                        continue;
                    var cij = Float3.Cross(normals[i], normals[j]);
                    if (cij.LengthSquared < 1e-10f)
                        continue;
                    for (int k = j + 1; k < count; k++)
                    {
                        if (!valid[k])
                            continue;
                        var cjk = Float3.Cross(normals[j], normals[k]);
                        float det = Float3.Dot(normals[i], cjk);
                        if (Math.Abs(det) < 1e-6f)
                            continue;
                        var cki = Float3.Cross(normals[k], normals[i]);
                        var p = (cjk * distances[i] + cki * distances[j] + cij * distances[k]) / det;
                        if (IsInside(normals, distances, valid, p, epsilon) && !Contains(vertices, p, epsilon))
                            vertices.Add(p);
                    }
                }
            }

            // Build ordered polygon for every face
            var polygons = new int[count][];
            var triangles = new List<int>();
            int usedFaces = 0, edgesTwice = 0;
            var polygon = new List<int>();
            var angles = new List<float>();
            for (int f = 0; f < count; f++)
            {
                polygons[f] = EmptyPolygon;
                if (!valid[f])
                    continue;
                var n = normals[f];
                polygon.Clear();
                var center = Float3.Zero;
                for (int v = 0; v < vertices.Count; v++)
                {
                    if (Math.Abs(Float3.Dot(n, vertices[v]) - distances[f]) <= epsilon)
                    {
                        polygon.Add(v);
                        center += vertices[v];
                    }
                }
                if (polygon.Count < 3)
                    continue;
                center /= polygon.Count;

                // Sort around the face center (counter-clockwise around the normal)
                var u = Float3.Cross(n, Math.Abs(n.Y) < 0.99f ? Float3.Up : Float3.Right).Normalized;
                var w = Float3.Cross(n, u);
                angles.Clear();
                for (int v = 0; v < polygon.Count; v++)
                {
                    var d = vertices[polygon[v]] - center;
                    angles.Add((float)Math.Atan2(Float3.Dot(d, w), Float3.Dot(d, u)));
                }
                var sorted = polygon.ToArray();
                Array.Sort(angles.ToArray(), sorted);
                polygons[f] = sorted;

                // Skip coplanar duplicates (eg. face duplicated by user before rotating it into a bevel)
                bool duplicate = false;
                for (int g = 0; g < f && !duplicate; g++)
                    duplicate = polygons[g].Length != 0 && Float3.Dot(normals[g], n) > 0.99999f && Math.Abs(distances[g] - distances[f]) <= epsilon;
                if (duplicate)
                    continue;
                usedFaces++;
                edgesTwice += sorted.Length;

                // Triangle fan
                for (int t = 1; t < sorted.Length - 1; t++)
                {
                    triangles.Add(sorted[0]);
                    triangles.Add(sorted[t]);
                    triangles.Add(sorted[t + 1]);
                }
            }

            // Closed convex polyhedron must satisfy Euler formula: V - E + F = 2
            bool isValid = vertices.Count >= 4 && usedFaces >= 4 && edgesTwice % 2 == 0 && vertices.Count - edgesTwice / 2 + usedFaces == 2;
            return new ConvexBrushGeometry(vertices.ToArray(), polygons, triangles.ToArray(), normals, distances, epsilon, isValid);
        }

        /// <summary>
        /// Gets the center point of the face polygon. For faces that don't touch the hull it returns the plane point closest to the origin.
        /// </summary>
        /// <param name="faceIndex">The face index.</param>
        /// <returns>The face center (local space).</returns>
        public Float3 GetFaceCenter(int faceIndex)
        {
            var polygon = Polygons[faceIndex];
            if (polygon.Length == 0)
                return Normals[faceIndex] * Distances[faceIndex];
            var center = Float3.Zero;
            for (int i = 0; i < polygon.Length; i++)
                center += Vertices[polygon[i]];
            return center / polygon.Length;
        }

        /// <summary>
        /// Gets the face triangles as a flat list of vertices (3 per triangle).
        /// </summary>
        /// <param name="faceIndex">The face index.</param>
        /// <param name="doubleSided">True if emit both triangle windings.</param>
        /// <returns>The triangle vertices (local space).</returns>
        public Float3[] GetFaceTriangles(int faceIndex, bool doubleSided = false)
        {
            var polygon = Polygons[faceIndex];
            if (polygon.Length < 3)
                return Array.Empty<Float3>();
            int triangleCount = polygon.Length - 2;
            var result = new Float3[triangleCount * 3 * (doubleSided ? 2 : 1)];
            int e = 0;
            for (int t = 1; t < polygon.Length - 1; t++)
            {
                result[e++] = Vertices[polygon[0]];
                result[e++] = Vertices[polygon[t]];
                result[e++] = Vertices[polygon[t + 1]];
                if (doubleSided)
                {
                    result[e++] = Vertices[polygon[0]];
                    result[e++] = Vertices[polygon[t + 1]];
                    result[e++] = Vertices[polygon[t]];
                }
            }
            return result;
        }

        /// <summary>
        /// Gets the face outline as a list of line segments (2 vertices per segment).
        /// </summary>
        /// <param name="faceIndex">The face index.</param>
        /// <returns>The line vertices (local space).</returns>
        public Float3[] GetFaceLines(int faceIndex)
        {
            var polygon = Polygons[faceIndex];
            if (polygon.Length < 2)
                return Array.Empty<Float3>();
            var result = new Float3[polygon.Length * 2];
            for (int i = 0; i < polygon.Length; i++)
            {
                result[i * 2] = Vertices[polygon[i]];
                result[i * 2 + 1] = Vertices[polygon[(i + 1) % polygon.Length]];
            }
            return result;
        }

        /// <summary>
        /// Intersects the local-space ray with the given face polygon.
        /// </summary>
        /// <param name="faceIndex">The face index.</param>
        /// <param name="origin">The ray origin (local space).</param>
        /// <param name="direction">The ray direction (local space, doesn't need to be normalized).</param>
        /// <param name="hitPoint">The hit point (local space).</param>
        /// <returns>True if ray hits the face, otherwise false.</returns>
        public bool IntersectsFace(int faceIndex, Float3 origin, Float3 direction, out Float3 hitPoint)
        {
            hitPoint = Float3.Zero;
            if (Polygons[faceIndex].Length < 3)
                return false;
            var n = Normals[faceIndex];
            float denom = Float3.Dot(n, direction);
            if (Math.Abs(denom) < 1e-8f)
                return false;
            float t = (Distances[faceIndex] - Float3.Dot(n, origin)) / denom;
            if (t < 0.0f)
                return false;
            var p = origin + direction * t;
            for (int i = 0; i < Normals.Length; i++)
            {
                if (i != faceIndex && Polygons[i].Length != 0 && Float3.Dot(Normals[i], p) - Distances[i] > Epsilon)
                    return false;
            }
            hitPoint = p;
            return true;
        }

        private static bool IsInside(Float3[] normals, float[] distances, bool[] valid, Float3 p, float epsilon)
        {
            for (int m = 0; m < normals.Length; m++)
            {
                if (valid[m] && Float3.Dot(normals[m], p) - distances[m] > epsilon)
                    return false;
            }
            return true;
        }

        private static bool Contains(List<Float3> vertices, Float3 p, float epsilon)
        {
            float epsilonSq = epsilon * epsilon;
            for (int i = 0; i < vertices.Count; i++)
            {
                if ((vertices[i] - p).LengthSquared <= epsilonSq)
                    return true;
            }
            return false;
        }
    }
}
