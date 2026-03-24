namespace BattleShip.LobbyServer.Services
{
    public class SessionRegistry
    {
        private readonly Dictionary<string, SessionInfo> _sessions = new Dictionary<string, SessionInfo>();
        private readonly object _lock = new object();

        public void Add(SessionInfo info)
        {
            lock (_lock) { _sessions[info.SessionId] = info; }
        }

        public void UpdatePing(string sessionId)
        {
            lock (_lock)
            {
                if (_sessions.TryGetValue(sessionId, out var info))
                    info.LastPingAt = DateTime.UtcNow;
            }
        }

        public void Remove(string sessionId)
        {
            lock (_lock) { _sessions.Remove(sessionId); }
        }

        public string GetConfigName(string sessionId)
        {
            lock (_lock)
            {
                return _sessions.TryGetValue(sessionId, out var info) ? info.ConfigName : "classic";
            }
        }

        public SessionInfo? GetSession(string sessionId)
        {
            lock (_lock)
            {
                return _sessions.TryGetValue(sessionId, out var info) ? info : null;
            }
        }

        public List<SessionInfo> GetAll()
        {
            lock (_lock) { return _sessions.Values.ToList(); }
        }
    }
}
