using System;
using System.IO;
using Sandbox.ModAPI;
using VRageRender;

namespace DefenseShields
{
    public class Logging
    {
        private static Logging INSTANCE = null;
        private TextWriter file = null;
        private string fileName = "";

        private Logging()
        {
        }

        private static Logging getInstance()
        {
            if (Logging.INSTANCE == null)
            {
                Logging.INSTANCE = new Logging();
            }

            return INSTANCE;
        }

        public static bool init(string name)
        {

            bool output = false;

            if (getInstance().file == null)
            {

                try
                {
                    MyAPIGateway.Utilities.ShowNotification(name, 5000);
                    getInstance().fileName = name;
                    getInstance().file = MyAPIGateway.Utilities.WriteFileInLocalStorage(name, typeof(Logging));
                    output = true;
                }
                catch (Exception e)
                {
                    MyAPIGateway.Utilities.ShowNotification(e.Message, 5000);
                }
            }
            else
            {
                output = true;
            }

            return output;
        }

        public static void writeLine(string text)
        {
            try
            {
                if (getInstance().file != null)
                {
                    getInstance().file.WriteLine(text);
                    getInstance().file.Flush();
                }
            }
            catch (Exception e)
            {
            }
        }

        public static void close()
        {
            try
            {
                if (getInstance().file != null)
                {
                    getInstance().file.Flush();
                    getInstance().file.Close();
                }
            }
            catch (Exception e)
            {
            }
        }
    }
}

