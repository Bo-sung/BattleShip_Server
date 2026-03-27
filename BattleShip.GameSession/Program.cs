using BattleShip.Common;
using BattleShip.GameSession;

class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // .env 파일 로드 (현재 디렉토리 → 상위 디렉토리 순으로 탐색)
        var envFile = File.Exists(".env") ? ".env" : File.Exists("../.env") ? "../.env" : null;
        if (envFile != null)
            foreach (var line in File.ReadLines(envFile))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                var idx = line.IndexOf('=');
                if (idx < 0) continue;
                Environment.SetEnvironmentVariable(line[..idx].Trim(), line[(idx + 1)..].Trim());
            }

        var argMap = ParseArgs(args);

        string sessionId = argMap.GetValueOrDefault("--session", "test-session");
        int port = int.Parse(argMap.GetValueOrDefault("--port", "7010"));
        string lobbyHost = argMap.GetValueOrDefault("--lobby-host", "127.0.0.1");
        int lobbyPort = int.Parse(argMap.GetValueOrDefault("--lobby-port", "8002"));

        // standalone 모드: --config <name> 으로 config 파일 직접 지정
        GameRuleConfig? standaloneConfig = LoadStandaloneConfig(argMap);

        Console.WriteLine($"[Session:{sessionId}] 시작 — 포트 {port}");

        var server = new GameSessionServer(sessionId, port, lobbyHost, lobbyPort, standaloneConfig);
        await server.RunAsync();
    }

    static GameRuleConfig? LoadStandaloneConfig(Dictionary<string, string> argMap)
    {
        if (!argMap.TryGetValue("--config", out var configName))
            return null;

        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "configs", configName + ".json");

        if (!File.Exists(path))
        {
            Console.WriteLine($"[Session] 설정 파일 없음: {path}, Lobby에서 수신합니다.");
            return null;
        }

        try
        {
            var config = LoadConfigFromJson(path);
            Console.WriteLine($"[Session] standalone 룰 로드: {configName} (boardSize={config.BoardSize})");
            return config;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Session] 설정 파일 로드 실패: {ex.Message}");
            return null;
        }
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

    static Dictionary<string, string> ParseArgs(string[] args)
    {
        var map = new Dictionary<string, string>();
        for (int i = 0; i < args.Length - 1; i += 2)
            map[args[i]] = args[i + 1];
        return map;
    }
}
