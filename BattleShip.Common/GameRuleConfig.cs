using BattleShip.Common.Network;
using System.Collections.Generic;

namespace BattleShip.Common
{
    public class GameRuleConfig
    {
        public int BoardSize { get; set; } = 10;
        public List<ShipDefinition> Ships { get; set; } = new List<ShipDefinition>();

        public byte GameMode { get; set; } = 0; // 0: 기본, 1: 확장, 2: 스킬모드

        public List<SkillDefinition> SkillPool { get; set; } = new List<SkillDefinition>();

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
            writer.Write(GameMode);

            writer.Write((byte)Ships.Count);
            foreach (var ship in Ships)
            {
                writer.Write(ship.Type);
                writer.Write(ship.Name);
                writer.Write((byte)ship.Size);
            }

            writer.Write((byte)SkillPool.Count);
            foreach (var skill in SkillPool)
            {
                writer.Write(skill.Type);
                writer.Write(skill.Name);
                writer.Write(skill.ManaCost);
                writer.Write(skill.Description);
            }
        }

        public static GameRuleConfig Deserialize(PacketReader reader)
        {
            var config = new GameRuleConfig();
            config.BoardSize = reader.ReadByte();
            config.GameMode = reader.ReadByte();

            byte shipCount = reader.ReadByte();
            for (int i = 0; i < shipCount; i++)
            {
                config.Ships.Add(new ShipDefinition
                {
                    Type = reader.ReadByte(),
                    Name = reader.ReadString(),
                    Size = reader.ReadByte(),
                });
            }

            byte skillCount = reader.ReadByte();
            for (int i = 0; i < skillCount; i++)
            {
                config.SkillPool.Add(new SkillDefinition
                {
                    Type = reader.ReadByte(),
                    Name = reader.ReadString(),
                    ManaCost = reader.ReadByte(),
                    Description = reader.ReadString(),
                });
            }

            return config;
        }
    }
}
