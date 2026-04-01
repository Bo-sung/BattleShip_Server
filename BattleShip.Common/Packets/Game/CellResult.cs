using BattleShip.Common.Network;

namespace BattleShip.Common.Packets.Game
{
    public class CellResult
    {
        public byte X { get; set; }
        public byte Y { get; set; }
        public byte Result { get; set; }  // 0=Miss, 1=Hit, 2=Sunk, 3=AlreadyAttacked, 4=ShieldBlocked
        public string SunkShipName { get; set; } = "";

        public void Serialize(PacketWriter writer)
        {
            writer.Write(X);
            writer.Write(Y);
            writer.Write(Result);
            writer.Write(SunkShipName);
        }

        public static CellResult Deserialize(PacketReader reader)
        {
            return new CellResult
            {
                X = reader.ReadByte(),
                Y = reader.ReadByte(),
                Result = reader.ReadByte(),
                SunkShipName = reader.ReadString(),
            };
        }
    }
}
