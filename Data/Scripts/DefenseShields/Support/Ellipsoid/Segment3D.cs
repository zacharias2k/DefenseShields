using System;
using static System.Math;

namespace DefenseShields.Support
{
    /// <summary>
    /// Line segment in 3D space defined by two end points.
    /// </summary>
    public class Segment3d //: ILinearObject, IFiniteObject
    {

        private Point3d _p1;
        private Point3d _p2;

        /// <summary>
        /// Initializes line segment using two points.
        /// </summary>
        public Segment3d(Point3d p1, Point3d p2)
        {
            _p1 = p1.Copy();
            _p2 = p2.ConvertTo(p1.Coord);
        }

        /// <summary>
        /// Creates copy of the object
        /// </summary>
        public Segment3d Copy()
        {
            return new Segment3d(_p1, _p2);
        }

        public Point3d P1
        {
            get { return _p1.Copy(); }
            set { _p1 = value.Copy(); }
        }

        public Point3d P2
        {
            get { return _p2.Copy(); }
            set { _p2 = value.Copy(); }
        }
        public Vector3d ToVector
        {
            get { return new Vector3d(_p1, _p2); }
        }
        /*

        public double Length
        {
            get { return _p1.DistanceTo(_p2); }
        }

        public Ray3d ToRay
        {
            get { return new Ray3d(_p1, new Vector3d(_p1, _p2)); }
        }
        */
        public Line3d ToLine
        {
            get { return new Line3d(_p1, _p2); }
        }

        /// <summary>
        /// Direction vector of the segment
        /// </summary>
        /// <returns></returns>
        public Vector3d Direction
        {
            get { return this.ToVector; }
        }

        public bool IsOriented
        {
            get { return false; }
        }
    }
}

