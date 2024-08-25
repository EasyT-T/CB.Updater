namespace CB.Updater;

using System.CommandLine;
using System.Diagnostics;
using System.Text.Json;
using Features;
using Responses;
using Utils;

internal static class Program
{
    private const string Description =
        "Containment Breach updater, developed with C# on .NET Core. Created by EasyT_T, for downloading and applying the latest updates.";

    public static async Task<int> Main(string[] args)
    {
        LogUtil.Info("##### Containment Breach Updater #####");
        LogUtil.Info("Made by EasyT_T (https://github.com/EasyT-T)");
        LogUtil.Info("Special thanks to ZiYueCommentary (https://github.com/ZiYueCommentary)");

        if (!CheckInstance())
        {
            LogUtil.Error("Do not open multiple updater in the same directory.");
            return -1;
        }

        var rootCommand = new RootCommand(Description);

        var addressArgument = new Argument<string>("address", "The address of update server.");
        rootCommand.AddArgument(addressArgument);

        var getUpdateInfoCommand = new Command("get-update-info", "Get the update info.");
        rootCommand.AddCommand(getUpdateInfoCommand);

        var updateCommand = new Command("update", "Update the game");
        rootCommand.AddCommand(updateCommand);

        var updateInfoOption = new Option<string>(["--update-info", "-u"], "Set the update info for update the game.");
        updateCommand.AddOption(updateInfoOption);

        var batchmodeOption = new Option<bool>(["--batchmode", "-b"], "Enable batchmode will hide the console window.");
        rootCommand.AddGlobalOption(batchmodeOption);

        var outputOption = new Option<string>(["--output", "-o"],
            "Set the output path for log.")
        {
            IsRequired = true,
        };
        rootCommand.AddGlobalOption(outputOption);

        getUpdateInfoCommand.SetHandler(async (address, output, batchmode) => { await GetUpdateInfo(address, output, batchmode); }, addressArgument, outputOption, batchmodeOption);
        updateCommand.SetHandler(async (address, output, batchmode) => { await Update(address, output, batchmode);}, addressArgument, outputOption, batchmodeOption);

        return await rootCommand.InvokeAsync(args);
    }

    private static bool CheckInstance()
    {
        return !File.Exists("updating.lock") && !File.Exists("update.json");
    }

    private static async Task GetUpdateInfo(string address, string output, bool batchmode)
    {
        LogUtil.SetOutputPath(output);

        if (batchmode)
        {
            HideConsole();
        }

        var updater = new Updater(new Uri(address));

        if (!await updater.Connect())
        {
            return;
        }

        var updateInfo = await updater.GetUpdateInfo();

        if (updateInfo == null)
        {
            return;
        }

        var jsonText = JsonSerializer.Serialize(updateInfo, typeof(UpdateInfoResponse), SourceGenerationContext.Default);

        await File.WriteAllTextAsync("update.json", jsonText);
    }

    private static async Task Update(string address, string output, bool batchmode)
    {
        LogUtil.SetOutputPath(output);

        if (batchmode)
        {
            HideConsole();
        }

        File.Create("updating.lock").Close();

        var updater = new Updater(new Uri(address));

        if (!await updater.Connect())
        {
            File.Delete("updating.lock");
            return;
        }

        if (!File.Exists("update.json"))
        {
            LogUtil.Error("Update failed: please get update info first.");
            File.Delete("updating.lock");
            return;
        }

        var jsonText = await File.ReadAllTextAsync("update.json");
        var updateInfo = JsonSerializer.Deserialize(jsonText, SourceGenerationContext.Default.UpdateInfoResponse);

        if (updateInfo == null)
        {
            LogUtil.Error("Update failed: update info is invalid.");
            File.Delete("updating.lock");
            return;
        }

        var result = await updater.Update(updateInfo);

        if (!result)
        {
            LogUtil.Error("Update failed.");
            File.Delete("updating.lock");
            return;
        }

        File.Delete("update.json");
        File.Delete("updating.lock");

        Process.Start(updateInfo.GameFile);
    }

    private static void HideConsole()
    {
        var result = Win32ApiUtil.ShowWindow(Process.GetCurrentProcess().MainWindowHandle, 0);

        if (!result)
        {
            LogUtil.Warn("Unable to enable batchmode. (Call Win32Api Failed)");
        }
    }
}