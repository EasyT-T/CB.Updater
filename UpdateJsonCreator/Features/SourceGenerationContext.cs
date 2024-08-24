namespace Creator.Features;

using System.Text.Json.Serialization;
using Responses;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(StatusResponse))]
[JsonSerializable(typeof(UpdateInfoResponse))]
internal partial class SourceGenerationContext : JsonSerializerContext;