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

        [Serializable, NetSerializable]
        public sealed class Play : EuiMessageBase
        {
            //public ProtoId<GamblingGameModePrototype> GameMode;
            public required int PlayAmount;
            public required string PlayOption;
        }

        [Serializable, NetSerializable]
        public sealed class Restart : EuiMessageBase
        {
        }

        [Serializable, NetSerializable]
        public sealed class Result : EuiMessageBase
        {
            public bool Won = false;
        }

        [Serializable, NetSerializable]
        public sealed class Warning : EuiMessageBase
        {
            public required string Tittle;
            public required string Message;
            public bool? Restart;
        }
    }
}
