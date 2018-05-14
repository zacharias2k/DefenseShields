using System;

namespace DefenseShields.Support
{
    public class EllipsoidSA : Shape3D
    {
        public EllipsoidSA(double a, double b, double c)
        {
            base.a = a;
            base.b = b;
            base.c = c;
        }

        public override double Volume
        {
            get
            {
                return (4 / 3 * 3.14 * a * b * c);
            }
        }

        public override double Surface
        {
            get { return (4 * Math.PI * Math.Pow(((Math.Pow(a * b, 1.6) + Math.Pow(a * c, 1.6) + Math.Pow(b * c, 1.6)) / 3), 1 / 1.6)); }
        }
    }
}