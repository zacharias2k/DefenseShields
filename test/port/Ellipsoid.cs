using System;
using OpenTK;
using static System.Math;
using VRageMath;

namespace DefenseShields.Test
{
    /// <summary>
    /// Ellipsoid object defined by center point and three mutually orthogonal vectors.
    /// </summary>
    public class Ellipsoid : IFiniteObject
    {

        private Point _point;
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
        public Ellipsoid(Point Center, Vector3D v1, Vector3D v2, Vector3D v3)
        {
            if (!(v1.IsOrthogonalTo(v2) && v1.IsOrthogonalTo(v3) && v3.IsOrthogonalTo(v2)))
            {
                throw new Exception("Semiaxes are not orthogonal");
            }
            _point = Center;
            if (v1.Normalize() >= v2.Normalize() && v1.Normalize() >= v3.Normalize())
            {
                _v1 = v1;
                if (v2.Normalize() >= v3.Normalize())
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
            else if (v2.Normalize() >= v1.Normalize() && v2.Normalize() >= v3.Normalize())
            {
                _v1 = v2;
                if (v1.Normalize() >= v3.Normalize())
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
                if (v1.Normalize() >= v2.Normalize())
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

        #region "Properties"
        public Point Center
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
            get { return _v1.Normalize(); }
        }

        /// <summary>
        /// Length of the intermediate semiaxis
        /// </summary>
        public double B
        {
            get { return _v2.Normalize(); }
        }

        /// <summary>
        /// Length of the minor semiaxis
        /// </summary>
        public double C
        {
            get { return _v3.Normalize(); }
        }

        /// <summary>
        /// Volume of the ellipsoid
        /// </summary>
        public double Volume
        {
            get { return 4.0 / 3.0 * PI * A * B * C; }
        }

        /// <summary>
        /// Approximate surface area of the ellipsoid (accurate up to 1.061%).
        /// </summary>
        public double Area
        {
            get
            {
                double p = 1.6075;
                double tmp = Pow(A * B, p) + Pow(A * C, p) + Pow(C * B, p);
                return 4.0 * PI * Pow(tmp, 1 / p);
            }
        }
        #endregion

        #region "BoundingBox"
        /// <summary>
        /// Return minimum bounding box.
        /// </summary>
        public BoundingBoxD MinimumBoundingBox
        {
            get
            {
                Vector3D v1 = _v1.Normalize();
                Vector3D v2 = _v2.Normalize();
                Vector3D v3 = _v3.Normalize();
                MatrixD m = new MatrixD(v1, v2, v3);
                Rotation r = new Rotation(m.Transpose());
                return new Box3d(_point, 2.0 * this.A, 2.0 * this.B, 2.0 * this.C, r);
            }
        }

        /// <summary>
        /// Return Axis Aligned Bounding Box (AABB) in given coordinate system.
        /// </summary>
        public Box3d BoundingBox(Coord3d coord = null)
        {
            coord = (coord == null) ? Coord3d.GlobalCS : coord;
            LineD l1 = new LineD(coord.Origin, coord.Xaxis);
            LineD l2 = new LineD(coord.Origin, coord.Yaxis);
            LineD l3 = new LineD(coord.Origin, coord.Zaxis);
            Segment3d s1 = this.ProjectionTo(l1);
            Segment3d s2 = this.ProjectionTo(l2);
            Segment3d s3 = this.ProjectionTo(l3);
            return new Box3d(_point, s1.Length, s2.Length, s3.Length, coord);
        }

        /// <summary>
        /// Return bounding sphere.
        /// </summary>
        public BoundingSphereD BoundingSphere
        {
            get { return new BoundingSphereD(_point, this.A); }

        }
        #endregion

        /// <summary>
        /// Orthogonal projection of ellipsoid to line.
        /// </summary>
        public Segment3d ProjectionTo(LineD l)
        {
            //Stephen B. Pope "Algorithms for Ellipsoids"
            // https://tcg.mae.cornell.edu/pubs/Pope_FDA_08.pdf

            Coord3d lc = new Coord3d(_point, _v1, _v2);
            Point x0 = l.Point.ConvertTo(lc);
            Vector3D v = l.Direction.ConvertTo(lc);

            MatrixD L_T = MatrixD.DiagonalMatrix(this.A, this.B, this.C);
            Vector3D c = new Vector3D(0.0, 0.0, 0.0, lc);
            double s0 = v * (c - x0.ToVector) / (v * v);
            Vector3D w = L_T * v / (v * v);
            Point P1 = x0.Translate((s0 + w.Normalize()) * v);
            Point P2 = x0.Translate((s0 - w.Normalize()) * v);
            return new Segment3d(P1, P2);
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

            Coord3d lc = new Coord3d(_point, _v1, _v2);
            Vector3D v0 = s.Direction.ConvertTo(lc);
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

            Point p0 = s.Point.ConvertTo(lc);
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
                return new Point(t + x0, k * t + y0, l * t + z0, lc);
            }
            else
            {
                double t = -(sum1 + Sqrt(det)) / sum2;
                Point p1 = new Point(t + x0, k * t + y0, l * t + z0, lc);
                t = -(sum1 - Sqrt(det)) / sum2;
                Point p2 = new Point(t + x0, k * t + y0, l * t + z0, lc);
                return new Segment3d(p1, p2);
            }
        }

        /// <summary>
        /// Intersection of ellipsoid with plane.
        /// Returns 'null' (no intersection) or object of type 'Point3d' or 'Ellipse'.
        /// </summary>
        public object IntersectionWith(PlaneD plane)
        {
            // Solution 1:
            // Peter Paul Klein 
            // On the Ellipsoid and Plane Intersection Equation
            // Applied Mathematics, 2012, 3, 1634-1640 (DOI:10.4236/am.2012.311226)

            // Solution 2:
            // Sebahattin Bektas
            // Intersection of an Ellipsoid and a Plane
            // International Journal of Research in Engineering and Applied Sciences, VOLUME 6, ISSUE 6 (June, 2016)

            Coord3d lc = new Coord3d(_point, _v1, _v2, "LC1");
            plane.SetCoord(lc);
            double Ax, Ay, Az, Ad;
            double a, b, c;
            if (Abs(plane.C) >= Abs(plane.A) && Abs(plane.C) >= Abs(plane.B))
            {
                a = this.A; b = this.B; c = this.C;
            }
            else
            {
                lc = new Coord3d(_point, _v2, _v3, "LC2");
                plane.SetCoord(lc);
                if (Abs(plane.C) >= Abs(plane.A) && Abs(plane.C) >= Abs(plane.B))
                {
                    a = this.B; b = this.C; c = this.A;
                }
                else
                {
                    lc = new Coord3d(_point, _v3, _v1, "LC3");
                    plane.SetCoord(lc);
                    a = this.C; b = this.A; c = this.B;
                }
            }

            Ax = plane.A; Ay = plane.B; Az = plane.C; Ad = plane.D;
            double tmp = (Az * Az * c * c);
            double AA = 1.0 / (a * a) + Ax * Ax / tmp;
            double BB = 2.0 * Ax * Ay / tmp;
            double CC = 1.0 / (b * b) + Ay * Ay / tmp;
            double DD = 2.0 * Ax * Ad / tmp;
            double EE = 2.0 * Ay * Ad / tmp;
            double FF = Ad * Ad / tmp - 1.0;

            double det = 4.0 * AA * CC - BB * BB;
            if (GeometRi3D.AlmostEqual(det, 0))
            {
                return null;
            }
            double X0 = (BB * EE - 2 * CC * DD) / det;
            double Y0 = (BB * DD - 2 * AA * EE) / det;
            double Z0 = -(Ax * X0 + Ay * Y0 + Ad) / Az;

            Point P0 = new Point(X0, Y0, Z0, lc);
            if (P0.BelongsTo(this))
            {
                // the plane is tangent to ellipsoid
                return P0;
            }
            else if (P0.IsInside(this))
            {
                Vector3D q = P0.ToVector.ConvertTo(lc);
                MatrixD D1 = MatrixD.DiagonalMatrix(1 / a, 1 / b, 1 / c);
                Vector3D r = plane.Normal.ConvertTo(lc).OrthogonalVector.Normalized;
                Vector3D s = plane.Normal.ConvertTo(lc).Cross(r).Normalized;

                double omega = 0;
                double qq, qr, qs, rr, ss, rs;
                if (!GeometRi3D.AlmostEqual((D1 * r) * (D1 * s), 0))
                {
                    rr = (D1 * r) * (D1 * r);
                    rs = (D1 * r) * (D1 * s);
                    ss = (D1 * s) * (D1 * s);
                    if (GeometRi3D.AlmostEqual(rr - ss, 0))
                    {
                        omega = PI / 4;
                    }
                    else
                    {
                        omega = 0.5 * Atan(2.0 * rs / (rr - ss));
                    }
                    Vector3D rprim = Cos(omega) * r + Sin(omega) * s;
                    Vector3D sprim = -Sin(omega) * r + Cos(omega) * s;
                    r = rprim;
                    s = sprim;
                }

                qq = (D1 * q) * (D1 * q);
                qr = (D1 * q) * (D1 * r);
                qs = (D1 * q) * (D1 * s);
                rr = (D1 * r) * (D1 * r);
                ss = (D1 * s) * (D1 * s);

                double d = qq - qr * qr / rr - qs * qs / ss;
                AA = Sqrt((1 - d) / rr);
                BB = Sqrt((1 - d) / ss);

                return new Ellipse(P0, AA * r, BB * s);

            }
            else
            {
                return null;
            }

        }

        #region "TranslateRotateReflect"
        /// <summary>
        /// Translate ellipsoid by a vector
        /// </summary>
        public Ellipsoid Translate(Vector3D v)
        {
            return new Ellipsoid(this.Center.Translate(v), _v1, _v2, _v3);
        }

        /// <summary>
        /// Rotate ellipsoid by a given rotation matrix
        /// </summary>
        [System.Obsolete("use Rotation object and specify rotation center: this.Rotate(Rotation r, Point3d p)")]
        public Ellipsoid Rotate(MatrixD m)
        {
            return new Ellipsoid(this.Center.Rotate(m), _v1.Rotate(m), _v2.Rotate(m), _v3.Rotate(m));
        }

        /// <summary>
        /// Rotate ellipsoid by a given rotation matrix around point 'p' as a rotation center
        /// </summary>
        [System.Obsolete("use Rotation object: this.Rotate(Rotation r, Point3d p)")]
        public Ellipsoid Rotate(MatrixD m, Point p)
        {
            return new Ellipsoid(this.Center.Rotate(m, p), _v1.Rotate(m), _v2.Rotate(m), _v3.Rotate(m));
        }

        /// <summary>
        /// Rotate ellipsoid around point 'p' as a rotation center
        /// </summary>
        public Ellipsoid Rotate(Rotation r, Point p)
        {
            return new Ellipsoid(this.Center.Rotate(r, p), _v1.Rotate(r), _v2.Rotate(r), _v3.Rotate(r));
        }

        /// <summary>
        /// Reflect ellipsoid in given point
        /// </summary>
        public Ellipsoid ReflectIn(Point p)
        {
            return new Ellipsoid(this.Center.ReflectIn(p), _v1.ReflectIn(p), _v2.ReflectIn(p), _v3.ReflectIn(p));
        }

        /// <summary>
        /// Reflect ellipsoid in given line
        /// </summary>
        public Ellipsoid ReflectIn(LineD l)
        {
            return new Ellipsoid(this.Center.ReflectIn(l), _v1.ReflectIn(l), _v2.ReflectIn(l), _v3.ReflectIn(l));
        }

        /// <summary>
        /// Reflect ellipsoid in given plane
        /// </summary>
        public Ellipsoid ReflectIn(PlaneD s)
        {
            return new Ellipsoid(this.Center.ReflectIn(s), _v1.ReflectIn(s), _v2.ReflectIn(s), _v3.ReflectIn(s));
        }
        #endregion

        /// <summary>
        /// Determines whether two objects are equal.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj == null || (!object.ReferenceEquals(this.GetType(), obj.GetType())))
            {
                return false;
            }
            Ellipsoid e = (Ellipsoid)obj;

            if (GeometRi3D.UseAbsoluteTolerance)
            {
                if (this.Center != e.Center)
                {
                    return false;
                }

                if (GeometRi3D.AlmostEqual(this.A, this.B) && GeometRi3D.AlmostEqual(this.A, this.C))
                {
                    // Ellipsoid is sphere
                    if (GeometRi3D.AlmostEqual(e.A, e.B) && GeometRi3D.AlmostEqual(e.A, e.C))
                    {
                        // Second ellipsoid also sphere
                        return GeometRi3D.AlmostEqual(this.A, e.A);
                    }
                    else
                    {
                        return false;
                    }
                }
                else if (GeometRi3D.AlmostEqual(this.A, this.B) && GeometRi3D.AlmostEqual(e.A, e.B))
                {
                    return GeometRi3D.AlmostEqual(this.A, e.A) && GeometRi3D.AlmostEqual(this.C, e.C) &&
                           e.SemiaxisC.IsParallelTo(this.SemiaxisC);
                }
                else if (GeometRi3D.AlmostEqual(this.A, this.C) && GeometRi3D.AlmostEqual(e.A, e.C))
                {
                    return GeometRi3D.AlmostEqual(this.A, e.A) && GeometRi3D.AlmostEqual(this.B, e.B) &&
                           e.SemiaxisB.IsParallelTo(this.SemiaxisB);
                }
                else if (GeometRi3D.AlmostEqual(this.C, this.B) && GeometRi3D.AlmostEqual(e.C, e.B))
                {
                    return GeometRi3D.AlmostEqual(this.A, e.A) && GeometRi3D.AlmostEqual(this.C, e.C) &&
                           e.SemiaxisA.IsParallelTo(this.SemiaxisA);
                }
                else
                {
                    return GeometRi3D.AlmostEqual(this.A, e.A) && e.SemiaxisA.IsParallelTo(this.SemiaxisA) &&
                           GeometRi3D.AlmostEqual(this.B, e.B) && e.SemiaxisB.IsParallelTo(this.SemiaxisB) &&
                           GeometRi3D.AlmostEqual(this.C, e.C) && e.SemiaxisC.IsParallelTo(this.SemiaxisC);
                }
            }
            else
            {
                double tol = GeometRi3D.Tolerance;
                GeometRi3D.Tolerance = tol * e.SemiaxisA.Norm;
                GeometRi3D.UseAbsoluteTolerance = true;

                if (this.Center != e.Center)
                {
                    GeometRi3D.UseAbsoluteTolerance = false;
                    GeometRi3D.Tolerance = tol;
                    return false;
                }

                if (GeometRi3D.AlmostEqual(this.A, this.B) && GeometRi3D.AlmostEqual(this.A, this.C))
                {
                    // Ellipsoid is sphere
                    if (GeometRi3D.AlmostEqual(e.A, e.B) && GeometRi3D.AlmostEqual(e.A, e.C))
                    {
                        // Second ellipsoid also sphere
                        bool res = GeometRi3D.AlmostEqual(this.A, e.A);
                        GeometRi3D.UseAbsoluteTolerance = false;
                        GeometRi3D.Tolerance = tol;
                        return res;
                    }
                    else
                    {
                        GeometRi3D.UseAbsoluteTolerance = false;
                        GeometRi3D.Tolerance = tol;
                        return false;
                    }
                }
                else if (GeometRi3D.AlmostEqual(this.A, this.B) && GeometRi3D.AlmostEqual(e.A, e.B))
                {
                    bool res1 = GeometRi3D.AlmostEqual(this.A, e.A) && GeometRi3D.AlmostEqual(this.C, e.C);
                    GeometRi3D.UseAbsoluteTolerance = false;
                    GeometRi3D.Tolerance = tol;
                    bool res2 = e.SemiaxisC.IsParallelTo(this.SemiaxisC);
                    return res1 && res2;
                }
                else if (GeometRi3D.AlmostEqual(this.A, this.C) && GeometRi3D.AlmostEqual(e.A, e.C))
                {
                    bool res1 = GeometRi3D.AlmostEqual(this.A, e.A) && GeometRi3D.AlmostEqual(this.B, e.B);
                    GeometRi3D.UseAbsoluteTolerance = false;
                    GeometRi3D.Tolerance = tol;
                    bool res2 = e.SemiaxisB.IsParallelTo(this.SemiaxisB);
                    return res1 && res2;
                }
                else if (GeometRi3D.AlmostEqual(this.C, this.B) && GeometRi3D.AlmostEqual(e.C, e.B))
                {
                    bool res1 = GeometRi3D.AlmostEqual(this.A, e.A) && GeometRi3D.AlmostEqual(this.C, e.C);
                    GeometRi3D.UseAbsoluteTolerance = false;
                    GeometRi3D.Tolerance = tol;
                    bool res2 = e.SemiaxisA.IsParallelTo(this.SemiaxisA);
                    return res1 && res2;
                }
                else
                {
                    bool res1 = GeometRi3D.AlmostEqual(this.A, e.A) && GeometRi3D.AlmostEqual(this.B, e.B) && GeometRi3D.AlmostEqual(this.C, e.C);
                    GeometRi3D.UseAbsoluteTolerance = false;
                    GeometRi3D.Tolerance = tol;
                    bool res2 = e.SemiaxisA.IsParallelTo(this.SemiaxisA) && e.SemiaxisB.IsParallelTo(this.SemiaxisB) && e.SemiaxisC.IsParallelTo(this.SemiaxisC);
                    return res1 && res2;
                }
            }


        }

        /// <summary>
        /// Returns the hashcode for the object.
        /// </summary>
        public override int GetHashCode()
        {
            return GeometRi3D.HashFunction(_point.GetHashCode(), _v1.GetHashCode(), _v2.GetHashCode(), _v3.GetHashCode());
        }

        /// <summary>
        /// String representation of an object in global coordinate system.
        /// </summary>
        public override String ToString()
        {
            return ToString(Coord3d.GlobalCS);
        }

        /// <summary>
        /// String representation of an object in reference coordinate system.
        /// </summary>
        public String ToString(Coord3d coord)
        {
            string nl = System.Environment.NewLine;

            if (coord == null) { coord = Coord3d.GlobalCS; }
            Point P = _point.ConvertTo(coord);
            Vector3D v1 = _v1.ConvertTo(coord);
            Vector3D v2 = _v2.ConvertTo(coord);
            Vector3D v3 = _v3.ConvertTo(coord);

            string str = string.Format("Ellipsoid: ") + nl;
            str += string.Format("  Center -> ({0,10:g5}, {1,10:g5}, {2,10:g5})", P.X, P.Y, P.Z) + nl;
            str += string.Format("  Semiaxis A -> ({0,10:g5}, {1,10:g5}, {2,10:g5})", v1.X, v1.Y, v1.Z) + nl;
            str += string.Format("  Semiaxis B -> ({0,10:g5}, {1,10:g5}, {2,10:g5})", v2.X, v2.Y, v2.Z) + nl;
            str += string.Format("  Semiaxis C -> ({0,10:g5}, {1,10:g5}, {2,10:g5})", v3.X, v3.Y, v3.Z) + nl;
            return str;
        }

        // Operators overloads
        //-----------------------------------------------------------------

        public static bool operator ==(Ellipsoid c1, Ellipsoid c2)
        {
            return c1.Equals(c2);
        }
        public static bool operator !=(Ellipsoid c1, Ellipsoid c2)
        {
            return !c1.Equals(c2);
        }
    }
}