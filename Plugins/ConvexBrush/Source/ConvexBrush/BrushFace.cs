using System;
using FlaxEngine;

namespace ConvexBrush
{
    /// <summary>
    /// A single face (half-space plane) of a <see cref="ConvexBrushCollider"/>. The brush volume is the intersection of all faces.
    /// </summary>
    [Serializable]
    public struct BrushFace : IEquatable<BrushFace>
    {
        /// <summary>
        /// The outward face normal (in actor local space). Doesn't need to be normalized.
        /// </summary>
        [EditorOrder(0), Tooltip("The outward face normal (in actor local space). Doesn't need to be normalized.")]
        public Float3 Normal;

        /// <summary>
        /// The face plane distance from the actor origin, measured along the normalized <see cref="Normal"/> (in actor local space units).
        /// </summary>
        [EditorOrder(10), Tooltip("The face plane distance from the actor origin, measured along the normalized normal (in actor local space units).")]
        public float Distance;

        /// <summary>
        /// Initializes a new instance of the <see cref="BrushFace"/> struct.
        /// </summary>
        /// <param name="normal">The outward face normal.</param>
        /// <param name="distance">The plane distance from the origin along the normal.</param>
        public BrushFace(Float3 normal, float distance)
        {
            Normal = normal;
            Distance = distance;
        }

        /// <summary>
        /// Gets a value indicating whether the face normal is valid (non-zero).
        /// </summary>
        public bool IsValid => Normal.LengthSquared > 1e-12f;

        /// <inheritdoc />
        public bool Equals(BrushFace other)
        {
            return Normal == other.Normal && Distance == other.Distance;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is BrushFace other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return HashCode.Combine(Normal, Distance);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"Normal: {Normal}, Distance: {Distance}";
        }
    }
}
