using BattleShip.Common.Network;

namespace BattleShip.Common.Packets.Game
{
    // Game/S_PlaceShipsRes.cs
    public class S_PlaceShipsRes : IPacket
    {
        public PacketId PacketId => PacketId.S_PlaceShipsRes;

        public bool Success { get; set; }
        public string Message { get; set; }

        public void Serialize(PacketWriter writer)
        {
            writer.Write(Success);
            writer.Write(Message);
        }

        public void Deserialize(PacketReader reader)
        {
            Success = reader.ReadBool();
            Message = reader.ReadString();
        }
    }
}
