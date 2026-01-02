using System.Text.Json.Serialization;

namespace HereAndNowService.DTOs;

/// <summary>
/// Standard error response format for API errors
/// </summary>
public class ErrorResponseDto
{
    /// <summary>
    /// The error details
    /// </summary>
    [JsonPropertyName("error")]
    public ErrorDetailsDto Error { get; set; } = new();
}

/// <summary>
/// Error details containing code and message
/// </summary>
public class ErrorDetailsDto
{
    /// <summary>
    /// Machine-readable error code (e.g., TASK_NOT_FOUND)
    /// </summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable error message
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
