using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public static int Mod(int x, int m)
        {
            return (x % m + m) % m;
        }
    }
}
