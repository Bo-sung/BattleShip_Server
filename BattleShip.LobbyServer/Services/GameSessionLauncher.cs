using System.Diagnostics;

namespace BattleShip.LobbyServer.Services
{
    public class SessionInfo
    {
        public string SessionId { get; set; }
        public int Port { get; set; }
        public int ProcessId { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime LastPingAt { get; set; }
    }

    public class GameSessionLauncher
    {
        private readonly string _exePath;
        private readonly int _portMin;
        private readonly int _portMax;
        private readonly HashSet<int> _usedPorts = new HashSet<int>();
        private readonly object _lock = new object();

        public GameSessionLauncher(string exePath, int portMin, int portMax)
        {
            _exePath = exePath;
            _portMin = portMin;
            _portMax = portMax;
        }

        public SessionInfo Launch(string roomId)
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

            // 프로세스 종료 시 포트 반환
            process.EnableRaisingEvents = true;
            process.Exited += (s, e) => ReleasePort(port);

            return new SessionInfo
            {
                SessionId = sessionId,
                Port = port,
                ProcessId = process.Id,
                StartedAt = DateTime.UtcNow,
                LastPingAt = DateTime.UtcNow,
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
