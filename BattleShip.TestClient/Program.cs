using BattleShip.Common.Packets.Game;
using BattleShip.Common.Packets.Lobby;
using BattleShip.TestClient;

namespace BattleShip.TestClient;

class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.InputEncoding = System.Text.Encoding.UTF8;

        Console.Write("아이디: ");
        string username = Console.ReadLine()!;
        Console.Write("비밀번호: ");
        string password = Console.ReadLine()!;

        // ── 1. 로그인 ──────────────────────────────────────────────
        await using var gameClient = new GameClient();
        if (!await gameClient.LoginAsync(username, password))
        {
            Console.ReadKey();
            return;
        }

        // ── 2. 로비 진입 ───────────────────────────────────────────
        if (!await gameClient.EnterLobbyAsync())
        {
            Console.ReadKey();
            return;
        }

        // ── 3. 로비 메뉴 ───────────────────────────────────────────
        S_GameStart? gameStart = await ShowLobbyMenu(gameClient);
        if (gameStart == null)
        {
            Console.ReadKey();
            return;
        }

        // ── 4. 게임 연결 ───────────────────────────────────────────
        if (!await gameClient.ConnectGameAsync(gameStart))
        {
            Console.ReadKey();
            return;
        }

        // ── 5. 배 배치 ─────────────────────────────────────────────
        GameUIHelper.PrintShipPlacementGuide();

        var (placements, myBoard) = GetShipPlacements();
        if (!await gameClient.PlaceShipsAsync(placements))
        {
            Console.ReadKey();
            return;
        }

        var (placementSuccess, isMyTurn) = await gameClient.WaitForPlacementDoneAsync();
        if (!placementSuccess)
        {
            Console.ReadKey();
            return;
        }

        // ── 6. 게임 루프 ───────────────────────────────────────────
        var enemyBoard = InitializeBoard();
        Console.WriteLine("\n=== 게임 시작 ===");

        try
        {
            await PlayGameLoop(gameClient, myBoard, enemyBoard, isMyTurn, gameClient.PlayerIndex);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"게임 오류: {ex.Message}");
        }

        Console.WriteLine("\n게임 종료. 아무 키나 누르세요.");
        Console.ReadKey();
    }

    private static async Task<S_GameStart?> ShowLobbyMenu(GameClient gameClient)
    {
        while (true)
        {
            GameUIHelper.PrintLobbyMenu();
            string cmd = Console.ReadLine()!.Trim();

            if (cmd == "1")
                await ShowRoomList(gameClient);
            else if (cmd == "2")
            {
                var gameStart = await CreateAndJoinRoom(gameClient);
                if (gameStart != null)
                    return gameStart;
            }
            else if (cmd == "3")
            {
                var gameStart = await JoinExistingRoom(gameClient);
                if (gameStart != null)
                    return gameStart;
            }
        }
    }

    private static async Task ShowRoomList(GameClient gameClient)
    {
        var (success, rooms) = await gameClient.GetRoomListAsync();
        if (success && rooms != null)
        {
            if (rooms.Count == 0)
                Console.WriteLine("방 없음\n");
            else
                foreach (var r in rooms)
                    Console.WriteLine($"  [{r.RoomId}] {r.RoomName} ({r.PlayerCount}/2)");
            Console.WriteLine();
        }
    }

    private static async Task<S_GameStart?> CreateAndJoinRoom(GameClient gameClient)
    {
        Console.Write("방 이름: ");
        string roomName = Console.ReadLine()!;

        if (await gameClient.CreateRoomAsync(roomName) != null)
        {
            var (success, gs) = await gameClient.WaitForGameStartAsHostAsync();
            if (success && gs != null)
                return gs;
        }
        return null;
    }

    private static async Task<S_GameStart?> JoinExistingRoom(GameClient gameClient)
    {
        Console.Write("방 ID: ");
        string roomId = Console.ReadLine()!;

        if (await gameClient.JoinRoomAsync(roomId))
        {
            var (success, gs) = await gameClient.WaitForGameStartAsGuestAsync();
            if (success && gs != null)
                return gs;
        }
        return null;
    }

    private static async Task PlayGameLoop(
        GameClient gameClient,
        char[,] myBoard,
        char[,] enemyBoard,
        bool isMyTurn,
        int myPlayerIndex)
    {
        GameUIHelper.PrintBothBoards(myBoard, enemyBoard);

        while (true)
        {
            if (isMyTurn)
            {
                Console.WriteLine("=== 내 차례 ===");

                var (success, x, y) = await gameClient.GetAttackCoordinatesAsync();
                if (!success) continue;

                var (attackSuccess, attackRes) = await gameClient.AttackAsync(x, y);
                if (attackSuccess && attackRes != null)
                {
                    string resultStr = GameUIHelper.GetAttackResultString(attackRes.Result, attackRes.SunkShipName);
                    Console.WriteLine($"공격 결과: ({attackRes.X},{attackRes.Y}) → {resultStr}");

                    enemyBoard[attackRes.X, attackRes.Y] =
                        attackRes.Result == 0 ? GameConfig.BOARD_MISS : GameConfig.BOARD_HIT;
                }

                var next = await gameClient.ReceiveGamePacketAsync();
                if (next is S_GameOver go)
                {
                    GameUIHelper.PrintBothBoards(myBoard, enemyBoard);
                    PrintGameOver(go.WinnerId, myPlayerIndex);
                    return;
                }
                if (next is S_TurnNotify tn)
                {
                    isMyTurn = tn.IsMyTurn;
                    GameUIHelper.PrintBothBoards(myBoard, enemyBoard);
                }
            }
            else
            {
                Console.WriteLine("=== 상대방 차례... ===");
                var pkt = await gameClient.ReceiveGamePacketAsync();

                if (pkt is S_GameOver go)
                {
                    GameUIHelper.PrintBothBoards(myBoard, enemyBoard);
                    PrintGameOver(go.WinnerId, myPlayerIndex);
                    return;
                }

                if (pkt is S_OpponentAttack oa)
                {
                    string resultStr = GameUIHelper.GetAttackResultString(oa.Result, oa.SunkShipName);
                    Console.WriteLine($"피격: ({oa.X},{oa.Y}) → {resultStr}");

                    myBoard[oa.X, oa.Y] =
                        oa.Result == 0 ? GameConfig.BOARD_MISS : GameConfig.BOARD_HIT;

                    var next = await gameClient.ReceiveGamePacketAsync();
                    if (next is S_GameOver go2)
                    {
                        GameUIHelper.PrintBothBoards(myBoard, enemyBoard);
                        PrintGameOver(go2.WinnerId, myPlayerIndex);
                        return;
                    }
                    if (next is S_TurnNotify tn)
                    {
                        isMyTurn = tn.IsMyTurn;
                        GameUIHelper.PrintBothBoards(myBoard, enemyBoard);
                    }
                }
            }
        }
    }

    private static char[,] InitializeBoard()
    {
        var board = new char[GameConfig.BOARD_SIZE, GameConfig.BOARD_SIZE];
        for (int y = 0; y < GameConfig.BOARD_SIZE; y++)
            for (int x = 0; x < GameConfig.BOARD_SIZE; x++)
                board[x, y] = GameConfig.BOARD_EMPTY;
        return board;
    }

    private static (List<ShipPlacement>, char[,]) GetShipPlacements()
    {
        var placements = new List<ShipPlacement>();
        var boardMap = InitializeBoard();
        GameUIHelper.PrintPlacementBoard(boardMap);

        for (int i = 0; i < GameConfig.SHIP_COUNT; i++)
        {
            while (true)
            {
                Console.Write($"\n함선 {i}번 [{GameConfig.Ships.Names[i]}]: ");
                var parts = Console.ReadLine()!.Trim().Split(' ');

                if (!ValidateShipPlacement(parts, out byte x, out byte y, out bool horizontal))
                {
                    Console.WriteLine("  입력 오류. 다시 입력해주세요. (예: 0 0 H)");
                    continue;
                }

                int size = GameConfig.Ships.Sizes[i];
                if (!CheckBoundaries(x, y, size, horizontal))
                {
                    Console.WriteLine("  범위 초과. 다시 입력해주세요.");
                    continue;
                }

                if (CheckOverlap(boardMap, x, y, size, horizontal))
                {
                    Console.WriteLine("  다른 함선과 겹칩니다. 다시 입력해주세요.");
                    continue;
                }

                PlaceShipOnBoard(boardMap, x, y, size, horizontal, (char)('0' + i));
                placements.Add(new ShipPlacement
                {
                    ShipType = (byte)i,
                    X = x,
                    Y = y,
                    IsHorizontal = horizontal
                });

                GameUIHelper.PrintPlacementBoard(boardMap);
                break;
            }
        }

        return (placements, boardMap);
    }

    private static bool ValidateShipPlacement(string[] parts, out byte x, out byte y, out bool horizontal)
    {
        x = 0;
        y = 0;
        horizontal = true;

        return parts.Length >= 3
            && byte.TryParse(parts[0], out x) && x < GameConfig.BOARD_SIZE
            && byte.TryParse(parts[1], out y) && y < GameConfig.BOARD_SIZE
            && (parts[2].ToUpper() == "H" || parts[2].ToUpper() == "V")
            && (horizontal = parts[2].ToUpper() == "H") == horizontal;
    }

    private static bool CheckBoundaries(byte x, byte y, int size, bool horizontal)
    {
        return horizontal
            ? x + size <= GameConfig.BOARD_SIZE
            : y + size <= GameConfig.BOARD_SIZE;
    }

    private static bool CheckOverlap(char[,] boardMap, byte x, byte y, int size, bool horizontal)
    {
        for (int j = 0; j < size; j++)
        {
            int cx = horizontal ? x + j : x;
            int cy = horizontal ? y : y + j;
            if (boardMap[cx, cy] != GameConfig.BOARD_EMPTY)
                return true;
        }
        return false;
    }

    private static void PlaceShipOnBoard(char[,] boardMap, byte x, byte y, int size, bool horizontal, char symbol)
    {
        for (int j = 0; j < size; j++)
        {
            int cx = horizontal ? x + j : x;
            int cy = horizontal ? y : y + j;
            boardMap[cx, cy] = symbol;
        }
    }

    private static void PrintGameOver(string winnerId, int myPlayerIndex)
    {
        bool isWin = winnerId == myPlayerIndex.ToString();
        Console.WriteLine(isWin ? "\n승리!" : "\n패배...");
    }
}
