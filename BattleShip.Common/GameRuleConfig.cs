using BattleShip.Common.Network;
using System.Collections.Generic;

namespace BattleShip.Common
{
    public class GameRuleConfig
    {
        public int BoardSize { get; set; } = 10;
        public List<ShipDefinition> Ships { get; set; } = new List<ShipDefinition>();

        public static GameRuleConfig Default => new GameRuleConfig
        {
            BoardSize = 10,
            Ships = new List<ShipDefinition>
            {
                new ShipDefinition { Type = 0, Name = "항공모함", Size = 5 },
                new ShipDefinition { Type = 1, Name = "전함", Size = 4 },
                new ShipDefinition { Type = 2, Name = "순양함", Size = 3 },
                new ShipDefinition { Type = 3, Name = "잠수함", Size = 3 },
                new ShipDefinition { Type = 4, Name = "구축함", Size = 2 },
            }
        };

        public void Serialize(PacketWriter writer)
        {
            writer.Write((byte)BoardSize);
            writer.Write((byte)Ships.Count);
            foreach (var ship in Ships)
            {
                writer.Write(ship.Type);
                writer.Write(ship.Name);
                writer.Write((byte)ship.Size);
            }
        }

        public static GameRuleConfig Deserialize(PacketReader reader)
        {
            var config = new GameRuleConfig();
            config.BoardSize = reader.ReadByte();
            byte count = reader.ReadByte();
            for (int i = 0; i < count; i++)
            {
                config.Ships.Add(new ShipDefinition
                {
                    Type = reader.ReadByte(),
                    Name = reader.ReadString(),
                    Size = reader.ReadByte(),
                });
            }
            return config;
        }
    }
}
