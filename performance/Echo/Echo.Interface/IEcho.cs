using System.Threading.Tasks;
using Akka.Interfaced;

namespace Echo.Interface
{
    public interface IEcho : IInterfacedActor
    {
        Task<byte[]> Echo(byte[] data);
    }
}
