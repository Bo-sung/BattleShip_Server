namespace BattleShip.TestClient;

public static class GameConfig
{
    // Server Endpoints
    public const string AUTH_SERVER_HOST = "127.0.0.1";
    public const int AUTH_SERVER_PORT = 7001;

    public const string LOBBY_SERVER_HOST = "127.0.0.1";
    public const int LOBBY_SERVER_PORT = 7002;

    public const string GAME_SERVER_HOST = "127.0.0.1";

    // Game Constants
    public const int GAME_CONNECT_DELAY_MS = 500;
    public const char BOARD_EMPTY = '.';
    public const char BOARD_HIT  = 'X';
    public const char BOARD_MISS = 'O';
}
