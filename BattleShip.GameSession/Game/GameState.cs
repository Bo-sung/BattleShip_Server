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
        public int CurrentTurn { get; private set; } = -1;  // 0 or 1, -1=미결정
        public bool[] PlacementDone { get; } = new bool[2];
        public Board[] Boards { get; } = { new Board(), new Board() };

        // 배치 완료 처리. 양쪽 다 완료되면 true 반환
        public bool SetPlacementDone(int playerIndex)
        {
            PlacementDone[playerIndex] = true;

            if (PlacementDone[0] && PlacementDone[1])
            {
                Phase = GamePhase.InProgress;
                CurrentTurn = new Random().Next(0, 2);  // 선공 랜덤
                return true;
            }
            return false;
        }

        public bool IsMyTurn(int playerIndex) => CurrentTurn == playerIndex;

        // 공격 처리. 반환값: result, sunkShipName, isGameOver
        public (byte result, string sunkShipName, bool isGameOver) ProcessAttack(int attackerIndex, byte x, byte y)
        {
            int defenderIndex = 1 - attackerIndex;
            var (result, sunkShipName) = Boards[defenderIndex].Attack(x, y);

            bool isGameOver = Boards[defenderIndex].IsAllSunk();

            if (isGameOver)
                Phase = GamePhase.GameOver;
            else
                CurrentTurn = 1 - CurrentTurn;  // 표준 규칙: 항상 턴 전환

            return (result, sunkShipName, isGameOver);
        }
    }
}
