using System;
using static System.Math;

namespace DefenseShields.Support
{
    /// <summary>
    /// Infinite line  in 3D space and defined by any point lying on the line and a direction vector.
    /// </summary>
    public class Line3d : ILinearObject
    {

        private Point3d _point;
        private Vector3d _dir;

        #region "Constructors"
        /// <summary>
        /// Default constructor, initializes line aligned with X-axis in global coordinate system.
        /// </summary>
        public Line3d()
        {
            _point = new Point3d();
            _dir = new Vector3d(1, 0, 0);
        }

        /// <summary>
        /// Initializes line using point and direction.
        /// </summary>
        /// <param name="p">Point on the line.</param>
        /// <param name="v">Direction vector.</param>
        public Line3d(Point3d p, Vector3d v)
        {
            _point = p.Copy();
            _dir = v.Copy();
        }

        /// <summary>
        /// Initializes line using two points.
        /// </summary>
        /// <param name="p1">First point.</param>
        /// <param name="p2">Second point.</param>
        public Line3d(Point3d p1, Point3d p2)
        {
            _point = p1.Copy();
            _dir = new Vector3d(p1, p2);
        }
        #endregion

        /// <summary>
        /// Creates copy of the object
        /// </summary>
        public Line3d Copy()
        {
            return new Line3d(_point, _dir);
        }

        /// <summary>
        /// Base point of the line
        /// </summary>
        public Point3d Point
        {
            get { return _point.Copy(); }
            set { _point = value.Copy(); }
        }

        /// <summary>
        /// Direction vector of the line
        /// </summary>
        public Vector3d Direction
        {
            get { return _dir.Copy(); }
            set { _dir = value.Copy(); }
        }

        public bool IsOriented
        {
            get { return false; }
        }

        #region "ParallelMethods"
        /// <summary>
        /// Check if two objects are parallel
        /// </summary>
        public bool IsParallelTo(ILinearObject obj)
        {
            return this.Direction.IsParallelTo(obj.Direction);
        }

        /// <summary>
        /// Check if two objects are NOT parallel
        /// </summary>
        public bool IsNotParallelTo(ILinearObject obj)
        {
            return this.Direction.IsNotParallelTo(obj.Direction);
        }

        /// <summary>
        /// Check if two objects are orthogonal
        /// </summary>
        public bool IsOrthogonalTo(ILinearObject obj)
        {
            return this.Direction.IsOrthogonalTo(obj.Direction);
        }

        /// <summary>
        /// Check if two objects are parallel
        /// </summary>
        public bool IsParallelTo(IPlanarObject obj)
        {
            return this.Direction.IsOrthogonalTo(obj.Normal);
        }

        /// <summary>
        /// Check if two objects are NOT parallel
        /// </summary>
        public bool IsNotParallelTo(IPlanarObject obj)
        {
            return !this.Direction.IsOrthogonalTo(obj.Normal);
        }

        /// <summary>
        /// Check if two objects are orthogonal
        /// </summary>
        public bool IsOrthogonalTo(IPlanarObject obj)
        {
            return this.Direction.IsParallelTo(obj.Normal);
        }
        #endregion
    }
}


