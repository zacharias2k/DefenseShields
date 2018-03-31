using System;
using VRageMath;

namespace DefenseShields.Test
{
    /// <summary>
    /// Interface for 1D objects (vector, line, ray, segment)
    /// </summary>
    public interface ILinearObject
    {
        Vector3D Direction { get; }
        bool IsOriented { get; }
    }

    /// <summary>
    /// Interface for 2D objects (plane, circle, ellipse, triangle)
    /// </summary>
    public interface IPlanarObject
    {
        Vector3D Normal { get; }
        bool IsOriented { get; }
    }

    /// <summary>
    /// Interface for finite objects
    /// </summary>
    public interface IFiniteObject
    {
        Box3d BoundingBox(Coord3d coord);
        Box3d MinimumBoundingBox { get; }
        BoundingSphereD BoundingSphere { get; }
    }
}