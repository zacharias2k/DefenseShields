using System.Collections.Generic;
using System.Diagnostics;
using VRageMath;

namespace DefenseShields.Support
{
    public class DSUtils
    {
        public static float Mod(int x, int m)
        {
            return (x % m + m) % m;
        }

        public Stopwatch Sw { get; } = new Stopwatch();
        public double Last;
        public void StopWatchReport(string message, float log)
        {
            Sw.Stop();
            long ticks = Sw.ElapsedTicks;
            double ns = 1000000000.0 * ticks / Stopwatch.Frequency;
            double ms = ns / 1000000.0;
            double s = ms / 1000;
            if (log <= -1) Log.Line($"{message} - ms:{(float)ms} last-ms:{(float)Last} s:{(int)s}");
            else
            {
                if (ms >= log) Log.Line($"{message} - ms:{(float)ms} last-ms:{(float)Last} s:{(int)s}");
            }
            Last = ms;
            Sw.Reset();
        }

        public static BoundingSphereD CreateFromPointsList(List<Vector3D> points)
        {
            Vector3D current;
            Vector3D Vector3D_1 = current = points[0];
            Vector3D Vector3D_2 = current;
            Vector3D Vector3D_3 = current;
            Vector3D Vector3D_4 = current;
            Vector3D Vector3D_5 = current;
            Vector3D Vector3D_6 = current;
            foreach (Vector3D Vector3D_7 in points)
            {
                if (Vector3D_7.X < Vector3D_6.X)
                    Vector3D_6 = Vector3D_7;
                if (Vector3D_7.X > Vector3D_5.X)
                    Vector3D_5 = Vector3D_7;
                if (Vector3D_7.Y < Vector3D_4.Y)
                    Vector3D_4 = Vector3D_7;
                if (Vector3D_7.Y > Vector3D_3.Y)
                    Vector3D_3 = Vector3D_7;
                if (Vector3D_7.Z < Vector3D_2.Z)
                    Vector3D_2 = Vector3D_7;
                if (Vector3D_7.Z > Vector3D_1.Z)
                    Vector3D_1 = Vector3D_7;
            }
            double result1;
            Vector3D.Distance(ref Vector3D_5, ref Vector3D_6, out result1);
            double result2;
            Vector3D.Distance(ref Vector3D_3, ref Vector3D_4, out result2);
            double result3;
            Vector3D.Distance(ref Vector3D_1, ref Vector3D_2, out result3);
            Vector3D result4;
            double num1;
            if (result1 > result2)
            {
                if (result1 > result3)
                {
                    Vector3D.Lerp(ref Vector3D_5, ref Vector3D_6, 0.5f, out result4);
                    num1 = result1 * 0.5f;
                }
                else
                {
                    Vector3D.Lerp(ref Vector3D_1, ref Vector3D_2, 0.5f, out result4);
                    num1 = result3 * 0.5f;
                }
            }
            else if (result2 > result3)
            {
                Vector3D.Lerp(ref Vector3D_3, ref Vector3D_4, 0.5f, out result4);
                num1 = result2 * 0.5f;
            }
            else
            {
                Vector3D.Lerp(ref Vector3D_1, ref Vector3D_2, 0.5f, out result4);
                num1 = result3 * 0.5f;
            }
            foreach (Vector3D Vector3D_7 in points)
            {
                Vector3D Vector3D_8;
                Vector3D_8.X = Vector3D_7.X - result4.X;
                Vector3D_8.Y = Vector3D_7.Y - result4.Y;
                Vector3D_8.Z = Vector3D_7.Z - result4.Z;
                double num2 = Vector3D_8.Length();
                if (num2 > num1)
                {
                    num1 = ((num1 + num2) * 0.5);
                    result4 += (1.0 - num1 / num2) * Vector3D_8;
                }
            }
            BoundingSphereD boundingSphereD;
            boundingSphereD.Center = result4;
            boundingSphereD.Radius = num1;
            return boundingSphereD;
        }
    }
}
