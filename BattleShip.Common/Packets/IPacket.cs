using BattleShip.Common.Network;

namespace BattleShip.Common.Packets
{
    public interface IPacket
    {
        PacketId PacketId { get; }
        void Serialize(PacketWriter writer);
        void Deserialize(PacketReader reader);
    }
}
