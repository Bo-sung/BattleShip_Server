// BattleShip.AuthServer/Program.cs
using BattleShip.AuthServer;
using BattleShip.AuthServer.Services;
using StackExchange.Redis;

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

        string dbConnection = Environment.GetEnvironmentVariable("DB_CONNECTION")
                              ?? "Server=localhost;Database=battleship;User=root;Password=asdf1358;";
        string redisConnection = Environment.GetEnvironmentVariable("REDIS_CONNECTION")
                              ?? "localhost:6379";
        string lobbyHost = Environment.GetEnvironmentVariable("LOBBY_HOST") ?? "127.0.0.1";
        int lobbyPort = int.Parse(Environment.GetEnvironmentVariable("LOBBY_PORT") ?? "7002");

        var redis = await ConnectionMultiplexer.ConnectAsync(redisConnection);
        var userRepo = new UserRepository(dbConnection);
        var tokenSvc = new TokenService(redis.GetDatabase());

        var server = new AuthServer(userRepo, tokenSvc, lobbyHost, lobbyPort);
        await server.StartAsync(port: 7001);
    }
}