using BattleShip.Common.Network;

namespace BattleShip.Common.Packets.Auth
{
    public class C_RegisterReq : IPacket
    {
        public PacketId PacketId => PacketId.C_RegisterReq;

        public string Username { get; set; }
        public string PasswordHash { get; set; }

        public void Serialize(PacketWriter writer)
        {
            writer.Write(Username);
            writer.Write(PasswordHash);
        }

        public void Deserialize(PacketReader reader)
        {
            Username = reader.ReadString();
            PasswordHash = reader.ReadString();
        }
    }
}
