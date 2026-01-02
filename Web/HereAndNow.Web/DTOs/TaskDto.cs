using System.Text.Json.Serialization;

namespace HereAndNowService.DTOs;

/// <summary>
/// Data transfer object for Task API responses
/// </summary>
public class TaskDto
{
    /// <summary>
    /// Unique identifier for the task
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The name/title of the task
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Current state of the task (OnDeck, InProgress, Completed, Deleted)
    /// </summary>
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the task was created
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// UTC timestamp when the task was completed (null if not completed)
    /// </summary>
    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// ID of associated reminder (null if no reminder)
    /// </summary>
    [JsonPropertyName("reminderId")]
    public string? ReminderId { get; set; }
}
