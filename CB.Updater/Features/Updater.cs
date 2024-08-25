namespace CB.Updater.Features;

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Responses;
using Utils;

public class Updater(Uri address)
{
    public Uri Address { get; } = address;
    public byte MaxAttempts { get; set; } = 5;

    private HttpClient? httpClient;

    private static string GetFileMd5(string filePath)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        var hash = md5.ComputeHash(stream);

        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }

    private static void DeleteDirectory(string directory)
    {
        foreach (var filePath in Directory.GetFiles(directory))
        {
            if (filePath == Process.GetCurrentProcess().MainModule?.FileName)
            {
                continue;
            }

            File.Delete(filePath);
        }

        foreach (var subDir in Directory.GetDirectories(directory))
        {
            DeleteDirectory(subDir);
        }
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            if (file is "update.json" or "updating.lock")
            {
                continue;
            }

            var destFile = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            if (subDir == "Backup")
            {
                continue;
            }

            var destSubDir = Path.Combine(targetDir, Path.GetFileName(subDir));
            CopyDirectory(subDir, destSubDir);
        }
    }

    public async Task<bool> Connect()
    {
        httpClient = new HttpClient
        {
            BaseAddress = Address,
            Timeout = TimeSpan.FromSeconds(5),
        };

        HttpResponseMessage? response = null;

        for (var i = 0; i < MaxAttempts; i++)
        {
            try
            {
                response = await httpClient.GetAsync("/status.json");
                break;
            }
            catch (Exception e)
            {
                LogUtil.Error(e.ToString());
                LogUtil.Error($"Connect failed, try again. (Attempt {i + 1}/{MaxAttempts})");
            }
        }

        if (response == null)
        {
            httpClient.Dispose();

            LogUtil.Error("Connect failed: unable to establish connection.");
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            httpClient.Dispose();

            LogUtil.Error("Connect failed: the server has closed the connection.");
            return false;
        }

        var content = await response.Content.ReadAsStringAsync();
        var status = JsonSerializer.Deserialize(content, SourceGenerationContext.Default.StatusResponse);

        if (status == null)
        {
            httpClient.Dispose();

            LogUtil.Error("Connect failed: the server returned an invalid response.");
            return false;
        }

        if (!status.Open)
        {
            httpClient.Dispose();

            LogUtil.Error($"Connect failed: the server has been shutdown. ({status.Message})");
            return false;
        }

        LogUtil.Info($"Successfully connected to server {Address}.");
        return true;
    }

    public async Task<bool> Update(UpdateInfoResponse updateInfo)
    {

        var downloadTasks = new List<Task<bool>>();
        var cancelToken = new CancellationTokenSource();

        var directory = Directory.GetCurrentDirectory();
        var directoryInfo = new DirectoryInfo(directory);

        var backupDirectory = MakeBackup();

        foreach (var fileInfo in directoryInfo.GetFiles("*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(directory, fileInfo.FullName).Replace("\\", "/");

            if (updateInfo.IncludeDirectories.All(x => !relativePath.StartsWith(x)))
            {
                continue;
            }

            if (!updateInfo.UpdateFiles!.TryGetValue(relativePath.ToLower(), out var md5))
            {
                fileInfo.Delete();
                updateInfo.UpdateFiles.Remove(relativePath.ToLower());
                continue;
            }

            if (GetFileMd5(fileInfo.FullName) == md5)
            {
                continue;
            }

            downloadTasks.Add(DownloadFile(relativePath, cancelToken.Token));
            updateInfo.UpdateFiles.Remove(relativePath.ToLower());
        }

        downloadTasks.AddRange(updateInfo.UpdateFiles!.Keys.Select(file => DownloadFile(file, cancelToken.Token)));

        while (downloadTasks.Count > 0)
        {
            var result = await Task.WhenAny(downloadTasks);
            downloadTasks.Remove(result);

            if (await result)
            {
                continue;
            }

            await cancelToken.CancelAsync();
            await Task.WhenAll(downloadTasks);

            RevertToBackup(backupDirectory);
            DeleteBackup(backupDirectory);

            httpClient?.Dispose();

            LogUtil.Error("Update failed: task canceled. Game files already reverted.");
            return false;
        }

        await Task.WhenAll(downloadTasks);
        DeleteBackup(backupDirectory);

        httpClient?.Dispose();

        LogUtil.Info("Successfully updated.");
        return true;
    }

    public async Task DownloadChangelog()
    {
        if (httpClient == null)
        {
            LogUtil.Error("Unable to download changelog: connection is null.");
            return;
        }

        HttpResponseMessage response;

        try
        {
            response = await httpClient.GetAsync("/update/Changelog.txt");
        }
        catch (Exception e)
        {
            LogUtil.Error(e.ToString());
            LogUtil.Error("Unable to download changelog: unable to establish connection.");
            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            LogUtil.Error("Unable to download changelog: the server has closed the connection.");
            return;
        }

        var content = await response.Content.ReadAsStringAsync();
        await File.WriteAllTextAsync("Changelog_New.txt", content);
    }

    public async Task<UpdateInfoResponse?> GetUpdateInfo()
    {
        if (httpClient == null)
        {
            LogUtil.Error("Unable to get update info: connection is null.");
            return null;
        }

        await DownloadChangelog();

        HttpResponseMessage response;

        try
        {
            response = await httpClient.GetAsync("/update.json");
        }
        catch (Exception e)
        {
            LogUtil.Error(e.ToString());
            LogUtil.Error("Unable to get update info: unable to establish connection.");
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            LogUtil.Error("Unable to get update info: the server has closed the connection.");
            return null;
        }

        var content = await response.Content.ReadAsStringAsync();
        var updateInfo = JsonSerializer.Deserialize(content, SourceGenerationContext.Default.UpdateInfoResponse);

        if (updateInfo == null)
        {
            LogUtil.Error("Unable to get update info: the server returned an invalid response.");
            return null;
        }

        LogUtil.Info("Successfully get the update info.");
        return updateInfo;
    }

    private async Task<bool> DownloadFile(string path, CancellationToken token)
    {
        if (httpClient == null)
        {
            return false;
        }

        HttpResponseMessage response;

        try
        {
            response = await httpClient.GetAsync($"/update/{path}", HttpCompletionOption.ResponseHeadersRead, token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception e)
        {
            LogUtil.Error(e.ToString());
            LogUtil.Error("Unable to download file: unable to establish connection.");
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            LogUtil.Error("Unable to download file: the server has closed the connection.");
            return false;
        }

        try
        {
            var fileBytes = await response.Content.ReadAsByteArrayAsync(token);
            await File.WriteAllBytesAsync(path, fileBytes, token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception e)
        {
            LogUtil.Error(e.ToString());
            LogUtil.Error("Unable to download file: IO exception.");
            return false;
        }

        LogUtil.Info($"Successfully downloaded file {path}.");
        return true;
    }

    private static string MakeBackup()
    {
        var tempPath = Path.Combine(Directory.GetCurrentDirectory(), "Backup");

        Directory.CreateDirectory(tempPath);

        CopyDirectory(Directory.GetCurrentDirectory(), tempPath);

        return tempPath;
    }

    private static void RevertToBackup(string backupDirectory)
    {
        DeleteDirectory(Directory.GetCurrentDirectory());
        CopyDirectory(backupDirectory, Directory.GetCurrentDirectory());
    }

    private static void DeleteBackup(string backupDirectory)
    {
        DeleteDirectory(backupDirectory);
    }
}