using BattleShip.Common;
using System;

namespace BattleShip.TestClient;

public static class GameUIHelper
{
    private static readonly ConsoleColor[] ShipColors =
    {
        ConsoleColor.Cyan,
        ConsoleColor.Green,
        ConsoleColor.Yellow,
        ConsoleColor.Magenta,
        ConsoleColor.Red,
        ConsoleColor.Blue,
        ConsoleColor.DarkYellow,
        ConsoleColor.DarkCyan,
    };

    private static int _boardSize = 10;
    private static int _shipCount = 5;
    private static string[] _shipNames = { "항공모함(5칸)", "전함(4칸)", "순양함(3칸)", "잠수함(3칸)", "구축함(2칸)" };

    public static void Configure(GameRuleConfig config)
    {
        _boardSize = config.BoardSize;
        _shipCount = config.Ships.Count;
        _shipNames = config.Ships.Select(s => $"{s.Name}({s.Size}칸)").ToArray();
    }

    public static void PrintBothBoards(char[,] myBoard, char[,] enemyBoard)
    {
        Console.WriteLine();
        string header = string.Join(" ", Enumerable.Range(0, _boardSize));
        string padding = new string(' ', 12);
        Console.WriteLine($"  [내 보드]{padding}[상대 보드]");
        Console.WriteLine($"  {header}   {header}");

        for (int y = 0; y < _boardSize; y++)
        {
            Console.Write($"{y} ");
            for (int x = 0; x < _boardSize; x++)
                PrintMyBoardCell(myBoard[x, y]);

            Console.Write($"   {y} ");
            for (int x = 0; x < _boardSize; x++)
                PrintEnemyBoardCell(enemyBoard[x, y]);

            Console.WriteLine();
        }
        Console.WriteLine();
    }

    public static void PrintPlacementBoard(char[,] map)
    {
        string header = string.Join(" ", Enumerable.Range(0, _boardSize));
        Console.WriteLine($"\n  {header}");
        for (int y = 0; y < _boardSize; y++)
        {
            Console.Write($"{y} ");
            for (int x = 0; x < _boardSize; x++)
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

        for (int i = 0; i < _shipCount; i++)
            Console.WriteLine($"  타입 {i} = {_shipNames[i]}");
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
            3 => "이미 공격한 좌표입니다",
            _ => "?"
        };
    }

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
                if (c >= '0' && c < '0' + _shipCount)
                {
                    Console.ForegroundColor = GetShipColor(c);
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
        int idx = shipId - '0';
        if (idx >= 0 && idx < ShipColors.Length)
            return ShipColors[idx];
        return ConsoleColor.White;
    }
}
