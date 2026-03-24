// BattleShip.AuthServer/Program.cs
using BattleShip.AuthServer;
using BattleShip.AuthServer.Services;
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
        string lobbyHost = Environment.GetEnvironmentVariable("LOBBY_HOST") ?? "127.0.0.1";
        int lobbyPort = int.Parse(Environment.GetEnvironmentVariable("LOBBY_PORT") ?? "7002");

        var redis = await ConnectionMultiplexer.ConnectAsync(redisConnection);
        var userRepo = new UserRepository(dbConnection);
        var tokenSvc = new TokenService(redis.GetDatabase());

        var server = new AuthServer(userRepo, tokenSvc, lobbyHost, lobbyPort);
        await server.StartAsync(port: 7001);
    }
}