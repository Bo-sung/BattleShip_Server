using BattleShip.Common;

namespace BattleShip.GameSession.Game
{
    public enum GamePhase
    {
        WaitingPlacement,
        SkillSelection,  // StarBattle only
        InProgress,
        GameOver
    }

    public class GameState
    {
        public GamePhase Phase { get; private set; } = GamePhase.WaitingPlacement;
        public int CurrentTurn { get; private set; } = -1;
        public bool[] PlacementDone { get; } = new bool[2];
        public Board[] Boards { get; }

        // StarBattle mode fields
        private readonly byte _gameMode;
        private byte[] _mana = new byte[2];  // Mana per player (0-10)
        private byte[][] _selectedSkills = new byte[2][];  // 2 selected skills per player
        private Dictionary<(int playerIndex, byte shipType, int turnEnd), bool> _activeShields = new();  // (player, ship, turnExpires) -> active
        private HashSet<(int x, int y, int turnEnd)> _residualZones = new();  // (x, y, turnExpires)
        private int _currentGameTurn = 0;  // Game turn counter for tracking expiration

        public GameState(GameRuleConfig config)
        {
            Boards = new[] { new Board(config), new Board(config) };
            _gameMode = config.GameMode;

            // Initialize selected skills arrays
            _selectedSkills[0] = new byte[2];
            _selectedSkills[1] = new byte[2];
        }

        public bool SetPlacementDone(int playerIndex)
        {
            PlacementDone[playerIndex] = true;

            if (PlacementDone[0] && PlacementDone[1])
            {
                // For StarBattle mode, move to skill selection phase
                if (_gameMode == 2)  // StarBattle
                {
                    Phase = GamePhase.SkillSelection;
                }
                else
                {
                    Phase = GamePhase.InProgress;
                    CurrentTurn = new Random().Next(0, 2);
                }
                return true;
            }
            return false;
        }

        public void SetSkillsSelected(int playerIndex, byte skill1, byte skill2)
        {
            _selectedSkills[playerIndex][0] = skill1;
            _selectedSkills[playerIndex][1] = skill2;
        }

        public bool AreAllSkillsSelected()
        {
            return _selectedSkills[0][0] != 0 && _selectedSkills[1][0] != 0;
        }

        public void StartGameFromSkillSelection()
        {
            Phase = GamePhase.InProgress;
            CurrentTurn = new Random().Next(0, 2);
        }

        public bool IsMyTurn(int playerIndex) => CurrentTurn == playerIndex;

        public (byte result, string sunkShipName, bool isGameOver) ProcessAttack(int attackerIndex, byte x, byte y)
        {
            int defenderIndex = 1 - attackerIndex;

            // Check if position is blocked by shield (StarBattle only)
            if (_gameMode == 2)
            {
                var (shieldResult, _) = Boards[defenderIndex].GetShipAt(x, y);
                if (shieldResult != null && IsShieldActive(defenderIndex, shieldResult.Type))
                {
                    // Shield blocks the attack
                    return (4, "", false);  // Result 4 = ShieldBlocked
                }
            }

            var (result, sunkShipName) = Boards[defenderIndex].Attack(x, y);

            // Gain mana on successful attack (StarBattle only)
            if (_gameMode == 2 && (result == 1 || result == 2))
            {
                AddMana(attackerIndex, 1);
            }

            bool isGameOver = Boards[defenderIndex].IsAllSunk();

            if (isGameOver)
                Phase = GamePhase.GameOver;
            else
                CurrentTurn = 1 - CurrentTurn;

            _currentGameTurn++;
            return (result, sunkShipName, isGameOver);
        }

        public (bool success, string message) ProcessMove(int playerIndex, byte shipType, sbyte dirX, sbyte dirY)
        {
            if (_gameMode != 2)  // Only for StarBattle
                return (false, "Game mode does not support movement");

            var board = Boards[playerIndex];
            var (ship, cells) = board.GetShipWithCells(shipType);

            if (ship == null)
                return (false, "Ship not found");

            // Check if ship can move (cannot move if damaged)
            if (!CanShipMove(playerIndex, shipType))
                return (false, "Ship is damaged and cannot move");

            // Try to move ship
            var newCells = cells.Select(c => ((byte)(c.x + dirX), (byte)(c.y + dirY))).ToList();

            if (!board.CanPlaceShip(newCells))
                return (false, "Cannot move ship to that location");

            // Perform move
            board.MoveShip(shipType, newCells);

            // Move consumes a turn - switch turns
            CurrentTurn = 1 - CurrentTurn;
            _currentGameTurn++;

            return (true, "");
        }

        public (byte skillType, List<(byte x, byte y, byte result, string sunkShipName)> results, bool success) ProcessSkill(
            int playerIndex, byte skillType, byte targetX, byte targetY, byte shipType)
        {
            if (_gameMode != 2)  // Only for StarBattle
                return (0, new List<(byte, byte, byte, string)>(), false);

            int defenderIndex = 1 - playerIndex;

            // Determine if this is one of the player's selected skills
            if (skillType != _selectedSkills[playerIndex][0] && skillType != _selectedSkills[playerIndex][1])
                return (skillType, new List<(byte, byte, byte, string)>(), false);

            switch (skillType)
            {
                case 1:  // Range Attack
                    return ProcessSkillRangeAttack(playerIndex, defenderIndex, targetX, targetY);

                case 2:  // Shield
                    return ProcessSkillShield(playerIndex, shipType);

                case 3:  // Repair
                    return ProcessSkillRepair(playerIndex, shipType);

                case 4:  // Movement
                    return ProcessSkillMovement(playerIndex, shipType, targetX, targetY);

                case 5:  // Freeze
                    return ProcessSkillFreeze(playerIndex, targetX, targetY);

                case 6:  // Recovery
                    return ProcessSkillRecovery(playerIndex);

                default:
                    return (skillType, new List<(byte, byte, byte, string)>(), false);
            }
        }

        private (byte skillType, List<(byte x, byte y, byte result, string sunkShipName)> results, bool success)
            ProcessSkillRangeAttack(int attackerIndex, int defenderIndex, byte centerX, byte centerY)
        {
            var results = new List<(byte x, byte y, byte result, string sunkShipName)>();
            int cost = 3;

            if (_mana[attackerIndex] < cost)
                return (1, new List<(byte, byte, byte, string)>(), false);

            // 3x3 area attack
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    int x = centerX + dx;
                    int y = centerY + dy;

                    if (x < 0 || x >= Boards[defenderIndex].BoardSize || y < 0 || y >= Boards[defenderIndex].BoardSize)
                        continue;

                    var (ship, _) = Boards[defenderIndex].GetShipAt((byte)x, (byte)y);

                    // Check shield first
                    if (ship != null && IsShieldActive(defenderIndex, ship.Type))
                    {
                        results.Add(((byte)x, (byte)y, 4, ""));
                        continue;
                    }

                    var (result, sunkShipName) = Boards[defenderIndex].Attack((byte)x, (byte)y);
                    results.Add(((byte)x, (byte)y, result, sunkShipName));

                    // Add mana on hit
                    if (result == 1 || result == 2)
                        AddMana(attackerIndex, 1);
                }
            }

            SubtractMana(attackerIndex, cost);

            // Range attack consumes a turn - switch turns
            CurrentTurn = 1 - CurrentTurn;
            _currentGameTurn++;

            return (1, results, true);
        }

        private (byte skillType, List<(byte x, byte y, byte result, string sunkShipName)> results, bool success)
            ProcessSkillShield(int playerIndex, byte shipType)
        {
            int cost = 2;

            if (_mana[playerIndex] < cost)
                return (2, new List<(byte, byte, byte, string)>(), false);

            // Apply shield for 2 turns
            _activeShields[(playerIndex, shipType, _currentGameTurn + 2)] = true;

            SubtractMana(playerIndex, cost);

            // Shield consumes a turn - switch turns
            CurrentTurn = 1 - CurrentTurn;
            _currentGameTurn++;

            return (2, new List<(byte, byte, byte, string)>(), true);
        }

        private (byte skillType, List<(byte x, byte y, byte result, string sunkShipName)> results, bool success)
            ProcessSkillRepair(int playerIndex, byte shipType)
        {
            int cost = 1;

            if (_mana[playerIndex] < cost)
                return (3, new List<(byte, byte, byte, string)>(), false);

            var (ship, _) = Boards[playerIndex].GetShipWithCells(shipType);

            if (ship != null && ship.HitCount > 0)
            {
                ship.HitCount--;
                SubtractMana(playerIndex, cost);

                // Repair consumes a turn - switch turns
                CurrentTurn = 1 - CurrentTurn;
                _currentGameTurn++;

                return (3, new List<(byte, byte, byte, string)>(), true);
            }

            return (3, new List<(byte, byte, byte, string)>(), false);
        }

        private (byte skillType, List<(byte x, byte y, byte result, string sunkShipName)> results, bool success)
            ProcessSkillMovement(int playerIndex, byte shipType, byte dirX, byte dirY)
        {
            int cost = 1;

            if (_mana[playerIndex] < cost)
                return (4, new List<(byte, byte, byte, string)>(), false);

            // Note: ProcessMove already handles turn switching, so we don't do it again here
            var result = ProcessMove(playerIndex, shipType, (sbyte)dirX, (sbyte)dirY);

            if (result.success)
            {
                SubtractMana(playerIndex, cost);
                return (4, new List<(byte, byte, byte, string)>(), true);
            }

            return (4, new List<(byte, byte, byte, string)>(), false);
        }

        private (byte skillType, List<(byte x, byte y, byte result, string sunkShipName)> results, bool success)
            ProcessSkillFreeze(int playerIndex, byte centerX, byte centerY)
        {
            int cost = 2;

            if (_mana[playerIndex] < cost)
                return (5, new List<(byte, byte, byte, string)>(), false);

            // Create residual zone for 3 turns
            _residualZones.Add((centerX, centerY, _currentGameTurn + 3));

            SubtractMana(playerIndex, cost);

            // Freeze consumes a turn - switch turns
            CurrentTurn = 1 - CurrentTurn;
            _currentGameTurn++;

            return (5, new List<(byte, byte, byte, string)>(), true);
        }

        private (byte skillType, List<(byte x, byte y, byte result, string sunkShipName)> results, bool success)
            ProcessSkillRecovery(int playerIndex)
        {
            int cost = 1;

            if (_mana[playerIndex] < cost)
                return (6, new List<(byte, byte, byte, string)>(), false);

            // Restore 3 mana
            AddMana(playerIndex, 3);
            SubtractMana(playerIndex, cost);

            // Recovery consumes a turn - switch turns
            CurrentTurn = 1 - CurrentTurn;
            _currentGameTurn++;

            return (6, new List<(byte, byte, byte, string)>(), true);
        }

        public bool CanShipMove(int playerIndex, byte shipType)
        {
            if (_gameMode != 2)
                return false;

            var (ship, _) = Boards[playerIndex].GetShipWithCells(shipType);
            if (ship == null)
                return false;

            // Cannot move if ship is damaged (HitCount > 0)
            return ship.HitCount == 0;
        }

        public bool IsShieldActive(int playerIndex, byte shipType)
        {
            // Clean up expired shields
            var expiredKeys = _activeShields
                .Where(kvp => kvp.Key.turnEnd <= _currentGameTurn)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
                _activeShields.Remove(key);

            // Check if shield is active
            return _activeShields.Any(kvp =>
                kvp.Key.playerIndex == playerIndex &&
                kvp.Key.shipType == shipType &&
                kvp.Key.turnEnd > _currentGameTurn &&
                kvp.Value);
        }

        public bool IsResidualZone(byte x, byte y)
        {
            // Clean up expired zones
            var expiredZones = _residualZones
                .Where(z => z.turnEnd <= _currentGameTurn)
                .ToList();

            foreach (var zone in expiredZones)
                _residualZones.Remove(zone);

            // Check if position has residual effect
            return _residualZones.Any(z => z.x == x && z.y == y && z.turnEnd > _currentGameTurn);
        }

        public void AddMana(int playerIndex, byte amount)
        {
            _mana[playerIndex] = Math.Min((byte)10, (byte)(_mana[playerIndex] + amount));
        }

        public void SubtractMana(int playerIndex, int amount)
        {
            _mana[playerIndex] = (byte)Math.Max(0, _mana[playerIndex] - amount);
        }

        public byte GetMana(int playerIndex) => _mana[playerIndex];
    }
}
