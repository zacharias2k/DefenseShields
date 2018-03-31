using System;
using static System.Math;

namespace DefenseShields.Support
{
    /// <summary>
    /// Ellipsoid object defined by center point and three mutually orthogonal vectors.
    /// </summary>
    public class Ellipsoid
    {

        private Point3d _point;
        private Vector3d _v1;
        private Vector3d _v2;
        private Vector3d _v3;

        /// <summary>
        /// Initializes ellipsoid instance using center point and three orthogonal vectors.
        /// </summary>
        /// <param name="Center">Center point.</param>
        /// <param name="v1">First semiaxis.</param>
        /// <param name="v2">Second semiaxis.</param>
        /// <param name="v3">Third semiaxis.</param>
        public Ellipsoid(Point3d Center, Vector3d v1, Vector3d v2, Vector3d v3)
        {
            if (!(v1.IsOrthogonalTo(v2) && v1.IsOrthogonalTo(v3) && v3.IsOrthogonalTo(v2)))
            {
                //throw new Exception("Semiaxes are not orthogonal");
            }
            _point = Center.Copy();
            if (v1.Norm >= v2.Norm && v1.Norm >= v3.Norm)
            {
                _v1 = v1.Copy();
                if (v2.Norm >= v3.Norm)
                {
                    _v2 = v2.Copy();
                    _v3 = v3.Copy();
                }
                else
                {
                    _v2 = v3.Copy();
                    _v3 = v2.Copy();
                }
            }
            else if (v2.Norm >= v1.Norm && v2.Norm >= v3.Norm)
            {
                _v1 = v2.Copy();
                if (v1.Norm >= v3.Norm)
                {
                    _v2 = v1.Copy();
                    _v3 = v3.Copy();
                }
                else
                {
                    _v2 = v3.Copy();
                    _v3 = v1.Copy();
                }
            }
            else
            {
                _v1 = v3.Copy();
                if (v1.Norm >= v2.Norm)
                {
                    _v2 = v1.Copy();
                    _v3 = v2.Copy();
                }
                else
                {
                    _v2 = v2.Copy();
                    _v3 = v1.Copy();
                }
            }
        }

        /// <summary>
        /// Creates copy of the object
        /// </summary>
        public Ellipsoid Copy()
        {
            return new Ellipsoid(_point.Copy(), _v1.Copy(), _v2.Copy(), _v3.Copy());
        }

        public Point3d Center
        {
            get { return _point.Copy(); }
        }

        /// <summary>
        /// Major semiaxis
        /// </summary>
        public Vector3d SemiaxisA
        {
            get { return _v1.Copy(); }
        }
        /// <summary>
        /// Intermediate semiaxis
        /// </summary>
        public Vector3d SemiaxisB
        {
            get { return _v2.Copy(); }
        }
        /// <summary>
        /// Minor semiaxis
        /// </summary>
        public Vector3d SemiaxisC
        {
            get { return _v3.Copy(); }
        }

        /// <summary>
        /// Length of the major semiaxis
        /// </summary>
        public double A
        {
            get { return _v1.Norm; }
        }

        /// <summary>
        /// Length of the intermediate semiaxis
        /// </summary>
        public double B
        {
            get { return _v2.Norm; }
        }

        /// <summary>
        /// Length of the minor semiaxis
        /// </summary>
        public double C
        {
            get { return _v3.Norm; }
        }

        /// <summary>
        /// Intersection of ellipsoid with line.
        /// Returns 'null' (no intersection) or object of type 'Point3d' or 'Segment3d'.
        /// </summary>
        public object IntersectionWith(Line3d s)
        {
            // Analytical solution from:
            // https://johannesbuchner.github.io/intersection/intersection_line_ellipsoid.html

            // Define local cordinate system for ellipsoid
            // and present line in parametric form in local coordinate system
            // x: t + x0
            // y: k * t + y0
            // z: l * t + z0
            // For numerical stability choose local X axis such that k<=1 and l<=1 !!!

            Coord3d lc = new Coord3d(_point, _v1, _v2);
            Vector3d v0 = s.Direction.ConvertTo(lc);
            if (Abs(v0.Y) > Abs(v0.X) || Abs(v0.Z) > Abs(v0.X))
            {
                // Bad choice of X axis, try again
                lc = new Coord3d(_point, _v2, _v3);
                v0 = s.Direction.ConvertTo(lc);
                if (Abs(v0.Y) > Abs(v0.X) || Abs(v0.Z) > Abs(v0.X))
                {
                    lc = new Coord3d(_point, _v3, _v1);
                    v0 = s.Direction.ConvertTo(lc);
                }
            }
            // Normalize direction vector
            double k = v0.Y / v0.X;
            double l = v0.Z / v0.X;

            Point3d p0 = s.Point.ConvertTo(lc);
            double x0 = p0.X;
            double y0 = p0.Y;
            double z0 = p0.Z;

            double a2b2 = A * A * B * B;
            double a2c2 = A * A * C * C;
            double b2c2 = B * B * C * C;

            double det = a2b2 * C * C * (a2b2 * l * l + a2c2 * k * k - A * A * k * k * z0 * z0 +
                                         2 * A * A * k * l * y0 * z0 - A * A * l * l * y0 * y0 + b2c2 -
                                         B * B * l * l * x0 * x0 + 2 * B * B * l * x0 * z0 - B * B * z0 * z0 -
                                         C * C * k * k * x0 * x0 + 2 * C * C * k * x0 * y0 - C * C * y0 * y0);

            if (det < -GeometRi3D.Tolerance)
            {
                return null;
            }

            double sum1 = a2b2 * l * z0 + a2c2 * k * y0 + b2c2 * x0;
            double sum2 = a2b2 * l * l + a2c2 * k * k + b2c2;

            if (Abs(det) <= GeometRi3D.Tolerance)
            {
                // Intersection is point
                double t = -sum1 / sum2;
                return new Point3d(t + x0, k * t + y0, l * t + z0, lc);
            }
            else
            {
                double t = -(sum1 + Sqrt(det)) / sum2;
                Point3d p1 = new Point3d(t + x0, k * t + y0, l * t + z0, lc);
                t = -(sum1 - Sqrt(det)) / sum2;
                Point3d p2 = new Point3d(t + x0, k * t + y0, l * t + z0, lc);
                return new Segment3d(p1, p2);
            }
        }
    }
}