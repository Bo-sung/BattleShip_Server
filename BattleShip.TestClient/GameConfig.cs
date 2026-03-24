namespace BattleShip.TestClient;

public static class GameConfig
{
    // Server Endpoints (런타임에 설정됨)
    public static string AUTH_SERVER_HOST  { get; set; } = "127.0.0.1";

    public const int AUTH_SERVER_PORT  = 7001;

    // Game Constants
    public const int  GAME_CONNECT_DELAY_MS = 500;
    public const char BOARD_EMPTY = '.';
    public const char BOARD_HIT   = 'X';
    public const char BOARD_MISS  = 'O';
}
