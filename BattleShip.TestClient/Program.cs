// BattleShip.TestClient/Program.cs
using BattleShip.Common.Network;
using BattleShip.Common.Packets;
using BattleShip.Common.Packets.Auth;
using BattleShip.Common.Packets.Game;
using BattleShip.Common.Packets.Lobby;
using System.Net.Sockets;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.InputEncoding = System.Text.Encoding.UTF8;

Console.Write("아이디: ");
string username = Console.ReadLine()!;
Console.Write("비밀번호: ");
string password = Console.ReadLine()!;

// ── 1. 로그인 ──────────────────────────────────────────────
string? token = await LoginAsync(username, password);
if (token == null) { Console.ReadKey(); return; }

// ── 2. 로비 진입 ───────────────────────────────────────────
using var lobbyClient = new TcpClient();
await lobbyClient.ConnectAsync("127.0.0.1", 7002);
var lobbyStream = lobbyClient.GetStream();

await SendAsync(lobbyStream, new C_EnterLobbyReq { Token = token });
var enterRes = await ReceiveAsync(lobbyStream);
if (enterRes is not S_EnterLobbyRes { Success: true })
{
    Console.WriteLine("로비 진입 실패");
    Console.ReadKey();
    return;
}
Console.WriteLine("로비 진입 성공\n");

// ── 3. 로비 메뉴 ───────────────────────────────────────────
NetworkStream? gameStream = null;
string? sessionId = null;

while (gameStream == null)
{
    Console.WriteLine("1. 방 목록  2. 방 생성  3. 방 참가");
    Console.Write("> ");
    string cmd = Console.ReadLine()!.Trim();

    if (cmd == "1")
    {
        await SendAsync(lobbyStream, new C_RoomListReq());
        var res = await ReceiveAsync(lobbyStream);
        if (res is S_RoomListRes list)
        {
            if (list.Rooms.Count == 0)
                Console.WriteLine("방 없음\n");
            else
                foreach (var r in list.Rooms)
                    Console.WriteLine($"  [{r.RoomId}] {r.RoomName} ({r.PlayerCount}/2)");
            Console.WriteLine();
        }
    }
    else if (cmd == "2")
    {
        Console.Write("방 이름: ");
        string roomName = Console.ReadLine()!;

        await SendAsync(lobbyStream, new C_RoomCreateReq { RoomName = roomName });
        var res = await ReceiveAsync(lobbyStream);
        if (res is S_RoomCreateRes { Success: true } create)
        {
            Console.WriteLine($"방 생성 완료 — RoomId: {create.RoomId}");
            Console.WriteLine("상대방 입장 대기 중...");

            var notify = await ReceiveAsync(lobbyStream);
            if (notify is S_RoomUserJoined joined)
                Console.WriteLine($"{joined.Username} 입장\n");

            Console.WriteLine("Ready 전송...");
            await SendAsync(lobbyStream, new C_ReadyReq());

            var gs = await ReceiveAsync(lobbyStream);
            if (gs is S_GameStart gameStart)
            {
                Console.WriteLine($"게임 시작 — 포트 {gameStart.GameServerPort}\n");
                gameStream = await ConnectGameAsync(gameStart);
                sessionId = gameStart.SessionId;
            }
        }
        else
        {
            Console.WriteLine("방 생성 실패\n");
        }
    }
    else if (cmd == "3")
    {
        Console.Write("방 ID: ");
        string roomId = Console.ReadLine()!;

        await SendAsync(lobbyStream, new C_RoomJoinReq { RoomId = roomId });
        var res = await ReceiveAsync(lobbyStream);
        if (res is S_RoomJoinRes { Success: true })
        {
            Console.WriteLine("방 참가 완료");
            Console.WriteLine("Ready 전송...");
            await SendAsync(lobbyStream, new C_ReadyReq());

            var gs = await ReceiveAsync(lobbyStream);
            if (gs is S_GameStart gameStart)
            {
                Console.WriteLine($"게임 시작 — 포트 {gameStart.GameServerPort}\n");
                gameStream = await ConnectGameAsync(gameStart);
                sessionId = gameStart.SessionId;
            }
        }
        else
        {
            Console.WriteLine("방 참가 실패\n");
        }
    }
}

// ── 4. 배 배치 ─────────────────────────────────────────────
Console.WriteLine("=== 배 배치 ===");
Console.WriteLine("형식: 타입 X Y 방향");
Console.WriteLine("방향: H=가로 V=세로\n");
Console.WriteLine("  타입 0 = 항공모함 (5칸)");
Console.WriteLine("  타입 1 = 전함     (4칸)");
Console.WriteLine("  타입 2 = 순양함   (3칸)");
Console.WriteLine("  타입 3 = 잠수함   (3칸)");
Console.WriteLine("  타입 4 = 구축함   (2칸)\n");

var placements = new List<ShipPlacement>();
var shipNames = new[] { "항공모함(5칸)", "전함(4칸)", "순양함(3칸)", "잠수함(3칸)", "구축함(2칸)" };
var shipSizes = new[] { 5, 4, 3, 3, 2 };
var boardMap = new char[10, 10];

for (int y = 0; y < 10; y++)
    for (int x = 0; x < 10; x++)
        boardMap[x, y] = '.';

PrintPlacementBoard(boardMap);

for (int i = 0; i < 5; i++)
{
    while (true)
    {
        Console.Write($"\n함선 {i}번 [{shipNames[i]}]: ");
        var parts = Console.ReadLine()!.Trim().Split(' ');

        if (parts.Length < 3
            || !byte.TryParse(parts[0], out byte x) || x > 9
            || !byte.TryParse(parts[1], out byte y) || y > 9
            || (parts[2].ToUpper() != "H" && parts[2].ToUpper() != "V"))
        {
            Console.WriteLine("  입력 오류. 다시 입력해주세요. (예: 0 0 H)");
            continue;
        }

        bool horizontal = parts[2].ToUpper() == "H";
        int size = shipSizes[i];

        if (horizontal && x + size > 10) { Console.WriteLine("  범위 초과. 다시 입력해주세요."); continue; }
        if (!horizontal && y + size > 10) { Console.WriteLine("  범위 초과. 다시 입력해주세요."); continue; }

        bool overlap = false;
        for (int j = 0; j < size; j++)
        {
            int cx = horizontal ? x + j : x;
            int cy = horizontal ? y : y + j;
            if (boardMap[cx, cy] != '.') { overlap = true; break; }
        }
        if (overlap) { Console.WriteLine("  다른 함선과 겹칩니다. 다시 입력해주세요."); continue; }

        char symbol = (char)('0' + i);
        for (int j = 0; j < size; j++)
        {
            int cx = horizontal ? x + j : x;
            int cy = horizontal ? y : y + j;
            boardMap[cx, cy] = symbol;
        }

        placements.Add(new ShipPlacement
        {
            ShipType = (byte)i,  // 타입은 순서로 자동 결정
            X = x,
            Y = y,
            IsHorizontal = horizontal
        });

        PrintPlacementBoard(boardMap);
        break;
    }
}

await SendAsync(gameStream!, new C_PlaceShipsReq { Ships = placements });
var placeRes = await ReceiveAsync(gameStream!);
if (placeRes is S_PlaceShipsRes pr && !pr.Success)
{
    Console.WriteLine($"배치 실패: {pr.Message}");
    Console.ReadKey();
    return;
}
Console.WriteLine("배치 완료. 상대방 배치 대기 중...");

var placementDone = await ReceiveAsync(gameStream!);  // S_PlacementDone
var turnNotify = await ReceiveAsync(gameStream!);  // S_TurnNotify

bool isMyTurn = (turnNotify as S_TurnNotify)?.IsMyTurn ?? false;
Console.WriteLine(isMyTurn ? "\n내 차례로 시작!" : "\n상대방이 먼저 시작합니다.");

// ── 5. 게임 루프 ───────────────────────────────────────────
Console.WriteLine("\n=== 게임 시작 ===");
PrintBoard(boardMap);

while (true)
{
    if (isMyTurn)
    {
        Console.Write("\n공격 좌표 (예: 3 5): ");
        var parts = Console.ReadLine()!.Trim().Split(' ');
        byte ax = byte.Parse(parts[0]);
        byte ay = byte.Parse(parts[1]);

        await SendAsync(gameStream!, new C_AttackReq { X = ax, Y = ay });

        var attackRes = await ReceiveAsync(gameStream!);
        if (attackRes is S_AttackRes ar)
        {
            string resultStr = ar.Result switch
            {
                0 => "Miss",
                1 => "Hit",
                2 => $"Sunk ({ar.SunkShipName})",
                _ => "?"
            };
            Console.WriteLine($"공격 결과: ({ar.X},{ar.Y}) → {resultStr}");
        }

        var next = await ReceiveAsync(gameStream!);
        if (next is S_GameOver go)
        {
            Console.WriteLine(go.WinnerId == "0" || go.WinnerId == username ? "\n승리!" : "\n패배...");
            break;
        }
        if (next is S_TurnNotify tn)
            isMyTurn = tn.IsMyTurn;
    }
    else
    {
        Console.WriteLine("상대방 차례...");

        var pkt = await ReceiveAsync(gameStream!);

        if (pkt is S_GameOver go)
        {
            Console.WriteLine(go.WinnerId == "0" || go.WinnerId == username ? "\n승리!" : "\n패배...");
            break;
        }

        if (pkt is S_OpponentAttack oa)
        {
            string resultStr = oa.Result switch
            {
                0 => "Miss",
                1 => "Hit",
                2 => $"Sunk ({oa.SunkShipName})",
                _ => "?"
            };
            Console.WriteLine($"피격: ({oa.X},{oa.Y}) → {resultStr}");

            var next = await ReceiveAsync(gameStream!);
            if (next is S_GameOver go2)
            {
                Console.WriteLine(go2.WinnerId == "0" || go2.WinnerId == username ? "\n승리!" : "\n패배...");
                break;
            }
            if (next is S_TurnNotify tn)
                isMyTurn = tn.IsMyTurn;
        }
    }
}

Console.WriteLine("\n게임 종료. 아무 키나 누르세요.");
Console.ReadKey();

// ── 헬퍼 ──────────────────────────────────────────────────

async Task<string?> LoginAsync(string user, string pass)
{
    using var auth = new TcpClient();
    await auth.ConnectAsync("127.0.0.1", 7001);
    var s = auth.GetStream();

    await SendAsync(s, new C_LoginReq { Username = user, PasswordHash = pass });
    var res = await ReceiveAsync(s);

    if (res is S_LoginRes { Success: true } login)
    {
        Console.WriteLine("로그인 성공\n");
        return login.Token;
    }

    Console.WriteLine($"로그인 실패: {(res as S_LoginRes)?.Message}");
    return null;
}

async Task<NetworkStream> ConnectGameAsync(S_GameStart gs)
{
    await Task.Delay(500);

    var client = new TcpClient();
    await client.ConnectAsync("127.0.0.1", gs.GameServerPort);
    var s = client.GetStream();

    await SendAsync(s, new C_EnterSessionReq { SessionId = gs.SessionId });
    var res = await ReceiveAsync(s);

    if (res is S_EnterSessionRes er)
        Console.WriteLine($"세션 진입 — PlayerIndex: {er.PlayerIndex}");

    return s;
}

void PrintBoard(char[,] map)
{
    Console.WriteLine("\n  0 1 2 3 4 5 6 7 8 9");
    for (int y = 0; y < 10; y++)
    {
        Console.Write($"{y} ");
        for (int x = 0; x < 10; x++)
            Console.Write(". ");
        Console.WriteLine();
    }
    Console.WriteLine();
}

void PrintPlacementBoard(char[,] map)
{
    Console.WriteLine("\n  0 1 2 3 4 5 6 7 8 9");
    for (int y = 0; y < 10; y++)
    {
        Console.Write($"{y} ");
        for (int x = 0; x < 10; x++)
        {
            char c = map[x, y];
            if (c == '.')
            {
                Console.Write(". ");
            }
            else
            {
                Console.ForegroundColor = c switch
                {
                    '0' => ConsoleColor.Cyan,
                    '1' => ConsoleColor.Green,
                    '2' => ConsoleColor.Yellow,
                    '3' => ConsoleColor.Magenta,
                    '4' => ConsoleColor.Red,
                    _ => ConsoleColor.White
                };
                Console.Write($"{c} ");
                Console.ResetColor();
            }
        }
        Console.WriteLine();
    }
    Console.WriteLine();
}

async Task SendAsync(NetworkStream s, IPacket packet)
{
    byte[] data = PacketSerializer.Serialize(packet);
    await s.WriteAsync(data);
}

async Task<IPacket> ReceiveAsync(NetworkStream s)
{
    var buf = new RecvBuffer();
    while (true)
    {
        int n = await s.ReadAsync(buf.WriteMemory);
        if (n == 0) throw new Exception("연결 끊김");
        buf.OnWritten(n);
        if (buf.TryReadPacket(out var raw))
            return PacketSerializer.Deserialize(raw);
        buf.Compact();
    }
}