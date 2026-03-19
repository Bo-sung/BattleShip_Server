using BattleShip.Common.Network;

namespace BattleShip.Common.Packets.Game
{
    public class ShipPlacement  // 패킷 아님, 내부 데이터 구조
    {
        public byte ShipType { get; set; }  // 0=Carrier, 1=Battleship, 2=Cruiser, 3=Submarine, 4=Destroyer
        public byte X { get; set; }
        public byte Y { get; set; }
        public bool IsHorizontal { get; set; }

        public void Serialize(PacketWriter writer)
        {
            writer.Write(ShipType);
            writer.Write(X);
            writer.Write(Y);
            writer.Write(IsHorizontal);
        }

        public static ShipPlacement Deserialize(PacketReader reader)
        {
            return new ShipPlacement
            {
                ShipType = reader.ReadByte(),
                X = reader.ReadByte(),
                Y = reader.ReadByte(),
                IsHorizontal = reader.ReadBool(),
            };
        }
    }
}
