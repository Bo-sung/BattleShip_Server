using BattleShip.Common.Network;

namespace BattleShip.Common.Packets.Lobby
{
    public class C_EnterLobbyReq : IPacket
    {
        public PacketId PacketId => PacketId.C_EnterLobbyReq;

        public string Token { get; set; }  // Auth에서 받은 일회용 UUID

        public void Serialize(PacketWriter writer)
        {
            writer.Write(Token);
        }

        public void Deserialize(PacketReader reader)
        {
            Token = reader.ReadString();
        }
    }

    public class RoomInfo  // 패킷 아님, 내부 데이터 구조
    {
        public string RoomId { get; set; }
        public string RoomName { get; set; }
        public string HostUsername { get; set; }
        public byte PlayerCount { get; set; }

        public void Serialize(PacketWriter writer)
        {
            writer.Write(RoomId);
            writer.Write(RoomName);
            writer.Write(HostUsername);
            writer.Write(PlayerCount);
        }

        public static RoomInfo Deserialize(PacketReader reader)
        {
            return new RoomInfo
            {
                RoomId = reader.ReadString(),
                RoomName = reader.ReadString(),
                HostUsername = reader.ReadString(),
                PlayerCount = reader.ReadByte(),
            };
        }
    }
}
