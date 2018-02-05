using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRageMath;

namespace DefenseShields.Support
{
    class MyMaths
    {
        public static int FaceDivder(int steps, int numFaces)
        {
            var _faceDiv = 0;
            if (numFaces % steps == 0)
            {
                _faceDiv = numFaces / steps;
                return _faceDiv;
            }
            if (steps % numFaces == 0)
            {
                _faceDiv = steps / numFaces * -1;
                return _faceDiv;
            }
            if (_faceDiv == 0) throw new Exception("Invalid number of steps");
            return _faceDiv;
        }
        public static float Mod(int x, int m)
        {
            return (x % m + m) % m;
        }

        public static Vector3D HighestVec(params Vector3D[] inputs)
        {
            return inputs.Max();
        }

        public static Vector3D LowesVect(params Vector3D[] inputs)
        {
            return inputs.Min();
        }
    }
}
