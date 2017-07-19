using Akka.Interfaced;
using Echo.Interface;
using System;
using System.Threading;

namespace Echo.Program.Server
{
    public class EchoActor : InterfacedActor, IEchoSync
    {
        public static int EchoCount;

        public EchoActor()
        {
        }

        [ResponsiveExceptionAll]
        byte[] IEchoSync.Echo(byte[] data)
        {
            Interlocked.Increment(ref EchoCount);

            var copied = new byte[data.Length];
            Buffer.BlockCopy(data, 0, copied, 0, data.Length);
            return copied;
        }

        protected override void PostStop()
        {
            base.PostStop();

            Console.WriteLine($"EchoActor Stop {Self.Path}");
        }
    }
}
