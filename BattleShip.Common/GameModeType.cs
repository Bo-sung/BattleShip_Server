namespace BattleShip.Common
{
    public enum GameModeType : byte
    {
        Basic = 0,      // Classic mode (10x10, 5 ships)
        Extended = 1,   // Extended mode (12x12, 6 ships)
        SkillMode = 2,  // StarBattle mode (12x12, 6 ships, with movement/mana/skills)
    }
}
