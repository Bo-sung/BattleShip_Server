using BattleShip.Common;
using BattleShip.Common.Network;
using BattleShip.Common.Packets;
using BattleShip.Common.Packets.Auth;
using BattleShip.Common.Packets.Game;
using BattleShip.Common.Packets.Lobby;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace BattleShip.TestClient;

public class GameClient : IAsyncDisposable
{
    private NetworkStream? _lobbyStream;
    private NetworkStream? _gameStream;
    private TcpClient? _lobbyClient;
    private TcpClient? _gameClient;
    private string? _token;
    private string? _sessionId;
    public int PlayerIndex { get; private set; } = -1;
    public GameRuleConfig RuleConfig { get; private set; } = GameRuleConfig.Default;
    public string LobbyHost { get; private set; } = "127.0.0.1";
    public int LobbyPort { get; private set; } = 7002;

    private readonly RecvBuffer _lobbyRecvBuf = new RecvBuffer();
    private readonly RecvBuffer _gameRecvBuf  = new RecvBuffer();

    public async Task<bool> LoginAsync(string username, string password)
    {
        try
        {
            using var auth = new TcpClient();
            await auth.ConnectAsync(GameConfig.AUTH_SERVER_HOST, GameConfig.AUTH_SERVER_PORT);
            var stream = auth.GetStream();
            var buf = new RecvBuffer();

            await SendAsync(stream, new C_LoginReq { Username = username, PasswordHash = password });
            var res = await ReceiveAsync(stream, buf);

            if (res is S_LoginRes { Success: true } login)
            {
                _token = login.Token;
                LobbyHost = login.LobbyHost;
                LobbyPort = login.LobbyPort;
                Console.WriteLine("로그인 성공\n");
                return true;
            }

            Console.WriteLine($"로그인 실패: {(res as S_LoginRes)?.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"로그인 오류: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> EnterLobbyAsync()
    {
        try
        {
            _lobbyClient = new TcpClient();
            await _lobbyClient.ConnectAsync(LobbyHost, LobbyPort);
            _lobbyStream = _lobbyClient.GetStream();

            await SendAsync(_lobbyStream, new C_EnterLobbyReq { Token = _token! });
            var res = await ReceiveAsync(_lobbyStream, _lobbyRecvBuf);

            if (res is S_EnterLobbyRes { Success: true })
            {
                Console.WriteLine("로비 진입 성공\n");
                return true;
            }

            Console.WriteLine("로비 진입 실패");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"로비 진입 오류: {ex.Message}");
            return false;
        }
    }

    public async Task<(bool, List<RoomInfo>?)> GetRoomListAsync()
    {
        try
        {
            await SendAsync(_lobbyStream!, new C_RoomListReq());
            var res = await ReceiveAsync(_lobbyStream!, _lobbyRecvBuf);

            if (res is S_RoomListRes list)
                return (true, list.Rooms);

            return (false, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"방 목록 조회 오류: {ex.Message}");
            return (false, null);
        }
    }

    public async Task<string?> CreateRoomAsync(string roomName, string configName = "classic")
    {
        try
        {
            await SendAsync(_lobbyStream!, new C_RoomCreateReq { RoomName = roomName, ConfigName = configName });
            var res = await ReceiveAsync(_lobbyStream!, _lobbyRecvBuf);

            if (res is S_RoomCreateRes { Success: true } create)
            {
                Console.WriteLine($"방 생성 완료 — RoomId: {create.RoomId}");
                return create.RoomId;
            }

            Console.WriteLine("방 생성 실패");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"방 생성 오류: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> JoinRoomAsync(string roomId)
    {
        try
        {
            await SendAsync(_lobbyStream!, new C_RoomJoinReq { RoomId = roomId });
            var res = await ReceiveAsync(_lobbyStream!, _lobbyRecvBuf);

            if (res is S_RoomJoinRes { Success: true })
            {
                Console.WriteLine("방 참가 완료");
                return true;
            }

            Console.WriteLine("방 참가 실패");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"방 참가 오류: {ex.Message}");
            return false;
        }
    }

    public async Task<(bool, S_GameStart?)> WaitForGameStartAsHostAsync()
    {
        try
        {
            Console.WriteLine("상대방 입장 대기 중...");
            var notify = await ReceiveAsync(_lobbyStream!, _lobbyRecvBuf);

            if (notify is S_RoomUserJoined joined)
                Console.WriteLine($"{joined.Username} 입장\n");

            Console.WriteLine("Ready 전송...");
            await SendAsync(_lobbyStream!, new C_ReadyReq());

            var gs = await ReceiveAsync(_lobbyStream!, _lobbyRecvBuf);
            if (gs is S_GameStart gameStart)
            {
                Console.WriteLine($"게임 시작 — 포트 {gameStart.GameServerPort}\n");
                return (true, gameStart);
            }

            return (false, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"게임 시작 대기 오류: {ex.Message}");
            return (false, null);
        }
    }

    public async Task<(bool, S_GameStart?)> WaitForGameStartAsGuestAsync()
    {
        try
        {
            Console.WriteLine("Ready 전송...");
            await SendAsync(_lobbyStream!, new C_ReadyReq());

            var gs = await ReceiveAsync(_lobbyStream!, _lobbyRecvBuf);
            if (gs is S_GameStart gameStart)
            {
                Console.WriteLine($"게임 시작 — 포트 {gameStart.GameServerPort}\n");
                return (true, gameStart);
            }

            return (false, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"게임 시작 대기 오류: {ex.Message}");
            return (false, null);
        }
    }

    public async Task<bool> ConnectGameAsync(S_GameStart gameStart)
    {
        try
        {
            await Task.Delay(GameConfig.GAME_CONNECT_DELAY_MS);

            _gameClient = new TcpClient();
            await _gameClient.ConnectAsync(gameStart.GameServerHost, gameStart.GameServerPort);
            _gameStream = _gameClient.GetStream();

            await SendAsync(_gameStream, new C_EnterSessionReq { SessionId = gameStart.SessionId });
            var res = await ReceiveAsync(_gameStream, _gameRecvBuf);

            if (res is S_EnterSessionRes er)
            {
                _sessionId = gameStart.SessionId;
                PlayerIndex = er.PlayerIndex;
                Console.WriteLine($"세션 진입 — PlayerIndex: {er.PlayerIndex}");

                // 룰 설정 수신
                var rulePacket = await ReceiveAsync(_gameStream, _gameRecvBuf);
                if (rulePacket is S_GameRuleConfig rc)
                {
                    RuleConfig = rc.Config;
                    Console.WriteLine($"룰 수신 — 보드: {rc.Config.BoardSize}x{rc.Config.BoardSize}, 함선: {rc.Config.Ships.Count}종");
                }

                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"게임 연결 오류: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> PlaceShipsAsync(List<ShipPlacement> placements)
    {
        try
        {
            await SendAsync(_gameStream!, new C_PlaceShipsReq { Ships = placements });
            var res = await ReceiveAsync(_gameStream!, _gameRecvBuf);

            if (res is S_PlaceShipsRes { Success: true })
            {
                Console.WriteLine("배치 완료. 상대방 배치 대기 중...");
                return true;
            }

            if (res is S_PlaceShipsRes pr)
                Console.WriteLine($"배치 실패: {pr.Message}");

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"배치 오류: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> WaitForPlacementDoneAsync()
    {
        try
        {
            await ReceiveAsync(_gameStream!, _gameRecvBuf);  // S_PlacementDone만 받기
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"배치 완료 대기 오류: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> WaitForTurnNotifyAsync()
    {
        try
        {
            var turnNotify = await ReceiveAsync(_gameStream!, _gameRecvBuf);  // S_TurnNotify
            bool isMyTurn = (turnNotify as S_TurnNotify)?.IsMyTurn ?? false;
            return isMyTurn;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"턴 정보 대기 오류: {ex.Message}");
            return false;
        }
    }

    public async Task<(bool, int x, int y)> GetAttackCoordinatesAsync()
    {
        try
        {
            Console.Write("\n공격 좌표 (예: 3 5): ");
            var input = Console.ReadLine()?.Trim() ?? "";
            var parts = input.Split(' ');

            if (parts.Length != 2 || !byte.TryParse(parts[0], out byte x) || !byte.TryParse(parts[1], out byte y))
            {
                Console.WriteLine("올바른 형식으로 입력해주세요.");
                return (false, 0, 0);
            }

            return (true, x, y);
        }
        catch
        {
            return (false, 0, 0);
        }
    }

    public async Task<(bool, S_AttackRes?)> AttackAsync(int x, int y)
    {
        try
        {
            await SendAsync(_gameStream!, new C_AttackReq { X = (byte)x, Y = (byte)y });
            var res = await ReceiveAsync(_gameStream!, _gameRecvBuf);

            if (res is S_AttackRes ar)
                return (true, ar);

            return (false, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"공격 오류: {ex.Message}");
            return (false, null);
        }
    }

    public async Task<IPacket?> ReceiveGamePacketAsync()
    {
        return await ReceiveAsync(_gameStream!, _gameRecvBuf);
    }

    public async Task<bool> SelectSkillsAsync(byte skill1, byte skill2)
    {
        try
        {
            await SendAsync(_gameStream!, new C_SelectSkillsReq { Skill1 = skill1, Skill2 = skill2 });
            var res = await ReceiveAsync(_gameStream!, _gameRecvBuf);

            if (res is S_SelectSkillsRes { Success: true })
            {
                Console.WriteLine("스킬 선택 완료. 상대방 선택 대기 중...");
                return true;
            }

            Console.WriteLine("스킬 선택 실패");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"스킬 선택 오류: {ex.Message}");
            return false;
        }
    }

    public async Task<(bool success, bool isMyTurn)> WaitForBothSkillsSelectedAsync()
    {
        try
        {
            var pkt = await ReceiveAsync(_gameStream!, _gameRecvBuf);
            if (pkt is S_BothSkillsSelected)
            {
                Console.WriteLine("양쪽 스킬 선택 완료!");
                var turnNotify = await ReceiveAsync(_gameStream!, _gameRecvBuf);
                bool isMyTurn = (turnNotify as S_TurnNotify)?.IsMyTurn ?? false;
                Console.WriteLine(isMyTurn ? "\n내 차례로 시작!" : "\n상대방이 먼저 시작합니다.");
                return (true, isMyTurn);
            }
            return (false, false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"스킬 선택 완료 대기 오류: {ex.Message}");
            return (false, false);
        }
    }

    public async Task<(bool success, S_MoveRes?)> MoveAsync(byte shipType, sbyte dirX, sbyte dirY)
    {
        try
        {
            await SendAsync(_gameStream!, new C_MoveReq { ShipType = shipType, DirX = dirX, DirY = dirY });
            var res = await ReceiveAsync(_gameStream!, _gameRecvBuf);

            if (res is S_MoveRes mr)
                return (true, mr);

            return (false, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"이동 오류: {ex.Message}");
            return (false, null);
        }
    }

    public async Task<(bool success, IPacket?)> UseSkillAsync(byte skillType, byte targetX, byte targetY, byte shipType)
    {
        try
        {
            await SendAsync(_gameStream!, new C_SkillReq { SkillType = skillType, TargetX = targetX, TargetY = targetY, ShipType = shipType });
            var res = await ReceiveAsync(_gameStream!, _gameRecvBuf);

            // Success 필드 확인
            bool isSuccess = res switch
            {
                S_SkillAttackRes sar => true,  // Range Attack, Freeze, Recovery는 항상 성공
                S_SkillShieldRes ssr => ssr.Success,
                S_SkillRepairRes srr => srr.Success,
                S_SkillMoveRes smr => smr.Success,
                _ => false
            };

            if (isSuccess && (res is S_SkillAttackRes or S_SkillShieldRes or S_SkillRepairRes or S_SkillMoveRes))
                return (true, res);

            return (false, res);  // 실패 응답도 반환 (클라에서 확인용)
        }
        catch (Exception ex)
        {
            Console.WriteLine($"스킬 사용 오류: {ex.Message}");
            return (false, null);
        }
    }

    private async Task SendAsync(NetworkStream stream, IPacket packet)
    {
        byte[] data = PacketSerializer.Serialize(packet);
        await stream.WriteAsync(data);
    }

    private async Task<IPacket> ReceiveAsync(NetworkStream stream, RecvBuffer buf)
    {
        while (true)
        {
            if (buf.TryReadPacket(out var cached))
            {
                var packet = PacketSerializer.Deserialize(cached);
                buf.Compact();
                return packet;
            }

            if (buf.FreeSize == 0)
                buf.Compact();

            int n = await stream.ReadAsync(buf.WriteMemory);
            if (n == 0) throw new Exception("연결 끊김");
            buf.OnWritten(n);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _gameStream?.Dispose();
        _gameClient?.Dispose();
        _lobbyStream?.Dispose();
        _lobbyClient?.Dispose();
    }
}
