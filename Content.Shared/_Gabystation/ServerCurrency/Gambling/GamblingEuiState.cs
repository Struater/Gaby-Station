using System;
using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Gabystation.ServerCurrency.Gambling
{
    [Serializable, NetSerializable]
    public sealed class GamblingEuiState : EuiStateBase { }
    public static class GamblingEuiMsg
    {
        [Serializable, NetSerializable]
        public sealed class Close : EuiMessageBase
        {
        }

        /* exemplo de mensagem
        [Serializable, NetSerializable]
        public sealed class DoAnnounce : EuiMessageBase
        {
            public bool CloseAfter;
            public string Announcer = default!;
            public string Announcement = default!;
            public AdminAnnounceType AnnounceType;
        }*/
    }
}
