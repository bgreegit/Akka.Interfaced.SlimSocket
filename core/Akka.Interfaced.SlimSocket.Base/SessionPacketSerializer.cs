using System;
using System.IO;

namespace Akka.Interfaced.SlimSocket
{
    public class SessionPacketSerializer : IPacketSerializer
    {
        private IPacketSerializer _innerPacketSerializer;
        private static Func<SessionPacket>[] _sessionPacketFactory;

        static SessionPacketSerializer()
        {
            var typeValues = Enum.GetValues(typeof(SessionPacketType));
            _sessionPacketFactory = new Func<SessionPacket>[typeValues.Length];
            _sessionPacketFactory[(int)SessionPacketType.SqSessionCreate] = () => new SqSessionCreate();
            _sessionPacketFactory[(int)SessionPacketType.SrSessionCreate] = () => new SrSessionCreate();
            _sessionPacketFactory[(int)SessionPacketType.SqSessionRebind] = () => new SqSessionRebind();
            _sessionPacketFactory[(int)SessionPacketType.SrSessionRebind] = () => new SrSessionRebind();
            _sessionPacketFactory[(int)SessionPacketType.ScSessionAck] = () => new ScSessionAck();
            _sessionPacketFactory[(int)SessionPacketType.ScSessionInnerPacket] = () => new ScSessionInnerPacket();
            _sessionPacketFactory[(int)SessionPacketType.ScSessionFin] = () => new ScSessionFin();
            _sessionPacketFactory[(int)SessionPacketType.ScSessionFinAck] = () => new ScSessionFinAck();
            _sessionPacketFactory[(int)SessionPacketType.SqEcho] = () => new SqEcho();
            _sessionPacketFactory[(int)SessionPacketType.SrEcho] = () => new SrEcho();
        }

        public SessionPacketSerializer(IPacketSerializer innerPacketSerializer)
        {
            _innerPacketSerializer = innerPacketSerializer;
        }

        int IPacketSerializer.EstimateLength(object packet)
        {
            var sp = (SessionPacket)packet;
            return sp.EstimateLength(_innerPacketSerializer);
        }

        void IPacketSerializer.Serialize(Stream stream, object packet)
        {
            var sp = (SessionPacket)packet;
            var packetLengthMarker = new StreamLengthMarker(stream, true);
            stream.WriteByte((byte)sp.PacketType);
            sp.Serialize(_innerPacketSerializer, stream);
            packetLengthMarker.WriteLength(true);
        }

        int IPacketSerializer.PeekLength(Stream stream)
        {
            var len = (int)(stream.Length - stream.Position);
            if (len < 4)
                return 0;

            // Peek Len
            var bytes = new byte[4];
            stream.Read(bytes, 0, 4);
            stream.Seek(-4, SeekOrigin.Current);

            return BitConverter.ToInt32(bytes, 0) + 4;
        }

        object IPacketSerializer.Deserialize(Stream stream)
        {
            int len = stream.Read32BitEncodedInt();
            byte packetType = (byte)stream.ReadByte();
            if (packetType < _sessionPacketFactory.Length)
            {
                var sp = _sessionPacketFactory[packetType].Invoke();
                sp.Deserialize(_innerPacketSerializer, stream);
                return sp;
            }
            else
            {
                return null;
            }
        }
    }
}
