using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DefenseShields.Support
{
    public class DSUtils
    {

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
    }
}
