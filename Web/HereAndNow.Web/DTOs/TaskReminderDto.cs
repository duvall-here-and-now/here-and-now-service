using System.Text.Json.Serialization;

namespace HereAndNowService.DTOs;

/// <summary>
/// Data transfer object for TaskReminder API responses
/// </summary>
public class TaskReminderDto
{
    /// <summary>
    /// Unique identifier for the reminder
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the associated task
    /// </summary>
    [JsonPropertyName("taskId")]
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// The name of the associated task (denormalized for display)
    /// </summary>
    [JsonPropertyName("taskName")]
    public string TaskName { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the reminder should trigger
    /// </summary>
    [JsonPropertyName("scheduledTime")]
    public DateTime ScheduledTime { get; set; }

    /// <summary>
    /// Whether the reminder has been dismissed
    /// </summary>
    [JsonPropertyName("isDismissed")]
    public bool IsDismissed { get; set; }

    /// <summary>
    /// UTC timestamp when the reminder was dismissed (null if not dismissed)
    /// </summary>
    [JsonPropertyName("dismissedAt")]
    public DateTime? DismissedAt { get; set; }

    /// <summary>
    /// UTC timestamp when the reminder was created
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
}
