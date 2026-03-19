using BattleShip.Common.Network;
using System.Collections.Generic;

namespace BattleShip.Common.Packets.Game
{
    public class S_EnterSessionRes : IPacket
    {
        public PacketId PacketId => PacketId.S_EnterSessionRes;

        public bool Success { get; set; }
        public byte PlayerIndex { get; set; }  // 0 or 1

        public void Serialize(PacketWriter writer)
        {
            writer.Write(Success);
            writer.Write(PlayerIndex);
        }

        public void Deserialize(PacketReader reader)
        {
            Success = reader.ReadBool();
            PlayerIndex = reader.ReadByte();
        }
    }
}
