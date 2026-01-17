using System.Text.Json.Serialization;

namespace HereAndNowService.Commands;

/// <summary>
/// Base class for command responses. Specific commands may return different
/// response types, but errors always follow the standard ErrorResponseDto format.
/// </summary>
public class CommandResponse
{
    /// <summary>
    /// Whether the command executed successfully
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// Optional message providing additional context
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
