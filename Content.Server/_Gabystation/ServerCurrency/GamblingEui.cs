using Content.Server.EUI;
using Content.Shared.Eui;
using Content.Shared._Gabystation.ServerCurrency.Gambling;

namespace Content.Server._Gabystation.ServerCurrence
{
    public sealed class GamblingEui : BaseEui
    {

        public GamblingEui()
        {
            IoCManager.InjectDependencies(this);
        }

        public override void Opened()
        {
            StateDirty();
        }

        public override EuiStateBase GetNewState()
        {
            return new GamblingEuiState();
        }


        public override void HandleMessage(EuiMessageBase msg)
        {
            base.HandleMessage(msg);
        }
    }
}
