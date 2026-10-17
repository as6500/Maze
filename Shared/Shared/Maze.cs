using System;
namespace Shared;

public static class Maze
{
// '#' is a wall, '.' is floor. (0,0) is the top-left corner.
    public static readonly string[] Rows =
    {
        "############",
        "#..........#",
        "#..##......#",
        "#......##..#",
        "#..#.......#",
        "#..#...#...#",
        "#..........#",
        "############",
    };
    public static int Width => Rows[0].Length;
    public static int Height => Rows.Length;
    public static bool IsFree(int x, int y) =>
        y >= 0 && y < Height && x >= 0 && x < Width && Rows[y][x] == '.';
// Turn a direction letter into a step. Anything else is (0,0): no move.
    public static (int dx, int dy) Step(string dir) => dir switch
    {
        "r" => (1, 0),
        "l" => (-1, 0),
        "u" => (0, -1),
        "d" => (0, 1),
        _ => (0, 0),
    };
    // Works on a real console (redraws in place) and on a redirected one
// (prints a compact state line), so the same client runs in a test.
    public static void Render(IEnumerable<(int x, int y, char avatar)> players,
        string status)
    {
        var list = players.ToList();
        if (Console.IsOutputRedirected)
        {
            Console.WriteLine("state: " + string.Join(" ",
                list.Select(p => $"{p.avatar}({p.x},{p.y})")) + " " + status);
            return;
        }
        Console.SetCursorPosition(0, 0);
        for (int y = 0; y < Height; y++)
        {
            var row = Rows[y].ToCharArray();
            foreach (var p in list)
                if (p.y == y && p.x >= 0 && p.x < Width) row[p.x] = p.avatar;
            Console.WriteLine(new string(row));
        }
        Console.WriteLine(status.PadRight(Console.WindowWidth - 1));
    }
    // On a redirected stdin (tests, scripts) reads lines "r" "l" "u" "d" "q".
    public static string? ReadDirection()
    {
        if (Console.IsInputRedirected)
        {
            string? line = Console.ReadLine();
            return line == null || line == "q" ? null : line.Trim();
        }
        while (true)
        {
            var keyInfo = Console.ReadKey(intercept: true);
            var key = keyInfo.Key;
            string? dir = key switch
            {
                ConsoleKey.RightArrow or ConsoleKey.D => "r",
                ConsoleKey.LeftArrow or ConsoleKey.A => "l",
                ConsoleKey.UpArrow or ConsoleKey.W => "u",
                ConsoleKey.DownArrow or ConsoleKey.S => "d",
                ConsoleKey.Q or ConsoleKey.Escape => null,
                _ => ""
            };
            if (keyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift))
                return "S" + dir;

            return dir;
        }
    }

    public static (int x, int y) Wrap(int x, int y)
    {
        if (x <= 0) 
            x = Width - 2;
        else if (x >= Width - 1) 
            x = 1;
        if (y <= 0) 
            y = Height - 2;
        else if (y >= Height - 1) 
            y = 1;
        
        return (x, y);
    }
}