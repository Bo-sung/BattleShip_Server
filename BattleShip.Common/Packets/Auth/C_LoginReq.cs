using BattleShip.Common.Network;

namespace BattleShip.Common.Packets.Auth
{// Auth/C_LoginReq.cs
    public class C_LoginReq : IPacket
    {
        public PacketId PacketId => PacketId.C_LoginReq;

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
