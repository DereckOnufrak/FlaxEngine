// Copyright (c) Wojciech Figat. All rights reserved.

#if FLAX_TESTS
using NUnit.Framework;

namespace FlaxEngine.Tests
{
    /// <summary>
    /// Tests for <see cref="EditableMeshBuilder"/> and <see cref="EditableMeshData"/>.
    /// </summary>
    [TestFixture]
    public class TestEditableMeshBuilder
    {
        private static void AssertClosedManifold(EditableMeshData data)
        {
            foreach (var he in data.HalfEdges)
                Assert.GreaterOrEqual(he.Twin, 0, "Expected a fully closed manifold (every edge should have a twin).");
        }

        [Test]
        public void TestCreateCube()
        {
            var cube = EditableMeshBuilder.CreateCube(100.0f);
            Assert.AreEqual(8, cube.Positions.Count);
            Assert.AreEqual(6, cube.Faces.Count);
            Assert.AreEqual(24, cube.HalfEdges.Count);
            for (int i = 0; i < cube.Faces.Count; i++)
                Assert.AreEqual(4, cube.GetFaceLoop(i).Count);
            AssertClosedManifold(cube);
        }

        [Test]
        public void TestCreatePlane()
        {
            var plane = EditableMeshBuilder.CreatePlane(100.0f);
            Assert.AreEqual(4, plane.Positions.Count);
            Assert.AreEqual(1, plane.Faces.Count);
            Assert.AreEqual(4, plane.HalfEdges.Count);
            // A single quad has no neighboring face, so every edge is a boundary edge.
            foreach (var he in plane.HalfEdges)
                Assert.AreEqual(-1, he.Twin);
        }

        [Test]
        public void TestTriangulateCube()
        {
            var cube = EditableMeshBuilder.CreateCube(100.0f);
            EditableMeshBuilder.Triangulate(cube, out var positions, out var triangles, out var normals, out var uv);

            // Each of the 6 quad faces is duplicated into 4 unique (flat-shaded) vertices and 2 triangles.
            Assert.AreEqual(24, positions.Length);
            Assert.AreEqual(24, normals.Length);
            Assert.AreEqual(24, uv.Length);
            Assert.AreEqual(36, triangles.Length);

            // Every normal should be unit length and axis-aligned (cube faces are axis-aligned planes).
            foreach (var n in normals)
            {
                Assert.AreEqual(1.0f, n.Length, 0.001f);
                float ax = Mathf.Abs(n.X), ay = Mathf.Abs(n.Y), az = Mathf.Abs(n.Z);
                int axisCount = (ax > 0.99f ? 1 : 0) + (ay > 0.99f ? 1 : 0) + (az > 0.99f ? 1 : 0);
                Assert.AreEqual(1, axisCount, "Cube face normals should be axis-aligned.");
            }

            // All 6 distinct axis directions should be represented exactly once (one face per cube side).
            var seen = new System.Collections.Generic.HashSet<Float3>();
            for (int f = 0; f < 6; f++)
                seen.Add(normals[f * 4]);
            Assert.AreEqual(6, seen.Count);
        }

        [Test]
        public void TestExtrudeFaceAddsGeometry()
        {
            var cube = EditableMeshBuilder.CreateCube(100.0f);
            int facesBefore = cube.Faces.Count;
            int vertsBefore = cube.Positions.Count;
            var loopBefore = cube.GetFaceLoop(0);
            int loopLength = loopBefore.Count;

            EditableMeshBuilder.ExtrudeFace(cube, 0, 50.0f);

            // Removed 1 face, added 1 cap + N sides => net +N faces. Added N new vertices for the cap ring.
            Assert.AreEqual(facesBefore + loopLength, cube.Faces.Count);
            Assert.AreEqual(vertsBefore + loopLength, cube.Positions.Count);
            AssertClosedManifold(cube);

            // The result should still triangulate without error.
            EditableMeshBuilder.Triangulate(cube, out var positions, out var triangles, out _, out _);
            Assert.Greater(positions.Length, 0);
            Assert.AreEqual(0, triangles.Length % 3);
        }

        [Test]
        public void TestDeleteFaceRemovesUnusedVertices()
        {
            var plane = EditableMeshBuilder.CreatePlane(100.0f);
            EditableMeshBuilder.DeleteFace(plane, 0);
            Assert.AreEqual(0, plane.Faces.Count);
            Assert.AreEqual(0, plane.HalfEdges.Count);
            Assert.AreEqual(0, plane.Positions.Count, "Vertices only used by the deleted face should be discarded.");
        }

        [Test]
        public void TestMoveVertices()
        {
            var plane = EditableMeshBuilder.CreatePlane(100.0f);
            var original = new System.Collections.Generic.List<Float3>(plane.Positions);
            var delta = new Vector3(1, 2, 3);

            EditableMeshBuilder.MoveVertices(plane, new[] { 0, 2 }, delta);

            Assert.AreEqual(original[0] + (Float3)delta, plane.Positions[0]);
            Assert.AreEqual(original[1], plane.Positions[1]);
            Assert.AreEqual(original[2] + (Float3)delta, plane.Positions[2]);
            Assert.AreEqual(original[3], plane.Positions[3]);
        }
    }
}
#endif
