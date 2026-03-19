using BattleShip.Common.Network;

namespace BattleShip.Common.Packets.Game
{
    public class S_GameOver : IPacket
    {
        public PacketId PacketId => PacketId.S_GameOver;

        public string WinnerId { get; set; }
        public byte Reason { get; set; }  // 0=AllShipsSunk, 1=Disconnected

        public void Serialize(PacketWriter writer)
        {
            writer.Write(WinnerId);
            writer.Write(Reason);
        }

        public void Deserialize(PacketReader reader)
        {
            WinnerId = reader.ReadString();
            Reason = reader.ReadByte();
        }
    }
}
