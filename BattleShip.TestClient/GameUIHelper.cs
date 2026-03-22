using System;

namespace BattleShip.TestClient;

public static class GameUIHelper
{
    private static readonly ConsoleColor[] ShipColors =
    {
        ConsoleColor.Cyan,      // '0' - 항공모함
        ConsoleColor.Green,     // '1' - 전함
        ConsoleColor.Yellow,    // '2' - 순양함
        ConsoleColor.Magenta,   // '3' - 잠수함
        ConsoleColor.Red        // '4' - 구축함
    };

    // 내 보드(함선+피격) 와 상대 보드(공격 결과) 를 나란히 출력
    public static void PrintBothBoards(char[,] myBoard, char[,] enemyBoard)
    {
        Console.WriteLine();
        Console.WriteLine("  [내 보드]                      [상대 보드]");
        Console.WriteLine("  0 1 2 3 4 5 6 7 8 9            0 1 2 3 4 5 6 7 8 9");

        for (int y = 0; y < GameConfig.BOARD_SIZE; y++)
        {
            Console.Write($"{y} ");
            for (int x = 0; x < GameConfig.BOARD_SIZE; x++)
                PrintMyBoardCell(myBoard[x, y]);

            Console.Write($"   {y} ");
            for (int x = 0; x < GameConfig.BOARD_SIZE; x++)
                PrintEnemyBoardCell(enemyBoard[x, y]);

            Console.WriteLine();
        }
        Console.WriteLine();
    }

    public static void PrintPlacementBoard(char[,] map)
    {
        Console.WriteLine("\n  0 1 2 3 4 5 6 7 8 9");
        for (int y = 0; y < GameConfig.BOARD_SIZE; y++)
        {
            Console.Write($"{y} ");
            for (int x = 0; x < GameConfig.BOARD_SIZE; x++)
            {
                char c = map[x, y];
                if (c == GameConfig.BOARD_EMPTY)
                {
                    Console.Write(". ");
                }
                else
                {
                    Console.ForegroundColor = GetShipColor(c);
                    Console.Write($"{c} ");
                    Console.ResetColor();
                }
            }
            Console.WriteLine();
        }
        Console.WriteLine();
    }

    public static void PrintShipPlacementGuide()
    {
        Console.WriteLine("=== 배 배치 ===");
        Console.WriteLine("형식: X Y 방향");
        Console.WriteLine("방향: H=가로 V=세로\n");

        for (int i = 0; i < GameConfig.SHIP_COUNT; i++)
            Console.WriteLine($"  타입 {i} = {GameConfig.Ships.Names[i]}");
        Console.WriteLine();
    }

    public static void PrintLobbyMenu()
    {
        Console.WriteLine("1. 방 목록  2. 방 생성  3. 방 참가");
        Console.Write("> ");
    }

    public static string GetAttackResultString(int result, string? sunkShipName = null)
    {
        return result switch
        {
            0 => "Miss",
            1 => "Hit",
            2 => $"Sunk ({sunkShipName})",
            _ => "?"
        };
    }

    // ── private helpers ─────────────────────────────────────────

    private static void PrintMyBoardCell(char c)
    {
        switch (c)
        {
            case GameConfig.BOARD_HIT:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("X ");
                Console.ResetColor();
                break;
            case GameConfig.BOARD_MISS:
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("O ");
                Console.ResetColor();
                break;
            case GameConfig.BOARD_EMPTY:
                Console.Write(". ");
                break;
            default:
                if (c >= '0' && c < '0' + GameConfig.SHIP_COUNT)
                {
                    Console.ForegroundColor = ShipColors[c - '0'];
                    Console.Write($"{c} ");
                    Console.ResetColor();
                }
                else
                {
                    Console.Write($"{c} ");
                }
                break;
        }
    }

    private static void PrintEnemyBoardCell(char c)
    {
        switch (c)
        {
            case GameConfig.BOARD_HIT:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("X ");
                Console.ResetColor();
                break;
            case GameConfig.BOARD_MISS:
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("O ");
                Console.ResetColor();
                break;
            default:
                Console.Write(". ");
                break;
        }
    }

    private static ConsoleColor GetShipColor(char shipId)
    {
        if (shipId >= '0' && shipId < '0' + GameConfig.SHIP_COUNT)
            return ShipColors[shipId - '0'];
        return ConsoleColor.White;
    }
}
