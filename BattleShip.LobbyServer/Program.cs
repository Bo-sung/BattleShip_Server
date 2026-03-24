// BattleShip.LobbyServer/Program.cs
using BattleShip.Common;
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
        string gameSessionHost = Environment.GetEnvironmentVariable("GAME_SESSION_HOST") ?? "127.0.0.1";

        var configs = LoadConfigs(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "configs"));

        var redis = await ConnectionMultiplexer.ConnectAsync(redisConnection);
        var redisDb = redis.GetDatabase();
        var roomMgr = new RoomManager(redisDb);
        var gameRecord = new GameRecordRepository(dbConnection);
        var launcher = new GameSessionLauncher(gameSessionExe, portMin, portMax, gameSessionHost);
        var registry = new SessionRegistry();
        var watchdog = new SessionWatchdog(registry);

        var server = new LobbyServer(redisDb, roomMgr, launcher, registry, gameRecord, configs);

        _ = watchdog.StartAsync();

        await server.StartAsync(port: 7002, internalPort: 8002);
    }

    static Dictionary<string, GameRuleConfig> LoadConfigs(string configsDir)
    {
        var result = new Dictionary<string, GameRuleConfig>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(configsDir))
        {
            Console.WriteLine("[Lobby] configs 디렉토리 없음 — default(classic) 사용");
            result["classic"] = GameRuleConfig.Default;
            return result;
        }

        foreach (var file in Directory.GetFiles(configsDir, "*.json"))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            try
            {
                result[name] = LoadConfigFromJson(file);
                Console.WriteLine($"[Lobby] 룰 로드: {name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Lobby] 룰 로드 실패 ({name}): {ex.Message}");
            }
        }

        if (!result.ContainsKey("classic"))
            result["classic"] = GameRuleConfig.Default;

        return result;
    }

    static GameRuleConfig LoadConfigFromJson(string path)
    {
        var json = File.ReadAllText(path);
        var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        var config = new GameRuleConfig
        {
            BoardSize = root.GetProperty("boardSize").GetInt32()
        };

        foreach (var ship in root.GetProperty("ships").EnumerateArray())
        {
            config.Ships.Add(new ShipDefinition
            {
                Type = ship.GetProperty("type").GetByte(),
                Name = ship.GetProperty("name").GetString()!,
                Size = ship.GetProperty("size").GetInt32(),
            });
        }

        return config;
    }
}
