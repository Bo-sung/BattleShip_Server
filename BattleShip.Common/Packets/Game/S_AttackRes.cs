using BattleShip.Common.Network;

namespace BattleShip.Common.Packets.Game
{
    public class S_AttackRes : IPacket
    {
        public PacketId PacketId => PacketId.S_AttackRes;

        public byte X { get; set; }
        public byte Y { get; set; }
        public byte Result { get; set; }  // 0=Miss, 1=Hit, 2=Sunk
        public string SunkShipName { get; set; }  // Sunk일 때만 유효, 나머지는 빈 문자열

        public void Serialize(PacketWriter writer)
        {
            writer.Write(X);
            writer.Write(Y);
            writer.Write(Result);
            writer.Write(SunkShipName);
        }

        public void Deserialize(PacketReader reader)
        {
            X = reader.ReadByte();
            Y = reader.ReadByte();
            Result = reader.ReadByte();
            SunkShipName = reader.ReadString();
        }
    }
}
