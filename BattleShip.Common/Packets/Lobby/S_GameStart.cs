using BattleShip.Common.Network;

namespace BattleShip.Common.Packets.Lobby
{
    public class S_GameStart : IPacket
    {
        public PacketId PacketId => PacketId.S_GameStart;

        public string SessionId { get; set; }
        public int GameServerPort { get; set; }
        public string GameServerHost { get; set; } = "";

        public void Serialize(PacketWriter writer)
        {
            writer.Write(SessionId);
            writer.Write(GameServerPort);
            writer.Write(GameServerHost);
        }

        public void Deserialize(PacketReader reader)
        {
            SessionId = reader.ReadString();
            GameServerPort = reader.ReadInt();
            GameServerHost = reader.ReadString();
        }
    }
}
