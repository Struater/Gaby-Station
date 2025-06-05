using Content.Client.Eui;
using Content.Shared.Administration;
using Robust.Client.UserInterface.Controls;
using Content.Shared._Gabystation.ServerCurrency.Gambling;

namespace Content.Client._Gabystation.ServerCurrency.UI
{
    public class GamblingEui : BaseEui
    {
        private readonly GamblingWindow _window;
        public GamblingEui()
        {
            _window = new GamblingWindow();
            _window.OnClose += () => SendMessage(new GamblingEuiMsg.Close());
        }
        public override void Opened()
        {
            _window.OpenCentered();
        }
        public override void Closed()
        {
            _window.Close();
        }
    }
}
