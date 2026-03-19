// BattleShip.GameSession/Program.cs
using BattleShip.GameSession;

class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var argMap = ParseArgs(args);

        string sessionId = argMap.GetValueOrDefault("--session", "test-session");
        int port = int.Parse(argMap.GetValueOrDefault("--port", "7010"));
        string lobbyHost = argMap.GetValueOrDefault("--lobby-host", "127.0.0.1");
        int lobbyPort = int.Parse(argMap.GetValueOrDefault("--lobby-port", "8002"));

        Console.WriteLine($"[Session:{sessionId}] 시작 — 포트 {port}");

        var server = new GameSessionServer(sessionId, port, lobbyHost, lobbyPort);
        await server.RunAsync();
    }

    static Dictionary<string, string> ParseArgs(string[] args)
    {
        var map = new Dictionary<string, string>();
        for (int i = 0; i < args.Length - 1; i += 2)
            map[args[i]] = args[i + 1];
        return map;
    }
}