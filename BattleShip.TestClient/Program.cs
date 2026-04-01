using BattleShip.Common;
using BattleShip.Common.Packets.Game;
using BattleShip.Common.Packets.Lobby;
using BattleShip.TestClient;

namespace BattleShip.TestClient;

class Program
{
    private static GameRuleConfig _ruleConfig = GameRuleConfig.Default;

    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.InputEncoding = System.Text.Encoding.UTF8;

        Console.Write("Auth 서버 주소 [127.0.0.1]: ");
        string authHost = Console.ReadLine()!.Trim();
        if (string.IsNullOrEmpty(authHost)) authHost = "127.0.0.1";
        GameConfig.AUTH_SERVER_HOST = authHost;

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

        // 서버에서 받은 룰 적용
        _ruleConfig = gameClient.RuleConfig;
        GameUIHelper.Configure(_ruleConfig);

        // ── 5. 배 배치 ─────────────────────────────────────────────
        GameUIHelper.PrintShipPlacementGuide();

        var (placements, myBoard) = GetShipPlacements();
        if (!await gameClient.PlaceShipsAsync(placements))
        {
            Console.ReadKey();
            return;
        }

        if (!await gameClient.WaitForPlacementDoneAsync())
        {
            Console.ReadKey();
            return;
        }

        // ── 6. 턴 정보 및 스킬 선택 ────────────────────────────────────
        bool isMyTurn = false;

        if (_ruleConfig.GameMode == (byte)GameModeType.SkillMode)  // StarBattle
        {
            Console.WriteLine("스킬을 선택하세요...\n");

            // 스킬 풀은 이미 ConnectGameAsync()에서 받은 RuleConfig에 포함되어 있음
            var skills = _ruleConfig.SkillPool;
            if (skills.Count > 0)
            {
                var (skill1, skill2) = SelectSkills(skills);
                if (!await gameClient.SelectSkillsAsync(skill1, skill2))
                {
                    Console.ReadKey();
                    return;
                }

                var (bothReady, turnResult) = await gameClient.WaitForBothSkillsSelectedAsync();
                if (!bothReady)
                {
                    Console.ReadKey();
                    return;
                }
                isMyTurn = turnResult;  // 스킬 선택 후 받은 턴 정보
                Console.WriteLine(isMyTurn ? "내 차례로 시작!" : "상대방이 먼저 시작합니다.");
            }
        }
        else
        {
            // Classic/Extended: 배치 완료 후 턴 정보 수신
            isMyTurn = await gameClient.WaitForTurnNotifyAsync();
            Console.WriteLine(isMyTurn ? "내 차례로 시작!" : "상대방이 먼저 시작합니다.");
        }

        // ── 7. 게임 루프 ───────────────────────────────────────────
        var enemyBoard = InitializeBoard();
        Console.WriteLine("\n=== 게임 시작 ===");

        try
        {
            await PlayGameLoop(gameClient, myBoard, enemyBoard, isMyTurn, gameClient.PlayerIndex, _ruleConfig.GameMode);
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
                    Console.WriteLine($"  [{r.RoomId}] {r.RoomName} [{r.ConfigName}] ({r.PlayerCount}/2)");
            Console.WriteLine();
        }
    }

    private static async Task<S_GameStart?> CreateAndJoinRoom(GameClient gameClient)
    {
        Console.Write("방 이름: ");
        string roomName = Console.ReadLine()!;

        Console.WriteLine("게임 모드 선택:");
        Console.WriteLine("  1. classic    (10x10, 5종 함선)");
        Console.WriteLine("  2. extended   (12x12, 6종 함선)");
        Console.WriteLine("  3. starbattle (12x12, 6종 함선, 스킬 시스템)");
        Console.Write("선택 [1]: ");
        string modeInput = Console.ReadLine()!.Trim();
        string configName = modeInput switch
        {
            "2" => "extended",
            "3" => "starbattle",
            _ => "classic"
        };

        if (await gameClient.CreateRoomAsync(roomName, configName) != null)
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
        int myPlayerIndex,
        byte gameMode)
    {
        GameUIHelper.PrintBothBoards(myBoard, enemyBoard);

        while (true)
        {
            if (isMyTurn)
            {
                Console.WriteLine("=== 내 차례 ===");

                if (gameMode == 2)  // StarBattle
                {
                    await HandleStarBattleTurn(gameClient, myBoard, enemyBoard);
                }
                else
                {
                    var (success, x, y) = await gameClient.GetAttackCoordinatesAsync();
                    if (!success) continue;

                    var (attackSuccess, attackRes) = await gameClient.AttackAsync(x, y);
                    if (attackSuccess && attackRes != null)
                    {
                        string resultStr = GameUIHelper.GetAttackResultString(attackRes.Result, attackRes.SunkShipName);
                        Console.WriteLine($"공격 결과: ({attackRes.X},{attackRes.Y}) → {resultStr}");

                        if (attackRes.Result == 3)
                            continue;

                        if (attackRes.Result == 0)
                            enemyBoard[attackRes.X, attackRes.Y] = GameConfig.BOARD_MISS;
                        else
                            enemyBoard[attackRes.X, attackRes.Y] = GameConfig.BOARD_HIT;
                    }
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
                    if (gameMode == 2 && tn is S_TurnNotify { Mana: > 0 })
                        Console.WriteLine($"현재 마나: {tn.Mana}");
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
                else if (pkt is S_OpponentSkillAttack osa)
                {
                    Console.WriteLine($"상대방이 스킬(범위공격) 사용");
                    foreach (var cell in osa.Cells)
                    {
                        string resultStr = GameUIHelper.GetAttackResultString(cell.Result, cell.SunkShipName);
                        Console.WriteLine($"  ({cell.X},{cell.Y}) → {resultStr}");
                        myBoard[cell.X, cell.Y] = cell.Result == 0 ? GameConfig.BOARD_MISS : GameConfig.BOARD_HIT;
                    }

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
                else if (pkt is S_OpponentRepaired)
                {
                    Console.WriteLine($"상대방이 수리 스킬 사용");
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

    /// <summary>
    /// 스킬 모드 턴 처리
    /// </summary>
    private static async Task HandleStarBattleTurn(GameClient gameClient, char[,] myBoard, char[,] enemyBoard)
    {
        Console.WriteLine("\n1. 공격 | 2. 이동 | 3. 스킬 사용");
        Console.Write("선택: ");
        string actionInput = Console.ReadLine()!.Trim();

        if (actionInput == "1")
        {
            var (success, x, y) = await gameClient.GetAttackCoordinatesAsync();
            if (!success) return;

            var (attackSuccess, attackRes) = await gameClient.AttackAsync(x, y);
            if (attackSuccess && attackRes != null)
            {
                string resultStr = GameUIHelper.GetAttackResultString(attackRes.Result, attackRes.SunkShipName);
                Console.WriteLine($"공격 결과: ({attackRes.X},{attackRes.Y}) → {resultStr}");

                if (attackRes.Result == 0)
                    enemyBoard[attackRes.X, attackRes.Y] = GameConfig.BOARD_MISS;
                else
                    enemyBoard[attackRes.X, attackRes.Y] = GameConfig.BOARD_HIT;
            }
        }
        else if (actionInput == "2")
        {
            Console.Write("함선 타입: ");
            if (byte.TryParse(Console.ReadLine(), out byte shipType))
            {
                Console.Write("방향 (예: -1 0 또는 1 1): ");
                var parts = Console.ReadLine()!.Split(' ');
                if (parts.Length == 2 && sbyte.TryParse(parts[0], out sbyte dirX) && sbyte.TryParse(parts[1], out sbyte dirY))
                {
                    var (moveSuccess, moveRes) = await gameClient.MoveAsync(shipType, dirX, dirY);
                    if (moveSuccess && moveRes != null)
                        Console.WriteLine(moveRes.Success ? "이동 성공" : $"이동 실패: {moveRes.Message}");
                }
            }
        }
        else if (actionInput == "3")
        {
            Console.Write("스킬 타입 (1-6): ");
            if (byte.TryParse(Console.ReadLine(), out byte skillType))
            {
                Console.Write("대상 좌표 (예: 5 5): ");
                var parts = Console.ReadLine()!.Split(' ');
                if (parts.Length >= 2 && byte.TryParse(parts[0], out byte targetX) && byte.TryParse(parts[1], out byte targetY))
                {
                    Console.Write("함선 타입 (일부 스킬): ");
                    byte.TryParse(Console.ReadLine(), out byte shipType);

                    var (_, skillRes) = await gameClient.UseSkillAsync(skillType, targetX, targetY, shipType);
                    if (skillRes != null)
                    {
                        // Success 필드 확인
                        bool actualSuccess = skillRes switch
                        {
                            S_SkillAttackRes => true,
                            S_SkillShieldRes { Success: var s } => s,
                            S_SkillRepairRes { Success: var s } => s,
                            S_SkillMoveRes { Success: var s } => s,
                            _ => false
                        };

                        if (actualSuccess)
                        {
                            Console.WriteLine("스킬 사용 성공!");
                            if (skillRes is S_SkillAttackRes sar)
                                Console.WriteLine($"마나: {sar.Mana}");
                            else if (skillRes is S_SkillShieldRes sr)
                                Console.WriteLine($"방어 설정 완료 (남은 마나: {sr.Mana})");
                            else if (skillRes is S_SkillRepairRes rr)
                                Console.WriteLine($"수리 완료 (남은 마나: {rr.Mana})");
                            else if (skillRes is S_SkillMoveRes mr)
                                Console.WriteLine($"이동 완료 (남은 마나: {mr.Mana})");
                        }
                        else
                        {
                            // 실패 메시지
                            if (skillRes is S_SkillShieldRes ssr)
                                Console.WriteLine($"방어 설정 실패");
                            else if (skillRes is S_SkillRepairRes srr)
                                Console.WriteLine($"수리 실패");
                            else if (skillRes is S_SkillMoveRes smr)
                                Console.WriteLine($"이동 실패: {smr.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("스킬 사용 오류");
                    }
                }
            }
        }
    }

    private static (byte skill1, byte skill2) SelectSkills(List<SkillDefinition> skillPool)
    {
        Console.WriteLine("\n이용 가능한 스킬:");
        foreach (var skill in skillPool)
            Console.WriteLine($"  {skill.Type}. {skill.Name} (비용: {skill.ManaCost}) - {skill.Description}");

        Console.Write("첫 번째 스킬 선택 (번호): ");
        byte.TryParse(Console.ReadLine(), out byte skill1);

        Console.Write("두 번째 스킬 선택 (번호): ");
        byte.TryParse(Console.ReadLine(), out byte skill2);

        return (skill1, skill2);
    }

    private static char[,] InitializeBoard()
    {
        int size = _ruleConfig.BoardSize;
        var board = new char[size, size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                board[x, y] = GameConfig.BOARD_EMPTY;
        return board;
    }

    private static (List<ShipPlacement>, char[,]) GetShipPlacements()
    {
        var placements = new List<ShipPlacement>();
        var boardMap = InitializeBoard();
        GameUIHelper.PrintPlacementBoard(boardMap);

        for (int i = 0; i < _ruleConfig.Ships.Count; i++)
        {
            var shipDef = _ruleConfig.Ships[i];
            while (true)
            {
                Console.Write($"\n함선 {i}번 [{shipDef.Name}({shipDef.Size}칸)]: ");
                var parts = Console.ReadLine()!.Trim().Split(' ');

                if (!ValidateShipPlacement(parts, out byte x, out byte y, out bool horizontal))
                {
                    Console.WriteLine("  입력 오류. 다시 입력해주세요. (예: 0 0 H)");
                    continue;
                }

                if (!CheckBoundaries(x, y, shipDef.Size, horizontal))
                {
                    Console.WriteLine("  범위 초과. 다시 입력해주세요.");
                    continue;
                }

                if (CheckOverlap(boardMap, x, y, shipDef.Size, horizontal))
                {
                    Console.WriteLine("  다른 함선과 겹칩니다. 다시 입력해주세요.");
                    continue;
                }

                PlaceShipOnBoard(boardMap, x, y, shipDef.Size, horizontal, (char)('0' + i));
                placements.Add(new ShipPlacement
                {
                    ShipType = shipDef.Type,
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

        int boardSize = _ruleConfig.BoardSize;
        return parts.Length >= 3
            && byte.TryParse(parts[0], out x) && x < boardSize
            && byte.TryParse(parts[1], out y) && y < boardSize
            && (parts[2].ToUpper() == "H" || parts[2].ToUpper() == "V")
            && (horizontal = parts[2].ToUpper() == "H") == horizontal;
    }

    private static bool CheckBoundaries(byte x, byte y, int size, bool horizontal)
    {
        int boardSize = _ruleConfig.BoardSize;
        return horizontal
            ? x + size <= boardSize
            : y + size <= boardSize;
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
