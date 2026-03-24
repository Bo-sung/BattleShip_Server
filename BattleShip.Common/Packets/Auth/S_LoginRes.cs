using BattleShip.Common.Network;

namespace BattleShip.Common.Packets.Auth
{
    public class S_LoginRes : IPacket
    {
        public PacketId PacketId => PacketId.S_LoginRes;

        public bool Success { get; set; }
        public string Token { get; set; }  // 일회용 UUID, 실패 시 빈 문자열
        public string Message { get; set; }
        public string LobbyHost { get; set; } = "";
        public int LobbyPort { get; set; }

        public void Serialize(PacketWriter writer)
        {
            writer.Write(Success);
            writer.Write(Token);
            writer.Write(Message);
            writer.Write(LobbyHost);
            writer.Write(LobbyPort);
        }

        public void Deserialize(PacketReader reader)
        {
            Success = reader.ReadBool();
            Token = reader.ReadString();
            Message = reader.ReadString();
            LobbyHost = reader.ReadString();
            LobbyPort = reader.ReadInt();
        }
    }
}
