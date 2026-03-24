using BattleShip.Common.Network;
using BattleShip.Common.Packets;
using BattleShip.Common.Packets.Lobby;
using BattleShip.Common.Session;
using BattleShip.LobbyServer.Services;
using StackExchange.Redis;

namespace BattleShip.LobbyServer.Sessions
{
    public class LobbyClientSession : PacketSession
    {
        private readonly IDatabase _redis;
        private readonly RoomManager _roomMgr;
        private readonly GameSessionLauncher _launcher;
        private readonly SessionRegistry _registry;
        private readonly PacketDispatcher _dispatcher = new PacketDispatcher();

        public int? UserId { get; private set; }
        public string Username { get; private set; }
        public string RoomId { get; private set; }  // 현재 입장한 방
        public bool IsReady { get; internal set; }
        public bool IsAuthenticated => UserId.HasValue;

        public LobbyClientSession(
            IDatabase redis,
            RoomManager roomMgr,
            GameSessionLauncher launcher,
            SessionRegistry registry)
        {
            _redis = redis;
            _roomMgr = roomMgr;
            _launcher = launcher;
            _registry = registry;

            _dispatcher.Register<C_EnterLobbyReq>(PacketId.C_EnterLobbyReq, OnEnterLobby);
            _dispatcher.Register<C_RoomCreateReq>(PacketId.C_RoomCreateReq, OnRoomCreate);
            _dispatcher.Register<C_RoomListReq>(PacketId.C_RoomListReq, OnRoomList);
            _dispatcher.Register<C_RoomJoinReq>(PacketId.C_RoomJoinReq, OnRoomJoin);
            _dispatcher.Register<C_ReadyReq>(PacketId.C_ReadyReq, OnReady);
        }

        protected override void OnPacketReceived(IPacket packet)
        {
            if (!IsAuthenticated && packet.PacketId != PacketId.C_EnterLobbyReq)
            {
                Console.WriteLine("[Lobby] 미인증 패킷 차단");
                return;
            }
            _dispatcher.Dispatch(packet);
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"[Lobby] 연결 끊김: {Username}");

            if (RoomId != null)
                _roomMgr.LeaveRoom(RoomId, this);
        }

        // ── 핸들러 ────────────────────────────────────────

        private async void OnEnterLobby(C_EnterLobbyReq req)
        {
            try
            {
                string key = $"auth:token:{req.Token}";
                string value = await _redis.StringGetAsync(key);

                if (value == null)
                {
                    await SendAsync(new S_EnterLobbyRes
                    {
                        Success = false,
                        Message = "인증 토큰이 유효하지 않거나 만료되었습니다."
                    });
                    return;
                }

                // 1회 사용 후 즉시 삭제
                await _redis.KeyDeleteAsync(key);

                // "userId:username" 파싱
                var parts = value.ToString().Split(':');
                UserId = int.Parse(parts[0]);
                Username = parts[1];

                Console.WriteLine($"[Lobby] 인증 성공: {Username} (userId={UserId})");

                await SendAsync(new S_EnterLobbyRes { Success = true, Message = "" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Lobby] EnterLobby 오류: {ex.Message}");
            }
        }

        private async void OnRoomCreate(C_RoomCreateReq req)
        {
            try
            {
                if (RoomId != null)
                {
                    await SendAsync(new S_RoomCreateRes
                    {
                        Success = false,
                        Message = "이미 방에 입장해 있습니다."
                    });
                    return;
                }

                string roomId = _roomMgr.CreateRoom(req.RoomName, this);
                RoomId = roomId;

                await SendAsync(new S_RoomCreateRes
                {
                    Success = true,
                    RoomId = roomId,
                    Message = ""
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Lobby] RoomCreate 오류: {ex.Message}");
            }
        }

        private async void OnRoomList(C_RoomListReq req)
        {
            try
            {
                var rooms = _roomMgr.GetRoomList();
                await SendAsync(new S_RoomListRes { Rooms = rooms });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Lobby] RoomList 오류: {ex.Message}");
            }
        }

        private async void OnRoomJoin(C_RoomJoinReq req)
        {
            try
            {
                if (RoomId != null)
                {
                    await SendAsync(new S_RoomJoinRes
                    {
                        Success = false,
                        Message = "이미 방에 입장해 있습니다."
                    });
                    return;
                }

                var result = _roomMgr.JoinRoom(req.RoomId, this);

                if (!result.Success)
                {
                    await SendAsync(new S_RoomJoinRes
                    {
                        Success = false,
                        Message = result.Message
                    });
                    return;
                }

                RoomId = req.RoomId;

                await SendAsync(new S_RoomJoinRes { Success = true, Message = "" });

                // 방장에게 상대 입장 알림
                await result.Host.SendAsync(new S_RoomUserJoined { Username = Username });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Lobby] RoomJoin 오류: {ex.Message}");
            }
        }

        private async void OnReady(C_ReadyReq req)
        {
            try
            {
                if (RoomId == null)
                    return;

                IsReady = true;
                Console.WriteLine($"[Lobby] {Username} Ready");

                var room = _roomMgr.GetRoom(RoomId);
                if (room == null)
                    return;

                if (!room.IsBothReady())
                    return;

                // 이미 시작 중이면 중복 실행 방지
                lock (room)
                {
                    if (room.IsStarting)
                        return;
                    room.IsStarting = true;
                }

                // GameSession spawn
                var sessionInfo = _launcher.Launch(RoomId);
                _registry.Add(sessionInfo);

                Console.WriteLine($"[Lobby] 세션 생성: {sessionInfo.SessionId} — 포트 {sessionInfo.Port}");

                _roomMgr.RemoveRoom(RoomId);

                var gameStart = new S_GameStart
                {
                    SessionId = sessionInfo.SessionId,
                    GameServerPort = sessionInfo.Port,
                    GameServerHost = sessionInfo.Host
                };

                // Store pending — S_GameStart will be sent when SS_SessionReady arrives
                sessionInfo.Player1 = room.Player1;
                sessionInfo.Player2 = room.Player2;
                sessionInfo.PendingGameStart = gameStart;

                Console.WriteLine($"[Lobby] 세션 대기 중: {sessionInfo.SessionId} — GameSession Ready 대기");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Lobby] Ready 오류: {ex.Message}");
            }
        }
    }
}
