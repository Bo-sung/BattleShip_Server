using BattleShip.Common.Network;
using System.Collections.Generic;

namespace BattleShip.Common.Packets.Game
{
    public class C_PlaceShipsReq : IPacket
    {
        public PacketId PacketId => PacketId.C_PlaceShipsReq;

        public List<ShipPlacement> Ships { get; set; } = new List<ShipPlacement>();

        public void Serialize(PacketWriter writer)
        {
            writer.Write((byte)Ships.Count);
            foreach (var ship in Ships)
                ship.Serialize(writer);
        }

        public void Deserialize(PacketReader reader)
        {
            byte count = reader.ReadByte();
            for (int i = 0; i < count; i++)
                Ships.Add(ShipPlacement.Deserialize(reader));
        }
    }
}
