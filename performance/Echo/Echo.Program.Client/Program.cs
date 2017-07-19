using Echo.Interface;
using Newtonsoft.Json;
using System;
using System.IO;

namespace Echo.Program.Client
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            if (typeof(IEcho) == null)
            {
                throw new Exception("Force interface module to be loaded");
            }

            var config = LoadConfig<EchoDriver.Config>(args[0]);
            if (config == null)
            {
                Console.WriteLine($"Fail to read config {args[0]}");
                return;
            }

            // sample)
            //config = new EchoDriver.Config()
            //{
            //    ChannelType = "Session",
            //    RemoteIp = "127.0.0.1",
            //    RemotePort = 5001,
            //    RequestInterval = 100,
            //    RequestWaitDelay = 0,
            //    RequestLength = 32,
            //    ChannelCount = 100
            //};

            var s = JsonConvert.SerializeObject(config);
            Console.WriteLine(s);

            var driver = new EchoDriver(config);
            driver.Start();
            WaitForExit();
            driver.Stop();
        }

        public static void WaitForExit()
        {
            const string msg = "type [e] to exit";
            Console.WriteLine(msg);
            while (Console.ReadLine() != "e")
            {
                Console.WriteLine(msg);
            }
        }

        public static T LoadConfig<T>(string path) where T : class
        {
            try
            {
                using (StreamReader r = new StreamReader(path))
                {
                    return JsonConvert.DeserializeObject<T>(r.ReadToEnd());
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
