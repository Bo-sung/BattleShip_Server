using BattleShip.LobbyServer.Repositories;
using BattleShip.LobbyServer.Services;
using BattleShip.LobbyServer.Sessions;
using StackExchange.Redis;
using System.Net;
using System.Net.Sockets;

namespace BattleShip.LobbyServer
{
    public class LobbyServer
    {
        private readonly IDatabase _redis;
        private readonly RoomManager _roomMgr;
        private readonly GameSessionLauncher _launcher;
        private readonly SessionRegistry _registry;
        private readonly GameRecordRepository _gameRecord;

        public LobbyServer(
            IDatabase redis,
            RoomManager roomMgr,
            GameSessionLauncher launcher,
            SessionRegistry registry,
            GameRecordRepository gameRecord)
        {
            _redis = redis;
            _roomMgr = roomMgr;
            _launcher = launcher;
            _registry = registry;
            _gameRecord = gameRecord;
        }

        public async Task StartAsync(int port, int internalPort)
        {
            // 클라이언트 연결 수락
            _ = AcceptClientsAsync(port);

            // GameSession 역접속 수신
            _ = AcceptSessionConnectionsAsync(internalPort);

            Console.WriteLine($"[Lobby] 시작 — 클라이언트 :{port}, 내부 :{internalPort}");

            await Task.Delay(Timeout.Infinite);
        }

        private async Task AcceptClientsAsync(int port)
        {
            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();

            while (true)
            {
                var client = await listener.AcceptTcpClientAsync();
                Console.WriteLine("[Lobby] 클라이언트 연결");

                var session = new LobbyClientSession(_redis, _roomMgr, _launcher, _registry);
                _ = session.StartAsync(client);
            }
        }

        private async Task AcceptSessionConnectionsAsync(int port)
        {
            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();

            while (true)
            {
                var client = await listener.AcceptTcpClientAsync();
                Console.WriteLine("[Lobby] GameSession 역접속");

                var conn = new GameSessionConnection(_registry, _gameRecord);
                _ = conn.StartAsync(client);
            }
        }
    }
}
