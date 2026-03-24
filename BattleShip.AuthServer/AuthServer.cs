using BattleShip.AuthServer.Services;
using BattleShip.AuthServer.Sessions;
using System.Net;
using System.Net.Sockets;

namespace BattleShip.AuthServer
{
    public class AuthServer
    {
        private readonly UserRepository _userRepo;
        private readonly TokenService _tokenSvc;
        private readonly string _lobbyHost;
        private readonly int _lobbyPort;

        public AuthServer(UserRepository userRepo, TokenService tokenSvc, string lobbyHost, int lobbyPort)
        {
            _userRepo = userRepo;
            _tokenSvc = tokenSvc;
            _lobbyHost = lobbyHost;
            _lobbyPort = lobbyPort;
        }

        public async Task StartAsync(int port)
        {
            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            Console.WriteLine($"[Auth] 시작 — 포트 {port}");

            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                Console.WriteLine("[Auth] 클라이언트 연결");

                // 세션마다 독립 Task로 처리 (await 없이 fire-and-forget)
                var session = new AuthClientSession(_userRepo, _tokenSvc, _lobbyHost, _lobbyPort);
                _ = session.StartAsync(client);
            }
        }
    }
}
