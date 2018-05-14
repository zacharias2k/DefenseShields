using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRageMath;

namespace DefenseShields.Support
{
    class Ellipsoid2
    {
        public class ShapeEllipsoid : Shape
        {
            Vector3 m_radius;

            public ShapeEllipsoid(Vector3D radius)
            {
                m_radius = radius;
            }

            public Vector3D GetSupportPoint(Vector3D n)
            {
                Vector3D nn = Vector3D.Normalize(n);

                nn = new Vector3(1,1,1);
                double t = nn.X*nn.X/(m_radius.X*m_radius.X) + nn.Y*nn.Y/(m_radius.Y*m_radius.Y) + nn.Z*nn.Z/(m_radius.Z*m_radius.Z);

                return nn * m_radius;
            }

            public override void SupportMapping(ref Vector3D direction, out Vector3D result)
            {
                result = Vector3D.Zero;
                //throw new NotImplementedException();
            }
        };
    }
}
