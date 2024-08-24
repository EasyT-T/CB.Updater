namespace Creator;

using System.Security.Cryptography;
using System.Text.Json;
using Features;
using Responses;
using Utils;

internal static class Program
{
    public static async Task<int> Main()
    {
        LogUtil.Info("Please enter the version number.");

        var version = Console.ReadLine() ?? string.Empty;

        LogUtil.Info("Please enter the game path.");

        var gamePath = Console.ReadLine() ?? string.Empty;

        LogUtil.Info("Please enter the name of the executable file for the game.");

        var gameFileName = Console.ReadLine() ?? string.Empty;

        LogUtil.Info("Please enter the include directories. (Please use commas to separate them. Skip Changelog.txt.)");

        var includeDirectories = Console.ReadLine()?.Split(',') ?? [];

        if (!Directory.Exists(gamePath))
        {
            LogUtil.Error("Please enter the correct game directory!");

            await Task.Delay(5000);
            return -1;
        }

        var status = new StatusResponse
        {
            Message = "OK",
            Open = true,
        };

        await File.WriteAllTextAsync("status.json", JsonSerializer.Serialize(status, SourceGenerationContext.Default.StatusResponse));

        var gameFileDictionary = new Dictionary<string, string>();

        var directoryInfo = new DirectoryInfo(gamePath);

        foreach (var fileInfo in directoryInfo.GetFiles("*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(gamePath, fileInfo.FullName).Replace("\\", "/");

            if (includeDirectories.All(x => !relativePath.StartsWith(x)))
            {
                continue;
            }

            var md5 = GetFileMd5(fileInfo.FullName);

            gameFileDictionary.Add(relativePath, md5);
        }

        var updateInfo = new UpdateInfoResponse
        {
            GameFile = gameFileName,
            LastedVersion = version,
            IncludeDirectories = includeDirectories,
            UpdateFiles = gameFileDictionary,
        };

        await File.WriteAllTextAsync("update.json", JsonSerializer.Serialize(updateInfo, SourceGenerationContext.Default.UpdateInfoResponse));

        Console.WriteLine("Done! Press any key to exit.");

        Console.ReadKey();
        return 0;
    }

    private static string GetFileMd5(string filePath)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        var hash = md5.ComputeHash(stream);

        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }
}