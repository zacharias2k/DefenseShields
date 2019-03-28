using System.Collections.Generic;

namespace DefenseShields.Support
{
    internal class Perf
    {
        internal int Counter = -1;
        internal bool Alive;

        internal void Ticker(uint tick, int resetTime, bool fullReport, int column = 0)
        {
            if (resetTime <= 0)
            {
                return;
            }
            if (tick % resetTime == 0)
            {
                Alive = true;
            }
            else if (Alive == false) return;
            switch (Counter++)
            {
                case -1:
                    Init();
                    break;
                case 0:
                    Reset();
                    break;
                case 59:
                {
                    Log.Line("Counter59");
                        Counter = 0;
                    Alive = false;
                    ProcessData(fullReport, column);
                    break;
                }
            }
        }

        internal void Init()
        {
            Counter = 0;
        }

        internal void Reset()
        {
            for (int i = 0; i < 16; i++)
            {
                for (int j = 0; j < 60; j++) Storage[i][j] = 0;
            }
        }

        internal void ProcessData(bool fullReport, int column)
        {
            var eventLog = Session.Instance.EventLog;
            if (fullReport)
            {
                for (int i = 0; i < 16; i++)
                {
                    var s = Storage[i];
                    var name = $"\n{Names[i]}: ";
                    var logString = string.Empty;

                    for (int j = 0; j < 60; j++) logString += $" {s[j]}";

                    eventLog[i] = name + logString;
                }
            }
            else
            {
                var name = $"\n{Names[column]}: ";
                var logString = string.Empty;

                for (int j = 0; j < 60; j++) logString += $" {Storage[column][j]}";

                eventLog[column] = name + logString;
            }
            Log.Line("GenerateReport");
            Session.Instance.GenerateReport();
        }

        internal readonly Dictionary<int, string> Names = new Dictionary<int, string>
        {
            {0, "WebPhysics"}, {1, "WebEnts"}, {2, "EntChanged"}, {3, "ThreadEvents"}, {4, "Asleep"}, {5, "Awake"},
            {6, "Active"}, {7, "Paused"}, {8, "Moving"}, {9, "ShapeChanged"}, {10, "Protected"}, {11, "Emitters"},
            {12, "Modulators"}, {13, "Displays"}, {14, "O2Generators"}, {15, "Enhancers"}
        };

        internal void WebPhysics()
        {
            if (Alive) Storage[0][Counter]++;
        }

        internal void WebEnts(int value)
        {
            if (Alive) Storage[1][Counter] += value;
        }

        internal void EntChanged()
        {
            if (Alive) Storage[2][Counter]++;
        }

        internal void ThreadEvents(int value)
        {
            if (Alive) Storage[3][Counter] = value;
        }

        internal void Asleep()
        {
            if (Alive) Storage[4][Counter]++;
        }

        internal void Awake()
        {
            if (Alive) Storage[5][Counter]++;
        }

        internal void Active(int value)
        {
            if (Alive) Storage[6][Counter] = value;
        }

        internal void Paused(int value)
        {
            if (Alive) Storage[7][Counter] = value;
        }

        internal void Moving()
        {
            if (Alive) Storage[8][Counter]++;
        }

        internal void ShapeChanged()
        {
            if (Alive) Storage[9][Counter]++;
        }

        internal void Protected(int value)
        {
            if (Alive) Storage[10][Counter] = value;
        }

        internal void Emitters(int value)
        {
            if (Alive) Storage[11][Counter] = value;
        }

        internal void Modulators(int value)
        {
            if (Alive) Storage[12][Counter] = value;
        }

        internal void Displays(int value)
        {
            if (Alive) Storage[13][Counter] = value;
        }

        internal void O2Generators(int value)
        {
            if (Alive) Storage[14][Counter] = value;
        }

        internal void Enhancers(int value)
        {
            if (Alive) Storage[15][Counter] = value;
        }

        internal int[][] Storage = new int[16][]
        {
            new int[60] //0 WebPhysics
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
            },
            new int[60] //1 WebEnts
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
            },
            new int[60] //2 EntChanged
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
            },
            new int[60] //3 ThreadEvents
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
            },
            new int[60] //4 Asleep
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
            },
            new int[60] //5 Awake
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
            },
            new int[60] //6 Active
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
            },
            new int[60] //7 Paused
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
            },
            new int[60] //8 Moving
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
            },
            new int[60] //9 ShapeChanged
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
            },
            new int[60] //10 Protected
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
            },
            new int[60] //11 Emitters
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
            },
            new int[60] //12 Modulators
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
            },
            new int[60] //13 Displays
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
            },
            new int[60] //14 O2Generators
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
            },
            new int[60] //15 Enhancers
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
            },
        };
    }
}
