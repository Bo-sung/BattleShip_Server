using BattleShip.Common.Network;

namespace BattleShip.Common.Packets.Game
{
    public class S_OpponentRepaired : IPacket
    {
        public PacketId PacketId => PacketId.S_OpponentRepaired;

        public byte ShipType { get; set; }
        public byte X { get; set; }
        public byte Y { get; set; }

        public void Serialize(PacketWriter writer)
        {
            writer.Write(ShipType);
            writer.Write(X);
            writer.Write(Y);
        }

        public void Deserialize(PacketReader reader)
        {
            ShipType = reader.ReadByte();
            X = reader.ReadByte();
            Y = reader.ReadByte();
        }
    }
}
