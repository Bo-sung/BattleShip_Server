using BattleShip.Common.Network;

namespace BattleShip.Common.Packets.ServerInternal
{
    public class SS_SessionRuleConfig : IPacket
    {
        public PacketId PacketId => PacketId.SS_SessionRuleConfig;
        public GameRuleConfig Config { get; set; } = GameRuleConfig.Default;

        public void Serialize(PacketWriter writer) => Config.Serialize(writer);
        public void Deserialize(PacketReader reader) => Config = GameRuleConfig.Deserialize(reader);
    }
}
