namespace BattleShip.TestClient;

public static class GameConfig
{
    // Server Endpoints
    public const string AUTH_SERVER_HOST = "127.0.0.1";
    public const int AUTH_SERVER_PORT = 7001;

    public const string LOBBY_SERVER_HOST = "127.0.0.1";
    public const int LOBBY_SERVER_PORT = 7002;

    public const string GAME_SERVER_HOST = "127.0.0.1";

    // Game Board
    public const int BOARD_SIZE = 10;
    public const int SHIP_COUNT = 5;

    // Ship Configuration
    public static class Ships
    {
        public static readonly string[] Names =
        {
            "항공모함(5칸)",
            "전함(4칸)",
            "순양함(3칸)",
            "잠수함(3칸)",
            "구축함(2칸)"
        };

        public static readonly int[] Sizes = { 5, 4, 3, 3, 2 };
    }

    // Game Constants
    public const int GAME_CONNECT_DELAY_MS = 500;
    public const char BOARD_EMPTY = '.';
    public const char BOARD_HIT  = 'X';   // 히트 (내 보드: 피격된 함선 / 상대 보드: 명중)
    public const char BOARD_MISS = 'O';   // 미스 (내 보드: 빗나간 공격 / 상대 보드: 빗나감)
}
