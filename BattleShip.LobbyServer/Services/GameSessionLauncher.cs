using BattleShip.Common.Packets.Lobby;
using BattleShip.LobbyServer.Sessions;
using System.Diagnostics;

namespace BattleShip.LobbyServer.Services
{
    public class SessionInfo
    {
        public string SessionId { get; set; } = "";
        public int Port { get; set; }
        public string Host { get; set; } = "127.0.0.1";
        public int ProcessId { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime LastPingAt { get; set; }
        public string ConfigName { get; set; } = "classic";

        // Pending: waiting to send S_GameStart when session is ready
        public LobbyClientSession? Player1 { get; set; }
        public LobbyClientSession? Player2 { get; set; }
        public S_GameStart? PendingGameStart { get; set; }
    }

    public class GameSessionLauncher
    {
        private readonly string _exePath;
        private readonly int _portMin;
        private readonly int _portMax;
        private readonly string _gameSessionHost;
        private readonly HashSet<int> _usedPorts = new HashSet<int>();
        private readonly object _lock = new object();

        public GameSessionLauncher(string exePath, int portMin, int portMax, string gameSessionHost = "127.0.0.1")
        {
            _exePath = exePath;
            _portMin = portMin;
            _portMax = portMax;
            _gameSessionHost = gameSessionHost;
        }

        public SessionInfo Launch(string roomId, string configName = "classic")
        {
            int port = AllocatePort();
            string sessionId = Guid.NewGuid().ToString("N")[..8];

            var psi = new ProcessStartInfo
            {
                FileName = _exePath,
                Arguments = $"--session {sessionId} --port {port} --lobby-host 127.0.0.1 --lobby-port 8002",
                UseShellExecute = true,
                CreateNoWindow = false,
            };

            var process = Process.Start(psi)
                ?? throw new Exception("GameSession 프로세스 시작 실패");

            process.EnableRaisingEvents = true;
            process.Exited += (s, e) => ReleasePort(port);

            return new SessionInfo
            {
                SessionId = sessionId,
                Port = port,
                Host = _gameSessionHost,
                ProcessId = process.Id,
                StartedAt = DateTime.UtcNow,
                LastPingAt = DateTime.UtcNow,
                ConfigName = configName,
            };
        }

        private int AllocatePort()
        {
            lock (_lock)
            {
                for (int p = _portMin; p <= _portMax; p++)
                {
                    if (!_usedPorts.Contains(p))
                    {
                        _usedPorts.Add(p);
                        return p;
                    }
                }
                throw new Exception("사용 가능한 포트 없음");
            }
        }

        private void ReleasePort(int port)
        {
            lock (_lock) { _usedPorts.Remove(port); }
        }
    }
}
