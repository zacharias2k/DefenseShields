using System;
using VRageMath;

namespace DefenseShields.Support
{
    /// <summary>
    /// Ellipsoid object defined by center point and three mutually orthogonal vectors.
    /// </summary>
    public class Ellipsoid
    {

        private Vector3D _point;
        private Vector3D _v1;
        private Vector3D _v2;
        private Vector3D _v3;

        /// <summary>
        /// Initializes ellipsoid instance using center point and three orthogonal vectors.
        /// </summary>
        /// <param name="Center">Center point.</param>
        /// <param name="v1">First semiaxis.</param>
        /// <param name="v2">Second semiaxis.</param>
        /// <param name="v3">Third semiaxis.</param>
        public Ellipsoid(Vector3D Center, Vector3D v1, Vector3D v2, Vector3D v3)
        {
            const float eps1 = MathHelper.EPSILON;
            //if (!(v1.IsOrthogonalTo(v2) && v1.IsOrthogonalTo(v3) && v3.IsOrthogonalTo(v2)))
            if (!(Math.Abs(Vector3D.Dot(v1, v2)) < eps1 && Math.Abs(Vector3D.Dot(v1, v3)) < eps1 && Math.Abs(Vector3D.Dot(v3, v2)) < eps1))

            {
                //throw new Exception("Semiaxes are not orthogonal");
            }
            _point = Center;
            if (v1.Length() >= v2.Length() && v1.Length() >= v3.Length())
            {
                _v1 = v1;
                if (v2.Length() >= v3.Length())
                {
                    _v2 = v2;
                    _v3 = v3;
                }
                else
                {
                    _v2 = v3;
                    _v3 = v2;
                }
            }
            else if (v2.Length() >= v1.Length() && v2.Length() >= v3.Length())
            {
                _v1 = v2;
                if (v1.Length() >= v3.Length())
                {
                    _v2 = v1;
                    _v3 = v3;
                }
                else
                {
                    _v2 = v3;
                    _v3 = v1;
                }
            }
            else
            {
                _v1 = v3;
                if (v1.Length() >= v2.Length())
                {
                    _v2 = v1;
                    _v3 = v2;
                }
                else
                {
                    _v2 = v2;
                    _v3 = v1;
                }
            }
        }

        /// <summary>
        /// Creates copy of the object
        /// </summary>
        public Ellipsoid Copy()
        {
            return new Ellipsoid(_point, _v1, _v2, _v3);
        }

        public Vector3D Center
        {
            get { return _point; }
        }

        /// <summary>
        /// Major semiaxis
        /// </summary>
        public Vector3D SemiaxisA
        {
            get { return _v1; }
        }
        /// <summary>
        /// Intermediate semiaxis
        /// </summary>
        public Vector3D SemiaxisB
        {
            get { return _v2; }
        }
        /// <summary>
        /// Minor semiaxis
        /// </summary>
        public Vector3D SemiaxisC
        {
            get { return _v3; }
        }

        /// <summary>
        /// Length of the major semiaxis
        /// </summary>
        public double A
        {
            get { return _v1.Length(); }
        }

        /// <summary>
        /// Length of the intermediate semiaxis
        /// </summary>
        public double B
        {
            get { return _v2.Length(); }
        }

        /// <summary>
        /// Length of the minor semiaxis
        /// </summary>
        public double C
        {
            get { return _v3.Length(); }
        }

        /// <summary>
        /// Intersection of ellipsoid with line.
        /// Returns 'null' (no intersection) or object of type 'Point3d' or 'Segment3d'.
        /// </summary>
        public object IntersectionWith(LineD s)
        {
            // Analytical solution from:
            // https://johannesbuchner.github.io/intersection/intersection_line_ellipsoid.html

            // Define local cordinate system for ellipsoid
            // and present line in parametric form in local coordinate system
            // x: t + x0
            // y: k * t + y0
            // z: l * t + z0
            // For numerical stability choose local X axis such that k<=1 and l<=1 !!!
            var lc = MatrixD.CreateWorld(_point, _v1, _v2);
           // MatrixD lc = new MatrixD(_point, _v1, _v2);
            Vector3D v0 = s.Direction.ConvertTo(lc);
            if (Math.Abs(v0.Y) > Math.Abs(v0.X) || Math.Abs(v0.Z) > Math.Abs(v0.X))
            {
                // Bad choice of X axis, try again
                lc = MatrixD.CreateWorld(_point, _v2, _v3);
                v0 = s.Direction.ConvertTo(lc);
                if (Math.Abs(v0.Y) > Math.Abs(v0.X) || Math.Abs(v0.Z) > Math.Abs(v0.X))
                {
                    lc = MatrixD.CreateWorld(_point, _v3, _v1);
                    v0 = s.Direction.ConvertTo(lc);
                }
            }
            // Normalize direction vector
            double k = v0.Y / v0.X;
            double l = v0.Z / v0.X;

            Vector3D p0 = s.Point.ConvertTo(lc);
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

            if (det < -1E-12)
            {
                return null;
            }

            double sum1 = a2b2 * l * z0 + a2c2 * k * y0 + b2c2 * x0;
            double sum2 = a2b2 * l * l + a2c2 * k * k + b2c2;

            if (Math.Abs(det) <= 1E-12)
            {
                // Intersection is point
                double t = -sum1 / sum2;
                return new Vector3D(t + x0, k * t + y0, l * t + z0, lc);
            }
            else
            {
                double t = -(sum1 + Math.Sqrt(det)) / sum2;
                Vector3D p1 = new Vector3D(t + x0, k * t + y0, l * t + z0, lc);
                t = -(sum1 - Math.Sqrt(det)) / sum2;
                Vector3D p2 = new Vector3D(t + x0, k * t + y0, l * t + z0, lc);
                return new Segment3d(p1, p2);
            }
        }

        public class Segment3d
        {

            private Vector3D _p1;
            private Vector3D _p2;

            /// <summary>
            /// Initializes line segment using two points.
            /// </summary>
            public Segment3d(Vector3D p1, Vector3D p2)
            {
                _p1 = p1;
                _p2 = p2.ConvertTo(p1.Coord);
            }

            public Vector3D P1
            {
                get { return _p1; }
                set { _p1 = value; }
            }

            public Vector3D P2
            {
                get { return _p2; }
                set { _p2 = value; }
            }

            public LineD ToLine
            {
                get { return new LineD(_p1, _p2); }
            }

            public bool IsOriented
            {
                get { return false; }
            }
        }
    }
}