// Copyright (c) Wojciech Figat. All rights reserved.

using System.Collections.Generic;
using FlaxEngine;

namespace MeshModeling
{
    /// <summary>
    /// Stores the editable topology (vertices, half-edges, faces) of a <see cref="EditableMesh"/>. Meant to be
    /// stored as the instance data of a <see cref="JsonAsset"/> so it can be edited in the editor and shared/reused
    /// between scenes.
    /// </summary>
    public class EditableMeshData
    {
        /// <summary>
        /// A single directed half-edge. Half-edges are stored by index (not by object reference) so the whole
        /// structure serializes cleanly as plain data.
        /// </summary>
        public struct HalfEdge
        {
            /// <summary>
            /// The index (into <see cref="Positions"/>) of the vertex this half-edge starts at.
            /// </summary>
            public int Origin;

            /// <summary>
            /// The index (into <see cref="HalfEdges"/>) of the opposite half-edge on the neighboring face, or -1 if
            /// this edge has no neighbor (boundary edge).
            /// </summary>
            public int Twin;

            /// <summary>
            /// The index (into <see cref="HalfEdges"/>) of the next half-edge around the same face.
            /// </summary>
            public int Next;

            /// <summary>
            /// The index (into <see cref="HalfEdges"/>) of the previous half-edge around the same face.
            /// </summary>
            public int Prev;

            /// <summary>
            /// The index (into <see cref="Faces"/>) of the face this half-edge belongs to.
            /// </summary>
            public int Face;
        }

        /// <summary>
        /// A single (possibly non-triangular) polygon face, defined by the loop of half-edges reachable by
        /// following <see cref="HalfEdge.Next"/> starting at <see cref="FirstHalfEdge"/>.
        /// </summary>
        public struct Face
        {
            /// <summary>
            /// The index (into <see cref="HalfEdges"/>) of one half-edge belonging to this face's loop.
            /// </summary>
            public int FirstHalfEdge;

            /// <summary>
            /// The index into <see cref="MaterialSlots"/> to use when rendering this face.
            /// </summary>
            public int MaterialSlot;
        }

        /// <summary>
        /// A material slot, mirroring <see cref="Model"/>'s material slots.
        /// </summary>
        public struct MaterialSlot
        {
            /// <summary>
            /// The slot name.
            /// </summary>
            public string Name;

            /// <summary>
            /// The default material to use for faces assigned to this slot.
            /// </summary>
            public MaterialBase Material;
        }

        /// <summary>
        /// The vertex positions, in local-space.
        /// </summary>
        public List<Float3> Positions = new List<Float3>();

        /// <summary>
        /// All half-edges in the mesh.
        /// </summary>
        public List<HalfEdge> HalfEdges = new List<HalfEdge>();

        /// <summary>
        /// All faces in the mesh.
        /// </summary>
        public List<Face> Faces = new List<Face>();

        /// <summary>
        /// The material slots available for faces to use.
        /// </summary>
        public List<MaterialSlot> MaterialSlots = new List<MaterialSlot>();

        /// <summary>
        /// Gets the ordered loop of vertex indices making up the given face, by walking half-edges starting at
        /// <see cref="Face.FirstHalfEdge"/> until looping back around.
        /// </summary>
        /// <param name="faceIndex">The index into <see cref="Faces"/>.</param>
        /// <returns>The ordered vertex indices forming this face's outline.</returns>
        public List<int> GetFaceLoop(int faceIndex)
        {
            var result = new List<int>();
            int start = Faces[faceIndex].FirstHalfEdge;
            int e = start;
            do
            {
                result.Add(HalfEdges[e].Origin);
                e = HalfEdges[e].Next;
            } while (e != start && result.Count <= HalfEdges.Count);
            return result;
        }

        /// <summary>
        /// Gets the loops of vertex indices for every face, plus the material slot used by each face.
        /// </summary>
        public void GetAllFaceLoops(out List<List<int>> loops, out List<int> materialSlots)
        {
            loops = new List<List<int>>(Faces.Count);
            materialSlots = new List<int>(Faces.Count);
            for (int i = 0; i < Faces.Count; i++)
            {
                loops.Add(GetFaceLoop(i));
                materialSlots.Add(Faces[i].MaterialSlot);
            }
        }

        /// <summary>
        /// Creates a deep copy of this mesh data (used to snapshot state for undo).
        /// </summary>
        public EditableMeshData Clone()
        {
            return new EditableMeshData
            {
                Positions = new List<Float3>(Positions),
                HalfEdges = new List<HalfEdge>(HalfEdges),
                Faces = new List<Face>(Faces),
                MaterialSlots = new List<MaterialSlot>(MaterialSlots),
            };
        }
    }
}
