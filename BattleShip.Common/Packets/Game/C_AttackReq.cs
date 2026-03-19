using BattleShip.Common.Network;

namespace BattleShip.Common.Packets.Game
{

    // Game/C_AttackReq.cs
    public class C_AttackReq : IPacket
    {
        public PacketId PacketId => PacketId.C_AttackReq;

        public byte X { get; set; }
        public byte Y { get; set; }

        public void Serialize(PacketWriter writer)
        {
            writer.Write(X);
            writer.Write(Y);
        }

        public void Deserialize(PacketReader reader)
        {
            X = reader.ReadByte();
            Y = reader.ReadByte();
        }
    }
}
