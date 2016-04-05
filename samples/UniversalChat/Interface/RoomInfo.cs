﻿using System;
using System.Collections.Generic;
using ProtoBuf;

namespace UniversalChat.Interface
{
    [ProtoContract]
    public class RoomInfo
    {
        [ProtoMember(1)] public string Name;
        [ProtoMember(2)] public List<string> Users;
        [ProtoMember(3)] public List<ChatItem> History;
    }
}
