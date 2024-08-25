namespace CB.Updater.Utils;

public static class LogUtil
{
    private static string OutputPath { get; set; } = string.Empty;

    public static void SetOutputPath(string path)
    {
        File.WriteAllText(path, string.Empty);
        OutputPath = path;
    }

    private static void Log(string prefix, string message, ConsoleColor color = ConsoleColor.Gray)
    {
        Console.ForegroundColor = color;
        Console.WriteLine($"[{prefix}]" + message);
        Console.ForegroundColor = ConsoleColor.Gray;

        if (!string.IsNullOrEmpty(OutputPath))
        {
            Task.Run(() => File.AppendAllText(OutputPath, "\r\n" + message)).Wait();
        }
    }

    public static void Info(string message) => Log("INFO", message, ConsoleColor.Cyan);
    public static void Warn(string message) => Log("WARN", message, ConsoleColor.Yellow);
    public static void Error(string message) => Log("ERROR", message, ConsoleColor.DarkRed);
}