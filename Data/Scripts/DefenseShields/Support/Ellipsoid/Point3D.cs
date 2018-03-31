using System;
using static System.Math;

namespace DefenseShields.Support
{
    /*
    /// <summary>
    /// Point in 3D space defined in global or local reference frame.
    /// </summary>
    public class Point3d
    {

        private double _x;
        private double _y;
        private double _z;
        private Coord3d _coord;

        #region "Constructors"
        /// <summary>
        /// Default constructor, initializes zero point.
        /// </summary>
        /// <param name="coord">Reference coordinate system (default - Coord3d.GlobalCS).</param>
        public Point3d(Coord3d coord = null)
        {
            _x = 0.0;
            _y = 0.0;
            _z = 0.0;
            _coord = (coord == null) ? Coord3d.GlobalCS : coord;
        }

        /// <summary>
        /// Initiaizes point object using coordinates.
        /// </summary>
        /// <param name="coord">Reference coordinate system (default - Coord3d.GlobalCS).</param>
        public Point3d(double x, double y, double z, Coord3d coord = null)
        {
            _x = x;
            _y = y;
            _z = z;
            _coord = (coord == null) ? Coord3d.GlobalCS : coord;
        }

        /// <summary>
        /// Initiaizes point object using double array.
        /// </summary>
        /// <param name="coord">Reference coordinate system (default - Coord3d.GlobalCS).</param>
        public Point3d(double[] a, Coord3d coord = null)
        {
            if (a.GetUpperBound(0) < 2)
                throw new Exception("Point3d: Array size mismatch");
            _x = a[0];
            _y = a[1];
            _z = a[2];
            _coord = (coord == null) ? Coord3d.GlobalCS : coord;
        }
        #endregion

        /// <summary>
        /// Creates copy of the object
        /// </summary>
        public Point3d Copy()
        {
            return new Point3d(_x, _y, _z, _coord);
        }

        /// <summary>
        /// X coordinate in reference coordinate system
        /// </summary>
        public double X
        {
            get { return _x; }
            set { _x = value; }
        }
        /// <summary>
        /// Y coordinate in reference coordinate system
        /// </summary>
        public double Y
        {
            get { return _y; }
            set { _y = value; }
        }
        /// <summary>
        /// Z coordinate in reference coordinate system
        /// </summary>
        public double Z
        {
            get { return _z; }
            set { _z = value; }
        }

        /// <summary>
        ///  Reference coordinate system
        /// </summary>
        public Coord3d Coord
        {
            get { return _coord; }
        }

        /// <summary>
        /// Radius vector of point (in global coordinate system)
        /// </summary>
        public Vector3d ToVector
        {
            get { return new Vector3d(this); }
        }
        /// <summary>
        /// Convert point to reference coordinate system
        /// </summary>
        public Point3d ConvertTo(Coord3d coord)
        {
            Point3d p = this.Copy();

            p = p.ConvertToGlobal();
            if (coord == null || object.ReferenceEquals(coord, Coord3d.GlobalCS))
                return p;

            p = coord.Axes * (p - coord.Origin);
            p._coord = coord;

            return p;
        }

        /// <summary>
        /// Convert point to global coordinate system
        /// </summary>
        /// <returns></returns>
        public Point3d ConvertToGlobal()
        {
            if (_coord == null || object.ReferenceEquals(_coord, Coord3d.GlobalCS))
            {
                return this;
            }
            else
            {
                Vector3d v = new Vector3d(this.X, this.Y, this.Z);
                v = _coord.Axes.Inverse() * v;

                return v.ToPoint + _coord.Origin;

            }

        }

        public Point3d Add(Point3d p)
        {
            if ((this._coord != p._coord))
                p = p.ConvertTo(this._coord);
            Point3d tmp = this.Copy();
            tmp.X += p.X;
            tmp.Y += p.Y;
            tmp.Z += p.Z;
            return tmp;
        }
        public Point3d Subtract(Point3d p)
        {
            if ((this._coord != p._coord))
                p = p.ConvertTo(this._coord);
            Point3d tmp = this.Copy();
            tmp.X -= p.X;
            tmp.Y -= p.Y;
            tmp.Z -= p.Z;
            return tmp;
        }
        public Point3d Scale(double a)
        {
            Point3d tmp = this.Copy();
            tmp.X *= a;
            tmp.Y *= a;
            tmp.Z *= a;
            return tmp;
        }

        /// <summary>
        /// Check if point belongs to the line
        /// </summary>
        /// <param name="l"></param>
        /// <returns>True, if the point belongs to the line</returns>
        public bool BelongsTo(Line3d l)
        {
            if (this == l.Point)
            {
                return true;
            }
            else
            {
                return l.Direction.IsParallelTo(new Vector3d(this, l.Point));
            }
        }

        /// <summary>
        /// Check if point is inside ellipsoid
        /// </summary>
        public bool IsInside(Ellipsoid e)
        {
            Coord3d lc = new Coord3d(e.Center, e.SemiaxisA, e.SemiaxisB);
            Point3d p = this.ConvertTo(lc);
            if (GeometRi3D.UseAbsoluteTolerance)
            {
                return GeometRi3D.Smaller(p.X * p.X / e.A / e.A + p.Y * p.Y / e.B / e.B + p.Z * p.Z / e.C / e.C, 1.0);
            }
            else
            {
                double tol = GeometRi3D.Tolerance;
                GeometRi3D.Tolerance = tol * e.SemiaxisA.Norm;
                GeometRi3D.UseAbsoluteTolerance = true;
                bool result = this.IsInside(e);
                GeometRi3D.UseAbsoluteTolerance = false;
                GeometRi3D.Tolerance = tol;
                return result;
            }
        }

        // Operators overloads
        //-----------------------------------------------------------------
        public static Point3d operator +(Point3d v, Point3d a)
        {
            return v.Add(a);
        }
        public static Point3d operator -(Point3d v, Point3d a)
        {
            return v.Subtract(a);
        }
        public static Point3d operator -(Point3d v)
        {
            return v.Scale(-1.0);
        }
        public static Point3d operator *(Point3d v, double a)
        {
            return v.Scale(a);
        }
        public static Point3d operator *(double a, Point3d v)
        {
            return v.Scale(a);
        }
        public static Point3d operator /(Point3d v, double a)
        {
            return v.Scale(1.0 / a);
        }

        public static bool operator ==(Point3d p1, Point3d p2)
        {
            return p1.Equals(p2);
        }
        public static bool operator !=(Point3d p1, Point3d p2)
        {
            return !p1.Equals(p2);
        }
    }
    */
}