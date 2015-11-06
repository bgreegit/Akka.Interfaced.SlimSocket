// ------------------------------------------------------------------------------
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

#region UniversalChat.Interface.IOccupant

namespace UniversalChat.Interface
{
    [PayloadTableForInterfacedActor(typeof(IOccupant))]
    public static class IOccupant_PayloadTable
    {
        public static Type[,] GetPayloadTypes()
        {
            return new Type[,]
            {
                {typeof(GetHistory_Invoke), typeof(GetHistory_Return)},
                {typeof(Invite_Invoke), null},
                {typeof(Say_Invoke), null},
            };
        }

        public class GetHistory_Invoke : IInterfacedPayload, ITagOverridable, IAsyncInvokable
        {
            public Type GetInterfaceType() { return typeof(IOccupant); }

            public void SetTag(object value) { }

            public Task<IValueGetable> InvokeAsync(object target)
            {
                return null;
            }
        }

        public class GetHistory_Return : IInterfacedPayload, IValueGetable
        {
            public System.Collections.Generic.List<UniversalChat.Interface.ChatItem> v;

            public Type GetInterfaceType() { return typeof(IOccupant); }

            public object Value { get { return v; } }
        }

        public class Invite_Invoke : IInterfacedPayload, ITagOverridable, IAsyncInvokable
        {
            public System.String targetUserId;
            public System.String senderUserId;

            public Type GetInterfaceType() { return typeof(IOccupant); }

            public void SetTag(object value) { senderUserId = (System.String)value; }

            public Task<IValueGetable> InvokeAsync(object target)
            {
                return null;
            }
        }

        public class Say_Invoke : IInterfacedPayload, ITagOverridable, IAsyncInvokable
        {
            public System.String msg;
            public System.String senderUserId;

            public Type GetInterfaceType() { return typeof(IOccupant); }

            public void SetTag(object value) { senderUserId = (System.String)value; }

            public Task<IValueGetable> InvokeAsync(object target)
            {
                return null;
            }
        }
    }

    public interface IOccupant_NoReply
    {
        void GetHistory();
        void Invite(System.String targetUserId, System.String senderUserId = null);
        void Say(System.String msg, System.String senderUserId = null);
    }

    public class OccupantRef : InterfacedActorRef, IOccupant, IOccupant_NoReply
    {
        public OccupantRef(IActorRef actor, IRequestWaiter requestWaiter, TimeSpan? timeout)
            : base(actor, requestWaiter, timeout)
        {
        }

        public IOccupant_NoReply WithNoReply()
        {
            return this;
        }

        public OccupantRef WithRequestWaiter(IRequestWaiter requestWaiter)
        {
            return new OccupantRef(Actor, requestWaiter, Timeout);
        }

        public OccupantRef WithTimeout(TimeSpan? timeout)
        {
            return new OccupantRef(Actor, RequestWaiter, timeout);
        }

        public Task<System.Collections.Generic.List<UniversalChat.Interface.ChatItem>> GetHistory()
        {
            var requestMessage = new RequestMessage
            {
                InvokePayload = new IOccupant_PayloadTable.GetHistory_Invoke {  }
            };
            return SendRequestAndReceive<System.Collections.Generic.List<UniversalChat.Interface.ChatItem>>(requestMessage);
        }

        public Task Invite(System.String targetUserId, System.String senderUserId = null)
        {
            var requestMessage = new RequestMessage
            {
                InvokePayload = new IOccupant_PayloadTable.Invite_Invoke { targetUserId = targetUserId, senderUserId = senderUserId }
            };
            return SendRequestAndWait(requestMessage);
        }

        public Task Say(System.String msg, System.String senderUserId = null)
        {
            var requestMessage = new RequestMessage
            {
                InvokePayload = new IOccupant_PayloadTable.Say_Invoke { msg = msg, senderUserId = senderUserId }
            };
            return SendRequestAndWait(requestMessage);
        }

        void IOccupant_NoReply.GetHistory()
        {
            var requestMessage = new RequestMessage
            {
                InvokePayload = new IOccupant_PayloadTable.GetHistory_Invoke {  }
            };
            SendRequest(requestMessage);
        }

        void IOccupant_NoReply.Invite(System.String targetUserId, System.String senderUserId)
        {
            var requestMessage = new RequestMessage
            {
                InvokePayload = new IOccupant_PayloadTable.Invite_Invoke { targetUserId = targetUserId, senderUserId = senderUserId }
            };
            SendRequest(requestMessage);
        }

        void IOccupant_NoReply.Say(System.String msg, System.String senderUserId)
        {
            var requestMessage = new RequestMessage
            {
                InvokePayload = new IOccupant_PayloadTable.Say_Invoke { msg = msg, senderUserId = senderUserId }
            };
            SendRequest(requestMessage);
        }
    }
}

#endregion

#region UniversalChat.Interface.IUser

namespace UniversalChat.Interface
{
    [PayloadTableForInterfacedActor(typeof(IUser))]
    public static class IUser_PayloadTable
    {
        public static Type[,] GetPayloadTypes()
        {
            return new Type[,]
            {
                {typeof(EnterRoom_Invoke), typeof(EnterRoom_Return)},
                {typeof(ExitFromRoom_Invoke), null},
                {typeof(GetId_Invoke), typeof(GetId_Return)},
                {typeof(GetRoomList_Invoke), typeof(GetRoomList_Return)},
                {typeof(Whisper_Invoke), null},
            };
        }

        public class EnterRoom_Invoke : IInterfacedPayload, IAsyncInvokable
        {
            public System.String name;
            public System.Int32 observerId;

            public Type GetInterfaceType() { return typeof(IUser); }

            public Task<IValueGetable> InvokeAsync(object target)
            {
                return null;
            }
        }

        public class EnterRoom_Return : IInterfacedPayload, IValueGetable
        {
            public System.Tuple<System.Int32, UniversalChat.Interface.RoomInfo> v;

            public Type GetInterfaceType() { return typeof(IUser); }

            public object Value { get { return v; } }
        }

        public class ExitFromRoom_Invoke : IInterfacedPayload, IAsyncInvokable
        {
            public System.String name;

            public Type GetInterfaceType() { return typeof(IUser); }

            public Task<IValueGetable> InvokeAsync(object target)
            {
                return null;
            }
        }

        public class GetId_Invoke : IInterfacedPayload, IAsyncInvokable
        {
            public Type GetInterfaceType() { return typeof(IUser); }

            public Task<IValueGetable> InvokeAsync(object target)
            {
                return null;
            }
        }

        public class GetId_Return : IInterfacedPayload, IValueGetable
        {
            public System.String v;

            public Type GetInterfaceType() { return typeof(IUser); }

            public object Value { get { return v; } }
        }

        public class GetRoomList_Invoke : IInterfacedPayload, IAsyncInvokable
        {
            public Type GetInterfaceType() { return typeof(IUser); }

            public Task<IValueGetable> InvokeAsync(object target)
            {
                return null;
            }
        }

        public class GetRoomList_Return : IInterfacedPayload, IValueGetable
        {
            public System.Collections.Generic.List<System.String> v;

            public Type GetInterfaceType() { return typeof(IUser); }

            public object Value { get { return v; } }
        }

        public class Whisper_Invoke : IInterfacedPayload, IAsyncInvokable
        {
            public System.String targetUserId;
            public System.String message;

            public Type GetInterfaceType() { return typeof(IUser); }

            public Task<IValueGetable> InvokeAsync(object target)
            {
                return null;
            }
        }
    }

    public interface IUser_NoReply
    {
        void EnterRoom(System.String name, System.Int32 observerId);
        void ExitFromRoom(System.String name);
        void GetId();
        void GetRoomList();
        void Whisper(System.String targetUserId, System.String message);
    }

    public class UserRef : InterfacedActorRef, IUser, IUser_NoReply
    {
        public UserRef(IActorRef actor, IRequestWaiter requestWaiter, TimeSpan? timeout)
            : base(actor, requestWaiter, timeout)
        {
        }

        public IUser_NoReply WithNoReply()
        {
            return this;
        }

        public UserRef WithRequestWaiter(IRequestWaiter requestWaiter)
        {
            return new UserRef(Actor, requestWaiter, Timeout);
        }

        public UserRef WithTimeout(TimeSpan? timeout)
        {
            return new UserRef(Actor, RequestWaiter, timeout);
        }

        public Task<System.Tuple<System.Int32, UniversalChat.Interface.RoomInfo>> EnterRoom(System.String name, System.Int32 observerId)
        {
            var requestMessage = new RequestMessage
            {
                InvokePayload = new IUser_PayloadTable.EnterRoom_Invoke { name = name, observerId = observerId }
            };
            return SendRequestAndReceive<System.Tuple<System.Int32, UniversalChat.Interface.RoomInfo>>(requestMessage);
        }

        public Task ExitFromRoom(System.String name)
        {
            var requestMessage = new RequestMessage
            {
                InvokePayload = new IUser_PayloadTable.ExitFromRoom_Invoke { name = name }
            };
            return SendRequestAndWait(requestMessage);
        }

        public Task<System.String> GetId()
        {
            var requestMessage = new RequestMessage
            {
                InvokePayload = new IUser_PayloadTable.GetId_Invoke {  }
            };
            return SendRequestAndReceive<System.String>(requestMessage);
        }

        public Task<System.Collections.Generic.List<System.String>> GetRoomList()
        {
            var requestMessage = new RequestMessage
            {
                InvokePayload = new IUser_PayloadTable.GetRoomList_Invoke {  }
            };
            return SendRequestAndReceive<System.Collections.Generic.List<System.String>>(requestMessage);
        }

        public Task Whisper(System.String targetUserId, System.String message)
        {
            var requestMessage = new RequestMessage
            {
                InvokePayload = new IUser_PayloadTable.Whisper_Invoke { targetUserId = targetUserId, message = message }
            };
            return SendRequestAndWait(requestMessage);
        }

        void IUser_NoReply.EnterRoom(System.String name, System.Int32 observerId)
        {
            var requestMessage = new RequestMessage
            {
                InvokePayload = new IUser_PayloadTable.EnterRoom_Invoke { name = name, observerId = observerId }
            };
            SendRequest(requestMessage);
        }

        void IUser_NoReply.ExitFromRoom(System.String name)
        {
            var requestMessage = new RequestMessage
            {
                InvokePayload = new IUser_PayloadTable.ExitFromRoom_Invoke { name = name }
            };
            SendRequest(requestMessage);
        }

        void IUser_NoReply.GetId()
        {
            var requestMessage = new RequestMessage
            {
                InvokePayload = new IUser_PayloadTable.GetId_Invoke {  }
            };
            SendRequest(requestMessage);
        }

        void IUser_NoReply.GetRoomList()
        {
            var requestMessage = new RequestMessage
            {
                InvokePayload = new IUser_PayloadTable.GetRoomList_Invoke {  }
            };
            SendRequest(requestMessage);
        }

        void IUser_NoReply.Whisper(System.String targetUserId, System.String message)
        {
            var requestMessage = new RequestMessage
            {
                InvokePayload = new IUser_PayloadTable.Whisper_Invoke { targetUserId = targetUserId, message = message }
            };
            SendRequest(requestMessage);
        }
    }
}

#endregion

#region UniversalChat.Interface.IUserLogin

namespace UniversalChat.Interface
{
    [PayloadTableForInterfacedActor(typeof(IUserLogin))]
    public static class IUserLogin_PayloadTable
    {
        public static Type[,] GetPayloadTypes()
        {
            return new Type[,]
            {
                {typeof(Login_Invoke), typeof(Login_Return)},
            };
        }

        public class Login_Invoke : IInterfacedPayload, IAsyncInvokable
        {
            public System.String id;
            public System.String password;
            public System.Int32 observerId;

            public Type GetInterfaceType() { return typeof(IUserLogin); }

            public Task<IValueGetable> InvokeAsync(object target)
            {
                return null;
            }
        }

        public class Login_Return : IInterfacedPayload, IValueGetable
        {
            public System.Int32 v;

            public Type GetInterfaceType() { return typeof(IUserLogin); }

            public object Value { get { return v; } }
        }
    }

    public interface IUserLogin_NoReply
    {
        void Login(System.String id, System.String password, System.Int32 observerId);
    }

    public class UserLoginRef : InterfacedActorRef, IUserLogin, IUserLogin_NoReply
    {
        public UserLoginRef(IActorRef actor, IRequestWaiter requestWaiter, TimeSpan? timeout)
            : base(actor, requestWaiter, timeout)
        {
        }

        public IUserLogin_NoReply WithNoReply()
        {
            return this;
        }

        public UserLoginRef WithRequestWaiter(IRequestWaiter requestWaiter)
        {
            return new UserLoginRef(Actor, requestWaiter, Timeout);
        }

        public UserLoginRef WithTimeout(TimeSpan? timeout)
        {
            return new UserLoginRef(Actor, RequestWaiter, timeout);
        }

        public Task<System.Int32> Login(System.String id, System.String password, System.Int32 observerId)
        {
            var requestMessage = new RequestMessage
            {
                InvokePayload = new IUserLogin_PayloadTable.Login_Invoke { id = id, password = password, observerId = observerId }
            };
            return SendRequestAndReceive<System.Int32>(requestMessage);
        }

        void IUserLogin_NoReply.Login(System.String id, System.String password, System.Int32 observerId)
        {
            var requestMessage = new RequestMessage
            {
                InvokePayload = new IUserLogin_PayloadTable.Login_Invoke { id = id, password = password, observerId = observerId }
            };
            SendRequest(requestMessage);
        }
    }
}

#endregion

#region UniversalChat.Interface.IUserMessasing

namespace UniversalChat.Interface
{
    [PayloadTableForInterfacedActor(typeof(IUserMessasing))]
    public static class IUserMessasing_PayloadTable
    {
        public static Type[,] GetPayloadTypes()
        {
            return new Type[,]
            {
                {typeof(Invite_Invoke), null},
                {typeof(Whisper_Invoke), null},
            };
        }

        public class Invite_Invoke : IInterfacedPayload, IAsyncInvokable
        {
            public System.String invitorUserId;
            public System.String roomName;

            public Type GetInterfaceType() { return typeof(IUserMessasing); }

            public Task<IValueGetable> InvokeAsync(object target)
            {
                return null;
            }
        }

        public class Whisper_Invoke : IInterfacedPayload, IAsyncInvokable
        {
            public UniversalChat.Interface.ChatItem chatItem;

            public Type GetInterfaceType() { return typeof(IUserMessasing); }

            public Task<IValueGetable> InvokeAsync(object target)
            {
                return null;
            }
        }
    }

    public interface IUserMessasing_NoReply
    {
        void Invite(System.String invitorUserId, System.String roomName);
        void Whisper(UniversalChat.Interface.ChatItem chatItem);
    }

    public class UserMessasingRef : InterfacedActorRef, IUserMessasing, IUserMessasing_NoReply
    {
        public UserMessasingRef(IActorRef actor, IRequestWaiter requestWaiter, TimeSpan? timeout)
            : base(actor, requestWaiter, timeout)
        {
        }

        public IUserMessasing_NoReply WithNoReply()
        {
            return this;
        }

        public UserMessasingRef WithRequestWaiter(IRequestWaiter requestWaiter)
        {
            return new UserMessasingRef(Actor, requestWaiter, Timeout);
        }

        public UserMessasingRef WithTimeout(TimeSpan? timeout)
        {
            return new UserMessasingRef(Actor, RequestWaiter, timeout);
        }

        public Task Invite(System.String invitorUserId, System.String roomName)
        {
            var requestMessage = new RequestMessage
            {
                InvokePayload = new IUserMessasing_PayloadTable.Invite_Invoke { invitorUserId = invitorUserId, roomName = roomName }
            };
            return SendRequestAndWait(requestMessage);
        }

        public Task Whisper(UniversalChat.Interface.ChatItem chatItem)
        {
            var requestMessage = new RequestMessage
            {
                InvokePayload = new IUserMessasing_PayloadTable.Whisper_Invoke { chatItem = chatItem }
            };
            return SendRequestAndWait(requestMessage);
        }

        void IUserMessasing_NoReply.Invite(System.String invitorUserId, System.String roomName)
        {
            var requestMessage = new RequestMessage
            {
                InvokePayload = new IUserMessasing_PayloadTable.Invite_Invoke { invitorUserId = invitorUserId, roomName = roomName }
            };
            SendRequest(requestMessage);
        }

        void IUserMessasing_NoReply.Whisper(UniversalChat.Interface.ChatItem chatItem)
        {
            var requestMessage = new RequestMessage
            {
                InvokePayload = new IUserMessasing_PayloadTable.Whisper_Invoke { chatItem = chatItem }
            };
            SendRequest(requestMessage);
        }
    }
}

#endregion

#region UniversalChat.Interface.IRoomObserver

namespace UniversalChat.Interface
{
    public static class IRoomObserver_PayloadTable
    {
        public class Enter_Invoke : IInvokable
        {
            public System.String userId;

            public void Invoke(object target)
            {
                ((IRoomObserver)target).Enter(userId);
            }
        }

        public class Exit_Invoke : IInvokable
        {
            public System.String userId;

            public void Invoke(object target)
            {
                ((IRoomObserver)target).Exit(userId);
            }
        }

        public class Say_Invoke : IInvokable
        {
            public UniversalChat.Interface.ChatItem chatItem;

            public void Invoke(object target)
            {
                ((IRoomObserver)target).Say(chatItem);
            }
        }
    }
}

#endregion

#region UniversalChat.Interface.IUserEventObserver

namespace UniversalChat.Interface
{
    public static class IUserEventObserver_PayloadTable
    {
        public class Whisper_Invoke : IInvokable
        {
            public UniversalChat.Interface.ChatItem chatItem;

            public void Invoke(object target)
            {
                ((IUserEventObserver)target).Whisper(chatItem);
            }
        }

        public class Invite_Invoke : IInvokable
        {
            public System.String invitorUserId;
            public System.String roomName;

            public void Invoke(object target)
            {
                ((IUserEventObserver)target).Invite(invitorUserId, roomName);
            }
        }
    }
}

#endregion