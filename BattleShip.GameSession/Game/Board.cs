using BattleShip.Common.Packets.Game;

namespace BattleShip.GameSession.Game
{
    public class Board
    {
        private readonly bool[,] _ships = new bool[10, 10];  // 함선 위치
        private readonly bool[,] _hits = new bool[10, 10];  // 공격 받은 위치
        private readonly Ship?[,] _shipAt = new Ship[10, 10];  // 좌표별 함선 참조
        private readonly List<Ship> _shipList = new List<Ship>();

        public bool PlaceShips(List<ShipPlacement> placements)
        {
            // 5종류 함선 모두 있는지 확인
            if (placements.Count != 5)
                return false;

            var types = placements.Select(p => p.ShipType).Distinct().ToList();
            if (types.Count != 5)
                return false;

            // 임시 배치 후 유효성 검증
            var temp = new bool[10, 10];

            foreach (var p in placements)
            {
                var ship = Ship.Create(p.ShipType);

                for (int i = 0; i < ship.Size; i++)
                {
                    int x = p.IsHorizontal ? p.X + i : p.X;
                    int y = p.IsHorizontal ? p.Y : p.Y + i;

                    // 범위 초과
                    if (x < 0 || x >= 10 || y < 0 || y >= 10)
                        return false;

                    // 겹침
                    if (temp[x, y])
                        return false;

                    temp[x, y] = true;
                }
            }

            // 검증 통과 → 실제 배치
            foreach (var p in placements)
            {
                var ship = Ship.Create(p.ShipType);
                _shipList.Add(ship);

                for (int i = 0; i < ship.Size; i++)
                {
                    int x = p.IsHorizontal ? p.X + i : p.X;
                    int y = p.IsHorizontal ? p.Y : p.Y + i;

                    _ships[x, y] = true;
                    _shipAt[x, y] = ship;
                }
            }

            return true;
        }

        // 공격 처리. 반환값: 0=Miss, 1=Hit, 2=Sunk
        public (byte result, string sunkShipName) Attack(byte x, byte y)
        {
            if (_hits[x, y])
                return (0, "");  // 이미 공격한 좌표

            _hits[x, y] = true;

            if (!_ships[x, y])
                return (0, "");  // Miss

            var ship = _shipAt[x, y]!;
            ship.HitCount++;

            if (ship.IsSunk)
                return (2, ship.Name);  // Sunk

            return (1, "");  // Hit
        }

        public bool IsAllSunk() => _shipList.All(s => s.IsSunk);
    }
}
