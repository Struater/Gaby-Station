using Content.Client.Eui;
using Content.Shared.Administration;
using Robust.Client.UserInterface.Controls;
using Content.Shared._Gabystation.ServerCurrency.Gambling;
using Content.Shared.Eui;
using Robust.Client.UserInterface;
using Robust.Client.Console;

namespace Content.Client._Gabystation.ServerCurrency.UI
{
    public class GamblingEui : BaseEui
    {
        [Dependency] private readonly IUserInterfaceManager _userInterface = default!;
        [Dependency] private readonly IClientConsoleHost _consoleHost = default!;
        private readonly GamblingWindow _window;
        public GamblingEui()
        {
            _window = new GamblingWindow();
            _window.OnClose += () => SendMessage(new GamblingEuiMsg.Close());
            _window.OnPlay += (amount, option) => SendMessage(new GamblingEuiMsg.Play { PlayAmount = amount, PlayOption = option });
        }
        public override void Opened()
        {
            _window.OpenCentered();
        }
        public override void Closed()
        {
            _window.Close();
        }
        public override void HandleMessage(EuiMessageBase msg)
        {
            base.HandleMessage(msg);

            if (msg is GamblingEuiMsg.Restart _)
                _window.SetupLobbyState();

            if (msg is GamblingEuiMsg.Result result)
                _window.ShowResult(result.Won);

            if (msg is GamblingEuiMsg.Warning warning)
                {
                    return;

                    _userInterface.Popup(warning.Message, warning.Tittle, false);
                    if (warning.Restart is not true)
                        return;

                    _window.Close();
                    _consoleHost.ExecuteCommand("gambleui");
                }
        }
    }
}
