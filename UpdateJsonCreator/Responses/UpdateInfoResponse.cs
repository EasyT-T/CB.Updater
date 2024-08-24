namespace Creator.Responses;

public class UpdateInfoResponse
{
    public string GameFile { get; init; } = string.Empty;
    public string LastedVersion { get; init; } = string.Empty;
    public string[] IncludeDirectories { get; init; } = [];
    public Dictionary<string, string>? UpdateFiles { get; init; }
}