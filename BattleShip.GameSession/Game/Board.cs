using BattleShip.Common;
using BattleShip.Common.Packets.Game;

namespace BattleShip.GameSession.Game
{
    public class Board
    {
        private readonly int _boardSize;
        private readonly Dictionary<byte, ShipDefinition> _shipDefs;
        private readonly bool[,] _ships;
        private readonly bool[,] _hits;
        private readonly Ship?[,] _shipAt;
        private readonly List<Ship> _shipList = new List<Ship>();
        private readonly Dictionary<byte, List<(byte x, byte y)>> _shipCells = new();  // Track ship positions

        public int BoardSize => _boardSize;

        public Board(GameRuleConfig config)
        {
            _boardSize = config.BoardSize;
            _shipDefs = config.Ships.ToDictionary(s => s.Type);
            _ships = new bool[_boardSize, _boardSize];
            _hits = new bool[_boardSize, _boardSize];
            _shipAt = new Ship[_boardSize, _boardSize];
        }

        public bool PlaceShips(List<ShipPlacement> placements)
        {
            if (placements.Count != _shipDefs.Count)
                return false;

            var types = placements.Select(p => p.ShipType).Distinct().ToList();
            if (types.Count != _shipDefs.Count)
                return false;

            if (!types.All(t => _shipDefs.ContainsKey(t)))
                return false;

            var temp = new bool[_boardSize, _boardSize];

            foreach (var p in placements)
            {
                var def = _shipDefs[p.ShipType];
                var ship = Ship.Create(def);

                for (int i = 0; i < ship.Size; i++)
                {
                    int x = p.IsHorizontal ? p.X + i : p.X;
                    int y = p.IsHorizontal ? p.Y : p.Y + i;

                    if (x < 0 || x >= _boardSize || y < 0 || y >= _boardSize)
                        return false;

                    if (temp[x, y])
                        return false;

                    temp[x, y] = true;
                }
            }

            foreach (var p in placements)
            {
                var ship = Ship.Create(_shipDefs[p.ShipType]);
                _shipList.Add(ship);
                var cells = new List<(byte, byte)>();

                for (int i = 0; i < ship.Size; i++)
                {
                    int x = p.IsHorizontal ? p.X + i : p.X;
                    int y = p.IsHorizontal ? p.Y : p.Y + i;

                    _ships[x, y] = true;
                    _shipAt[x, y] = ship;
                    cells.Add(((byte)x, (byte)y));
                }

                _shipCells[p.ShipType] = cells;
            }

            return true;
        }

        public (byte result, string sunkShipName) Attack(byte x, byte y)
        {
            if (_hits[x, y])
                return (0, "");

            _hits[x, y] = true;

            if (!_ships[x, y])
                return (0, "");

            var ship = _shipAt[x, y]!;
            ship.HitCount++;

            if (ship.IsSunk)
                return (2, ship.Name);

            return (1, "");
        }

        public bool IsAlreadyAttacked(byte x, byte y) => _hits[x, y];

        public bool IsAllSunk() => _shipList.All(s => s.IsSunk);

        public (Ship? ship, List<(byte x, byte y)> cells) GetShipAt(byte x, byte y)
        {
            if (x < 0 || x >= _boardSize || y < 0 || y >= _boardSize)
                return (null, new List<(byte, byte)>());

            var ship = _shipAt[x, y];
            if (ship == null)
                return (null, new List<(byte, byte)>());

            var shipType = ship.Type;
            var cells = _shipCells.ContainsKey(shipType) ? _shipCells[shipType] : new List<(byte, byte)>();
            return (ship, cells);
        }

        public (Ship? ship, List<(byte x, byte y)> cells) GetShipWithCells(byte shipType)
        {
            var ship = _shipList.FirstOrDefault(s => s.Type == shipType);
            var cells = _shipCells.ContainsKey(shipType) ? _shipCells[shipType] : new List<(byte, byte)>();
            return (ship, cells);
        }

        public bool CanPlaceShip(List<(byte x, byte y)> cells)
        {
            // Check bounds and overlaps
            foreach (var (x, y) in cells)
            {
                if (x < 0 || x >= _boardSize || y < 0 || y >= _boardSize)
                    return false;

                if (_ships[x, y])
                    return false;
            }

            return true;
        }

        public void MoveShip(byte shipType, List<(byte x, byte y)> newCells)
        {
            // Clear old cells
            if (_shipCells.ContainsKey(shipType))
            {
                foreach (var (x, y) in _shipCells[shipType])
                {
                    _ships[x, y] = false;
                    _shipAt[x, y] = null;
                }
            }

            // Place at new cells
            var ship = _shipList.FirstOrDefault(s => s.Type == shipType);
            if (ship != null)
            {
                foreach (var (x, y) in newCells)
                {
                    _ships[x, y] = true;
                    _shipAt[x, y] = ship;
                }
                _shipCells[shipType] = newCells;
            }
        }
    }
}
