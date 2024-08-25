namespace CB.Updater.Responses;

public class UpdateInfoResponse
{
    public string GameFile { get; init; } = string.Empty;
    public string LatestVersion { get; init; } = string.Empty;
    public string[] IncludeDirectories { get; init; } = [];
    public Dictionary<string, string>? UpdateFiles { get; init; }
}