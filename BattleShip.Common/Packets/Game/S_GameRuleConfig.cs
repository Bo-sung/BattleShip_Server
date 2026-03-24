using BattleShip.Common.Network;

namespace BattleShip.Common.Packets.Game
{
    public class S_GameRuleConfig : IPacket
    {
        public PacketId PacketId => PacketId.S_GameRuleConfig;
        public GameRuleConfig Config { get; set; } = GameRuleConfig.Default;

        public void Serialize(PacketWriter writer) => Config.Serialize(writer);
        public void Deserialize(PacketReader reader) => Config = GameRuleConfig.Deserialize(reader);
    }
}
