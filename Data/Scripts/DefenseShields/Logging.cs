using System;
using System.IO;
using Sandbox.ModAPI;
using VRageRender;

namespace DefenseShields
{
    public class Logging
    {
        private static Logging _instance = null;
        private TextWriter _file = null;
        private string _fileName = "";

        private Logging()
        {
        }

        private static Logging GetInstance()
        {
            if (Logging._instance == null)
            {
                Logging._instance = new Logging();
            }

            return _instance;
        }

        public static bool Init(string name)
        {

            bool output = false;

            if (GetInstance()._file == null)
            {

                try
                {
                    MyAPIGateway.Utilities.ShowNotification(name, 5000);
                    GetInstance()._fileName = name;
                    GetInstance()._file = MyAPIGateway.Utilities.WriteFileInLocalStorage(name, typeof(Logging));
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

        public static void WriteLine(string text)
        {
            try
            {
                if (GetInstance()._file != null)
                {
                    GetInstance()._file.WriteLine(text);
                    GetInstance()._file.Flush();
                }
            }
            catch (Exception e)
            {
            }
        }

        public static void Close()
        {
            try
            {
                if (GetInstance()._file != null)
                {
                    GetInstance()._file.Flush();
                    GetInstance()._file.Close();
                }
            }
            catch (Exception e)
            {
            }
        }
    }
}

