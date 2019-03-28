using System.Collections;
namespace DefenseShields.Support
{
    internal static class Perf
    {
        internal static int Counter = -1;
        internal static bool Alive;
        internal static Stats[] Storage = new Stats[60];

        internal static void Ticker(uint tick)
        {
            if (tick % 600 == 0)
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
                case 60:
                {
                    Counter = 0;
                    Alive = false;
                    for (int i = 0; i < 60; i++)
                    {
                        var s = Storage[i];
                         Log.Line($"Counter{i}");
                         Log.Chars($"{s.Active}\n" +
                                   $"{s.Asleep}\n" +
                                   $"{s.Awake} \n" +
                                   $"{s.Paused}\n" +
                                   $"{s.Emitters}\n" +
                                   $"{s.Enhancers}\n" +
                                   $"{s.Modulators}\n" +
                                   $"{s.Displays}\n" +
                                   $"{s.O2Generators}\n" +
                                   $"{s.EntChanged}\n" +
                                   $"{s.ShapeChanged}\n" +
                                   $"{s.Moving}\n" +
                                   $"{s.ThreadEvents}\n" +
                                   $"{s.WebEnts}\n" +
                                   $"{s.WebPhysics}\n" +
                                   $"{s.Protected}");
                    }
                    break;
                }
            }
        }

        internal static void Init()
        {
            for (int i = 0; i < 60; i++) Storage[i] = new Stats();
            Counter = 0;
        }

        internal static void Reset()
        {
            for (int i = 0; i < 60; i++)
            {
                var s = Storage[i];
                s.WebPhysics = 0;
                s.WebEnts = 0;
                s.EntChanged = 0;
                s.ThreadEvents = 0;
                s.Asleep = 0;
                s.Awake = 0;
                s.Active = 0;
                s.Paused = 0;
                s.Moving = 0;
                s.ShapeChanged = 0;
                s.Protected = 0;
                s.Emitters = 0;
                s.Modulators = 0;
                s.Displays = 0;
                s.O2Generators = 0;
                s.Enhancers = 0;
            }
        }

        internal static void WebPhysics()
        {
            if (Alive) Storage[Counter].WebPhysics++;
        }

        internal static void WebEnts(int value)
        {
            if (Alive) Storage[Counter].WebEnts += value;
        }

        internal static void EntChanged()
        {
            if (Alive) Storage[Counter].EntChanged++;
        }

        internal static void ThreadEvents(int value)
        {
            if (Alive) Storage[Counter].ThreadEvents = value;
        }

        internal static void Asleep()
        {
            if (Alive) Storage[Counter].Asleep++;
        }

        internal static void Awake()
        {
            if (Alive) Storage[Counter].Awake++;
        }

        internal static void Active(int value)
        {
            if (Alive) Storage[Counter].Active = value;
        }

        internal static void Paused(int value)
        {
            if (Alive) Storage[Counter].Paused = value - Storage[Counter].Active;
        }

        internal static void Moving()
        {
            if (Alive) Storage[Counter].Moving++;
        }

        internal static void ShapeChanged()
        {
            if (Alive) Storage[Counter].ShapeChanged++;
        }

        internal static void Protected(int value)
        {
            if (Alive) Storage[Counter].Protected = value;
        }

        internal static void Emitters(int value)
        {
            if (Alive) Storage[Counter].Emitters = value;
        }

        internal static void Modulators(int value)
        {
            if (Alive) Storage[Counter].Modulators = value;
        }

        internal static void Displays(int value)
        {
            if (Alive) Storage[Counter].Displays = value;
        }

        internal static void O2Generators(int value)
        {
            if (Alive) Storage[Counter].O2Generators = value;
        }

        internal static void Enhancers(int value)
        {
            if (Alive) Storage[Counter].Enhancers = value;
        }
    }

    internal class Stats
    {
        internal string[] Name = {
            "WebPhysics", "WebEnts", "EntChanged", "ThreadEvents", "Asleep", "Awake", "Active", "Paused", "Moving",
            "ShapeChanged", "Protected", "Emitters", "Modulators", "Displays", "O2Generators", "Enhancers"
        };

        private int _position;
        internal int WebPhysics;
        internal int WebEnts;
        internal int EntChanged;
        internal int ThreadEvents;
        internal int Asleep;
        internal int Awake;
        internal int Active;
        internal int Paused;
        internal int Moving;
        internal int ShapeChanged;
        internal int Protected;
        internal int Emitters;
        internal int Modulators;
        internal int Displays;
        internal int O2Generators;
        internal int Enhancers;
    }
}
