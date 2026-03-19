using BattleShip.Common.Network;

namespace BattleShip.Common.Packets.Game
{
    public class S_OpponentAttack : IPacket
    {
        public PacketId PacketId => PacketId.S_OpponentAttack;

        public byte X { get; set; }
        public byte Y { get; set; }
        public byte Result { get; set; }
        public string SunkShipName { get; set; }

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
