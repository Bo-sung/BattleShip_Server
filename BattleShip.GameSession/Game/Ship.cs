using BattleShip.Common;

namespace BattleShip.GameSession.Game
{
    public class Ship
    {
        public string Name { get; set; } = "";
        public byte Type { get; set; }
        public int Size { get; set; }
        public int HitCount { get; set; }
        public bool IsSunk => HitCount >= Size;

        public static Ship Create(ShipDefinition def) => new Ship
        {
            Type = def.Type,
            Name = def.Name,
            Size = def.Size,
        };
    }
}
