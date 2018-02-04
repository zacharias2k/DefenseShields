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

        public static Stopwatch Sw { get; } = new Stopwatch();
        private static double _last;
        public static void StopWatchReport(string message, float log)
        {
            Sw.Stop();
            long ticks = Sw.ElapsedTicks;
            double ns = 1000000000.0 * ticks / Stopwatch.Frequency;
            double ms = ns / 1000000.0;
            double s = ms / 1000;
            if (log <= -1) Log.Line($"{message} - ms:{(float)ms} last-ms:{(float)_last} s:{(int)s}");
            else
            {
                if (ms >= log) Log.Line($"{message} - ms:{(float)ms} last-ms:{(float)_last} s:{(int)s}");
            }
            _last = ms;
            Sw.Reset();
        }
    }
}
