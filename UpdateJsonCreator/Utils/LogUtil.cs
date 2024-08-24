namespace Creator.Utils;

public static class LogUtil
{
    private static void Log(string prefix, string message, ConsoleColor color = ConsoleColor.Gray)
    {
        Console.ForegroundColor = color;
        Console.WriteLine($"[{prefix}]" + message);
        Console.ForegroundColor = ConsoleColor.Gray;
    }

    public static void Info(string message) => Log("INFO", message, ConsoleColor.Cyan);
    public static void Warn(string message) => Log("WARN", message, ConsoleColor.Yellow);
    public static void Error(string message) => Log("ERROR", message, ConsoleColor.DarkRed);
}