using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace DefenseShields
{
    public class Intersect : Station.DefenseShields
    {
        #region Detection Methods
        public static bool Detectin(IMyEntity ent)
        {
            float x = Vector3Extensions.Project(_worldMatrix.Forward, ent.GetPosition() - _worldMatrix.Translation).AbsMax();
            float y = Vector3Extensions.Project(_worldMatrix.Left, ent.GetPosition() - _worldMatrix.Translation).AbsMax();
            float z = Vector3Extensions.Project(_worldMatrix.Up, ent.GetPosition() - _worldMatrix.Translation).AbsMax();
            float detect = (x * x) / (_inWidth * _inWidth) + (y * y) / (_inDepth * _inDepth) + (z * z) / (_inHeight * _inHeight);
            if (detect <= 1)
            {
                //Logging.WriteLine(String.Format("{0} - {1} in-t: x:{2} y:{3} z:{4} d:{5} l:{6}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), ent, x, y, z, detect, Count));
                return true;
            }
            //Logging.WriteLine(String.Format("{0} - {1} in-f - d:{5} l:{6}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), ent, detect, Count));
            return false;
        }

        public static bool Detectedge(IMyEntity ent)
        {
            float x = Vector3Extensions.Project(_worldMatrix.Forward, ent.GetPosition() - _worldMatrix.Translation).AbsMax();
            float y = Vector3Extensions.Project(_worldMatrix.Left, ent.GetPosition() - _worldMatrix.Translation).AbsMax();
            float z = Vector3Extensions.Project(_worldMatrix.Up, ent.GetPosition() - _worldMatrix.Translation).AbsMax();
            float detect = (x * x) / (_width * _width) + (y * y) / (_depth * _depth) + (z * z) / (_height * _height);
            if (detect <= 1)
            {
                //Logging.WriteLine(String.Format("{0} - {1} edge-t - d:{2} l:{3}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), ent, detect, Count));
                return true;
            }
            //if (detect <= 1.1) Logging.WriteLine(String.Format("{0} - {1} edge-f - d:{2} l:{3}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), ent, detect, Count));
            return false;
        }

        public static bool Detectgridedge(IMyCubeGrid grid)
        {
            float x = Vector3Extensions.Project(_worldMatrix.Forward, grid.GetPosition() - _worldMatrix.Translation).AbsMax();
            float y = Vector3Extensions.Project(_worldMatrix.Left, grid.GetPosition() - _worldMatrix.Translation).AbsMax();
            float z = Vector3Extensions.Project(_worldMatrix.Up, grid.GetPosition() - _worldMatrix.Translation).AbsMax();
            float detect = (x * x) / (_width * _width) + (y * y) / (_depth * _depth) + (z * z) / (_height * _height);
            if (detect <= 1)
            {
                Logging.WriteLine(String.Format("{0} - {1} grid-t - d:{2} l:{3}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), grid.CustomName, detect, Count));
                return true;
            }
            Logging.WriteLine(String.Format("{0} - {1} grid-f - d:{2} l:{3}", DateTime.Now.ToString("MM-dd-yy_HH-mm-ss-fff"), grid.CustomName, detect, Count));
            return false;
        }
        #endregion
    }
}
