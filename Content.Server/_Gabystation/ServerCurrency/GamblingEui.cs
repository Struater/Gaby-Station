using Content.Server.EUI;
using Content.Shared.Eui;
using Content.Shared._Gabystation.ServerCurrency.Gambling;
using Content.Server._durkcode.ServerCurrency;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._Gabystation.ServerCurrence
{
    public sealed class GamblingEui : BaseEui
    {
        [Dependency] private readonly ServerCurrencyManager _currencyMan = default!;
        [Dependency] private readonly IRobustRandom _rand = default!;
        private ISawmill _sawmill = default!; // Goobstation

        public GamblingEui()
        {
            IoCManager.InjectDependencies(this);
            _sawmill = Logger.GetSawmill("gambling");
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

            switch (msg)
            {
                case GamblingEuiMsg.Play play:
                    //BuyToken(Buy.TokenId, Player);
                    GambleDouble(play.PlayAmount, play.PlayOption);
                    StateDirty();
                    break;
            }
        }

        // Play option = red, black or white (ultra rare)
        public void GambleDouble(int amount, string option)
        {
            if (amount <= 0 || !_currencyMan.CanAfford(Player.UserId, amount, out _))
                return;

            _currencyMan.RemoveCurrency(Player.UserId, amount);

            // get random number
            double roll = _rand.NextDouble();

            var win = false;
            var multiplier = 0;

            if (option == "White")
            {
                if (roll < 0.10) // 10%
                {
                    win = true;
                    multiplier = 10;
                }
            }
            else if (option == "Red")
            {
                if (roll < 0.45) // 45%
                {
                    win = true;
                    multiplier = 2;
                }
            }
            else if (option == "Black")
            {
                if (roll >= 0.45 && roll < 0.90) // 45%
                {
                    win = true;
                    multiplier = 2;
                }
            }
            else
            {
                _currencyMan.AddCurrency(Player.UserId, amount);
                _sawmill.Error($"{Player.Name} has gambled worng! - {option} - {amount} - {roll} - {multiplier}");
                SendMessage(new GamblingEuiMsg.Warning
                {
                    Message = "Something went worng. Dont worry! Your money is safe.", Tittle = "Error", Restart = true
                });
                return;
            }

            if (win)
            {
                var winnings = amount * multiplier;
                _currencyMan.AddCurrency(Player.UserId, winnings);
                // Pode mandar mensagem pro jogador aqui
                _sawmill.Info($"{Player.Name} gambled {amount} in {option} and won {winnings}!");

                SendMessage(new GamblingEuiMsg.Restart());

            }
            else
            {
                // player lost
                _sawmill.Info($"{Player.Name} gambled {amount} in {option} and lose.");

                SendMessage(new GamblingEuiMsg.Restart());

            }

            StateDirty();
        }
    }
}
