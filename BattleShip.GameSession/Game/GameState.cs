using BattleShip.Common;

namespace BattleShip.GameSession.Game
{
    public enum GamePhase
    {
        WaitingPlacement,
        InProgress,
        GameOver
    }

    public class GameState
    {
        public GamePhase Phase { get; private set; } = GamePhase.WaitingPlacement;
        public int CurrentTurn { get; private set; } = -1;
        public bool[] PlacementDone { get; } = new bool[2];
        public Board[] Boards { get; }

        public GameState(GameRuleConfig config)
        {
            Boards = new[] { new Board(config), new Board(config) };
        }

        public bool SetPlacementDone(int playerIndex)
        {
            PlacementDone[playerIndex] = true;

            if (PlacementDone[0] && PlacementDone[1])
            {
                Phase = GamePhase.InProgress;
                CurrentTurn = new Random().Next(0, 2);
                return true;
            }
            return false;
        }

        public bool IsMyTurn(int playerIndex) => CurrentTurn == playerIndex;

        public (byte result, string sunkShipName, bool isGameOver) ProcessAttack(int attackerIndex, byte x, byte y)
        {
            int defenderIndex = 1 - attackerIndex;
            var (result, sunkShipName) = Boards[defenderIndex].Attack(x, y);

            bool isGameOver = Boards[defenderIndex].IsAllSunk();

            if (isGameOver)
                Phase = GamePhase.GameOver;
            else
                CurrentTurn = 1 - CurrentTurn;

            return (result, sunkShipName, isGameOver);
        }
    }
}
