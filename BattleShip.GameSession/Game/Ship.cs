namespace BattleShip.GameSession.Game
{
    public class Ship
    {
        public string Name { get; set; }
        public byte Type { get; set; }
        public int Size { get; set; }
        public int HitCount { get; set; }
        public bool IsSunk => HitCount >= Size;

        public static Ship Create(byte type) => type switch
        {
            0 => new Ship { Type = 0, Name = "항공모함", Size = 5 },
            1 => new Ship { Type = 1, Name = "전함", Size = 4 },
            2 => new Ship { Type = 2, Name = "순양함", Size = 3 },
            3 => new Ship { Type = 3, Name = "잠수함", Size = 3 },
            4 => new Ship { Type = 4, Name = "구축함", Size = 2 },
            _ => throw new Exception($"알 수 없는 함선 타입: {type}")
        };
    }
}
