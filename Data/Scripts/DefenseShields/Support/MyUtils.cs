using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DefenseShields.Support
{
    class DSUtils
    {

        public static Stopwatch Sw { get; }= new Stopwatch();

        public static void StopWatchReport(string message, int log)
        {
            Sw.Stop();
            long ticks = Sw.ElapsedTicks;
            double ns = 1000000000.0 * (double)ticks / Stopwatch.Frequency;
            double ms = ns / 1000000.0;
            double s = ms / 1000;

            if (log == 0) Log.Line($"{message} - ns:{ns} ms:{ms} s:{s}");
            else
            {
                if (ms >= log) Log.Line($"{message} - ns:{ns} ms:{ms} s:{s}");
            }
            Sw.Reset();
        }
    }
}
