using Content.Server._Gabystation.ServerCurrence;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Gabystation.ServerCurrency.Commands
{
    [AnyCommand]
    public sealed class CurrencyUiCommand : IConsoleCommand
    {
        public string Command => "gamblingui";

        public string Description => "Let's go gambling!";

        public string Help => $"{Command}";

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            var player = shell.Player;
            if (player == null)
            {
                shell.WriteLine("This does not work from the server console.");
                return;
            }

            var eui = IoCManager.Resolve<EuiManager>();
            var ui = new GamblingEui();
            eui.OpenEui(ui, player);
        }
    }
}
