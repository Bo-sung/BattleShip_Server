namespace BattleShip.Common
{
    public class SkillDefinition
    {
        public byte Type { get; set; }          // 0~5 (6개 스킬)
        public string Name { get; set; } = "";  // 이온 크로스, 소성운 포격, 등
        public byte ManaCost { get; set; }      // 마나 비용
        public string Description { get; set; } = "";  // 설명 (UI 용)
    }
}
