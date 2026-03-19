using System.Diagnostics;

namespace BattleShip.LobbyServer.Services
{
    public class SessionWatchdog
    {
        private readonly SessionRegistry _registry;
        private const int CheckIntervalMs = 10000;  // 10초마다 검사
        private const int ZombieThresholdS = 30;     // 30초 무핑 → 좀비

        public SessionWatchdog(SessionRegistry registry)
        {
            _registry = registry;
        }

        public async Task StartAsync()
        {
            Console.WriteLine("[Lobby] Watchdog 시작");

            while (true)
            {
                await Task.Delay(CheckIntervalMs);

                var now = DateTime.UtcNow;
                var sessions = _registry.GetAll();

                foreach (var session in sessions)
                {
                    var silent = (now - session.LastPingAt).TotalSeconds;

                    if (silent > ZombieThresholdS)
                    {
                        Console.WriteLine($"[Lobby] 좀비 세션 감지: {session.SessionId} ({silent:F0}초 무핑)");
                        KillSession(session);
                    }
                }
            }
        }

        private void KillSession(SessionInfo session)
        {
            try
            {
                var process = Process.GetProcessById(session.ProcessId);
                process.Kill();
                Console.WriteLine($"[Lobby] 프로세스 Kill: PID {session.ProcessId}");
            }
            catch
            {
                // 이미 종료된 프로세스
            }
            finally
            {
                _registry.Remove(session.SessionId);
            }
        }
    }
}
