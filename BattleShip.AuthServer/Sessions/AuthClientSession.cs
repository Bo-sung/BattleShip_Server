using BattleShip.AuthServer.Services;
using BattleShip.Common.Network;
using BattleShip.Common.Packets;
using BattleShip.Common.Packets.Auth;
using BattleShip.Common.Session;

namespace BattleShip.AuthServer.Sessions
{
    public class AuthClientSession : PacketSession
    {
        private readonly UserRepository _userRepo;
        private readonly TokenService _tokenSvc;
        private readonly string _lobbyHost;
        private readonly int _lobbyPort;
        private readonly PacketDispatcher _dispatcher = new PacketDispatcher();

        public AuthClientSession(UserRepository userRepo, TokenService tokenSvc, string lobbyHost, int lobbyPort)
        {
            _userRepo = userRepo;
            _tokenSvc = tokenSvc;
            _lobbyHost = lobbyHost;
            _lobbyPort = lobbyPort;

            _dispatcher.Register<C_LoginReq>(PacketId.C_LoginReq, OnLogin);
            _dispatcher.Register<C_RegisterReq>(PacketId.C_RegisterReq, OnRegister);
        }

        protected override void OnPacketReceived(IPacket packet)
        {
            _dispatcher.Dispatch(packet);
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine("[Auth] 클라이언트 연결 끊김");
        }

        private async void OnLogin(C_LoginReq req)
        {
            try
            {
                var user = await _userRepo.FindByUsernameAsync(req.Username);

                if (user == null || user.PasswordHash != req.PasswordHash)
                {
                    await SendAsync(new S_LoginRes
                    {
                        Success = false,
                        Token = "",
                        Message = "아이디 또는 비밀번호가 틀렸습니다."
                    });
                    return;
                }

                string token = await _tokenSvc.IssueAsync(user.Id, user.Username);

                await SendAsync(new S_LoginRes
                {
                    Success = true,
                    Token = token,
                    Message = "",
                    LobbyHost = _lobbyHost,
                    LobbyPort = _lobbyPort
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Auth] 로그인 처리 오류: {ex.Message}");
            }
        }

        private async void OnRegister(C_RegisterReq req)
        {
            bool exists = await _userRepo.ExistsAsync(req.Username);

            if (exists)
            {
                await SendAsync(new S_RegisterRes
                {
                    Success = false,
                    Message = "이미 사용 중인 아이디입니다."
                });
                return;
            }

            await _userRepo.CreateAsync(req.Username, req.PasswordHash);

            await SendAsync(new S_RegisterRes
            {
                Success = true,
                Message = ""
            });
        }
    }
}
