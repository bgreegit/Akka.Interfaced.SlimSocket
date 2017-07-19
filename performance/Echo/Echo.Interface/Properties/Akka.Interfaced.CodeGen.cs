﻿// ------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by Akka.Interfaced CodeGenerator.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
// ------------------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Akka.Interfaced;
using Akka.Actor;
using ProtoBuf;
using TypeAlias;
using System.ComponentModel;

#region Echo.Interface.IEcho

namespace Echo.Interface
{
    [PayloadTable(typeof(IEcho), PayloadTableKind.Request)]
    public static class IEcho_PayloadTable
    {
        public static Type[,] GetPayloadTypes()
        {
            return new Type[,] {
                { typeof(Echo_Invoke), typeof(Echo_Return) },
            };
        }

        [ProtoContract, TypeAlias]
        public class Echo_Invoke
            : IInterfacedPayload, IAsyncInvokable
        {
            [ProtoMember(1)] public System.Byte[] data;

            public Type GetInterfaceType()
            {
                return typeof(IEcho);
            }

            public async Task<IValueGetable> InvokeAsync(object __target)
            {
                var __v = await ((IEcho)__target).Echo(data);
                return (IValueGetable)(new Echo_Return { v = __v });
            }
        }

        [ProtoContract, TypeAlias]
        public class Echo_Return
            : IInterfacedPayload, IValueGetable
        {
            [ProtoMember(1)] public System.Byte[] v;

            public Type GetInterfaceType()
            {
                return typeof(IEcho);
            }

            public object Value
            {
                get { return v; }
            }
        }
    }

    public interface IEcho_NoReply
    {
        void Echo(System.Byte[] data);
    }

    public class EchoRef : InterfacedActorRef, IEcho, IEcho_NoReply
    {
        public override Type InterfaceType => typeof(IEcho);

        public EchoRef() : base(null)
        {
        }

        public EchoRef(IRequestTarget target) : base(target)
        {
        }

        public EchoRef(IRequestTarget target, IRequestWaiter requestWaiter, TimeSpan? timeout = null) : base(target, requestWaiter, timeout)
        {
        }

        public IEcho_NoReply WithNoReply()
        {
            return this;
        }

        public EchoRef WithRequestWaiter(IRequestWaiter requestWaiter)
        {
            return new EchoRef(Target, requestWaiter, Timeout);
        }

        public EchoRef WithTimeout(TimeSpan? timeout)
        {
            return new EchoRef(Target, RequestWaiter, timeout);
        }

        public Task<System.Byte[]> Echo(System.Byte[] data)
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IEcho_PayloadTable.Echo_Invoke { data = data }
            };
            return SendRequestAndReceive<System.Byte[]>(requestMessage);
        }

        void IEcho_NoReply.Echo(System.Byte[] data)
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IEcho_PayloadTable.Echo_Invoke { data = data }
            };
            SendRequest(requestMessage);
        }
    }

    [ProtoContract]
    public class SurrogateForIEcho
    {
        [ProtoMember(1)] public IRequestTarget Target;

        [ProtoConverter]
        public static SurrogateForIEcho Convert(IEcho value)
        {
            if (value == null) return null;
            return new SurrogateForIEcho { Target = ((EchoRef)value).Target };
        }

        [ProtoConverter]
        public static IEcho Convert(SurrogateForIEcho value)
        {
            if (value == null) return null;
            return new EchoRef(value.Target);
        }
    }

    [AlternativeInterface(typeof(IEcho))]
    public interface IEchoSync : IInterfacedActorSync
    {
        System.Byte[] Echo(System.Byte[] data);
    }
}

#endregion
