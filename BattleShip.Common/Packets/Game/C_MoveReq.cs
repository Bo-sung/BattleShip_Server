using BattleShip.Common.Network;

namespace BattleShip.Common.Packets.Game
{
    public class C_MoveReq : IPacket
    {
        public PacketId PacketId => PacketId.C_MoveReq;

        public byte ShipType { get; set; }
        public sbyte DirX { get; set; }  // -1, 0, 1 (좌, 정지, 우)
        public sbyte DirY { get; set; }  // -1, 0, 1 (상, 정지, 하)

        public void Serialize(PacketWriter writer)
        {
            writer.Write(ShipType);
            writer.Write((byte)DirX);
            writer.Write((byte)DirY);
        }

        public void Deserialize(PacketReader reader)
        {
            ShipType = reader.ReadByte();
            DirX = (sbyte)reader.ReadByte();
            DirY = (sbyte)reader.ReadByte();
        }
    }
}
