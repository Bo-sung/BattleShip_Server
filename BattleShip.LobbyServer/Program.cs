// BattleShip.LobbyServer/Program.cs
using BattleShip.LobbyServer;
using BattleShip.LobbyServer.Repositories;
using BattleShip.LobbyServer.Services;
using StackExchange.Redis;

class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        string dbConnection = Environment.GetEnvironmentVariable("DB_CONNECTION")
                              ?? "Server=localhost;Database=battleship;User=root;Password=asdf1358;";
        string redisConnection = Environment.GetEnvironmentVariable("REDIS_CONNECTION")
                              ?? "localhost:6379";
        string gameSessionExe = Environment.GetEnvironmentVariable("GAME_SESSION_EXE_PATH")
                     ?? @"H:\Git\Portpolio\BattleShip\Server\BattleShip.GameSession\bin\Debug\net10.0\BattleShip.GameSession.exe";
        int portMin = int.Parse(Environment.GetEnvironmentVariable("GAME_SESSION_PORT_MIN") ?? "7010");
        int portMax = int.Parse(Environment.GetEnvironmentVariable("GAME_SESSION_PORT_MAX") ?? "7200");

        var redis = await ConnectionMultiplexer.ConnectAsync(redisConnection);
        var redisDb = redis.GetDatabase();
        var roomMgr = new RoomManager(redisDb);
        var gameRecord = new GameRecordRepository(dbConnection);
        var launcher = new GameSessionLauncher(gameSessionExe, portMin, portMax);
        var registry = new SessionRegistry();
        var watchdog = new SessionWatchdog(registry);

        var server = new LobbyServer(redisDb, roomMgr, launcher, registry, gameRecord);

        _ = watchdog.StartAsync();

        await server.StartAsync(port: 7002, internalPort: 8002);
    }
}