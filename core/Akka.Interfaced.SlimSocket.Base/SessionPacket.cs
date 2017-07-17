using System.IO;

namespace Akka.Interfaced.SlimSocket
{
    public enum SessionPacketType
    {
        SqSessionCreate,
        SrSessionCreate,
        SqSessionRebind,
        SrSessionRebind,
        ScSessionAck,
        ScSessionInnerPacket,
        ScSessionFin,
        ScSessionFinAck,
        SqEcho,
        SrEcho,
    }

    public abstract class SessionPacket
    {
        public abstract SessionPacketType PacketType { get; }
        public abstract int EstimateLength(IPacketSerializer innerPacketSerializer);
        public abstract void Serialize(IPacketSerializer innerPacketSerializer, Stream stream);
        public abstract void Deserialize(IPacketSerializer innerPacketSerializer, Stream stream);

        protected static int _maxLengthOf7BitEncodedInt = SerializeExtensions.Estimate7BitEncodedIntLength();
    }

    public class SqSessionCreate : SessionPacket
    {
        public string Token;

        public override SessionPacketType PacketType => SessionPacketType.SqSessionCreate;

        public override int EstimateLength(IPacketSerializer innerPacketSerializer)
        {
            return SerializeExtensions.EstimateStringLength(Token);
        }

        public override void Serialize(IPacketSerializer innerPacketSerializer, Stream stream)
        {
            stream.WriteString(Token);
        }

        public override void Deserialize(IPacketSerializer innerPacketSerializer, Stream stream)
        {
            Token = stream.ReadString();
        }
    }

    public class SrSessionCreate : SessionPacket
    {
        public int SessionId;

        public override SessionPacketType PacketType => SessionPacketType.SrSessionCreate;

        public override int EstimateLength(IPacketSerializer innerPacketSerializer)
        {
            return _maxLengthOf7BitEncodedInt;
        }

        public override void Serialize(IPacketSerializer innerPacketSerializer, Stream stream)
        {
            stream.Write32BitEncodedInt(SessionId);
        }

        public override void Deserialize(IPacketSerializer innerPacketSerializer, Stream stream)
        {
            SessionId = stream.Read32BitEncodedInt();
        }
    }

    public class SqSessionRebind : SessionPacket
    {
        public int SessionId;
        public int LineIndex;
        public int ClientMessageAck; // 클라이언트가 마지막으로 받은 Message index

        public override SessionPacketType PacketType => SessionPacketType.SqSessionRebind;

        public override int EstimateLength(IPacketSerializer innerPacketSerializer)
        {
            return
                _maxLengthOf7BitEncodedInt +
                _maxLengthOf7BitEncodedInt +
                _maxLengthOf7BitEncodedInt;
        }

        public override void Serialize(IPacketSerializer innerPacketSerializer, Stream stream)
        {
            stream.Write32BitEncodedInt(SessionId);
            stream.Write32BitEncodedInt(LineIndex);
            stream.Write32BitEncodedInt(ClientMessageAck);
        }

        public override void Deserialize(IPacketSerializer innerPacketSerializer, Stream stream)
        {
            SessionId = stream.Read32BitEncodedInt();
            LineIndex = stream.Read32BitEncodedInt();
            ClientMessageAck = stream.Read32BitEncodedInt();
        }
    }

    public class SrSessionRebind : SessionPacket
    {
        public int ServerMessageAck; // 서버가 마지막으로 받은 message index

        public override SessionPacketType PacketType => SessionPacketType.SrSessionRebind;

        public override int EstimateLength(IPacketSerializer innerPacketSerializer)
        {
            return _maxLengthOf7BitEncodedInt;
        }

        public override void Serialize(IPacketSerializer innerPacketSerializer, Stream stream)
        {
            stream.Write32BitEncodedInt(ServerMessageAck);
        }

        public override void Deserialize(IPacketSerializer innerPacketSerializer, Stream stream)
        {
            ServerMessageAck = stream.Read32BitEncodedInt();
        }
    }

    public class ScSessionAck : SessionPacket
    {
        public int MessageAck; // 송신자측이 받은 마지막으로 받은 MessageId

        public override SessionPacketType PacketType => SessionPacketType.ScSessionAck;

        public override int EstimateLength(IPacketSerializer innerPacketSerializer)
        {
            return _maxLengthOf7BitEncodedInt;
        }

        public override void Serialize(IPacketSerializer innerPacketSerializer, Stream stream)
        {
            stream.Write32BitEncodedInt(MessageAck);
        }

        public override void Deserialize(IPacketSerializer innerPacketSerializer, Stream stream)
        {
            MessageAck = stream.Read32BitEncodedInt();
        }
    }

    public abstract class SessionReliablePacket : SessionPacket
    {
        public int MessageId;
        public int MessageAck; // 송신자측이 받은 마지막으로 받은 MessageId

        public override int EstimateLength(IPacketSerializer innerPacketSerializer)
        {
            return
                _maxLengthOf7BitEncodedInt +
                _maxLengthOf7BitEncodedInt;
        }

        public override void Serialize(IPacketSerializer innerPacketSerializer, Stream stream)
        {
            stream.Write32BitEncodedInt(MessageId);
            stream.Write32BitEncodedInt(MessageAck);
        }

        public override void Deserialize(IPacketSerializer innerPacketSerializer, Stream stream)
        {
            MessageId = stream.Read32BitEncodedInt();
            MessageAck = stream.Read32BitEncodedInt();
        }
    }

    public class ScSessionInnerPacket : SessionReliablePacket
    {
        public Packet InnerPacket;

        public override SessionPacketType PacketType => SessionPacketType.ScSessionInnerPacket;

        public override int EstimateLength(IPacketSerializer innerPacketSerializer)
        {
            var innerPacketLength = innerPacketSerializer.EstimateLength(InnerPacket);
            if (innerPacketLength == 0)
            {
                return 0;
            }

            return
                base.EstimateLength(innerPacketSerializer)
                + innerPacketLength;
        }

        public override void Serialize(IPacketSerializer innerPacketSerializer, Stream stream)
        {
            base.Serialize(innerPacketSerializer, stream);
            innerPacketSerializer.Serialize(stream, InnerPacket);
        }

        public override void Deserialize(IPacketSerializer innerPacketSerializer, Stream stream)
        {
            base.Deserialize(innerPacketSerializer, stream);
            InnerPacket = innerPacketSerializer.Deserialize(stream) as Packet;
        }
    }

    public class ScSessionFin : SessionReliablePacket
    {
        public override SessionPacketType PacketType => SessionPacketType.ScSessionFin;

        public override int EstimateLength(IPacketSerializer innerPacketSerializer)
        {
            return base.EstimateLength(innerPacketSerializer);
        }

        public override void Serialize(IPacketSerializer innerPacketSerializer, Stream stream)
        {
            base.Serialize(innerPacketSerializer, stream);
        }

        public override void Deserialize(IPacketSerializer innerPacketSerializer, Stream stream)
        {
            base.Deserialize(innerPacketSerializer, stream);
        }
    }

    public class ScSessionFinAck : SessionReliablePacket
    {
        public override SessionPacketType PacketType => SessionPacketType.ScSessionFinAck;

        public override int EstimateLength(IPacketSerializer innerPacketSerializer)
        {
            return base.EstimateLength(innerPacketSerializer);
        }

        public override void Serialize(IPacketSerializer innerPacketSerializer, Stream stream)
        {
            base.Serialize(innerPacketSerializer, stream);
        }

        public override void Deserialize(IPacketSerializer innerPacketSerializer, Stream stream)
        {
            base.Deserialize(innerPacketSerializer, stream);
        }
    }

    public class SqEcho : SessionPacket
    {
        public int Ticks;

        public override SessionPacketType PacketType => SessionPacketType.SqEcho;

        public override int EstimateLength(IPacketSerializer innerPacketSerializer)
        {
            return _maxLengthOf7BitEncodedInt;
        }

        public override void Serialize(IPacketSerializer innerPacketSerializer, Stream stream)
        {
            stream.Write32BitEncodedInt(Ticks);
        }

        public override void Deserialize(IPacketSerializer innerPacketSerializer, Stream stream)
        {
            Ticks = stream.Read32BitEncodedInt();
        }
    }

    public class SrEcho : SessionPacket
    {
        public int Ticks;

        public override SessionPacketType PacketType => SessionPacketType.SrEcho;

        public override int EstimateLength(IPacketSerializer innerPacketSerializer)
        {
            return _maxLengthOf7BitEncodedInt;
        }

        public override void Serialize(IPacketSerializer innerPacketSerializer, Stream stream)
        {
            stream.Write32BitEncodedInt(Ticks);
        }

        public override void Deserialize(IPacketSerializer innerPacketSerializer, Stream stream)
        {
            Ticks = stream.Read32BitEncodedInt();
        }
    }
}
