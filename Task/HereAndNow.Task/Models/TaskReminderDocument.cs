using System.Text.Json.Serialization;

namespace HereAndNowService.Models;

/// <summary>
/// Represents a TaskReminder document stored in Cosmos DB.
/// Stored in the same container as Task documents with type discriminator.
/// </summary>
public class TaskReminderDocument
{
    /// <summary>
    /// Unique identifier for the reminder
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Document type discriminator for Cosmos DB (always "TaskReminder")
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "TaskReminder";

    /// <summary>
    /// The user ID who owns this reminder (partition key)
    /// </summary>
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the associated task
    /// </summary>
    [JsonPropertyName("taskId")]
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// Denormalized task name for display without requiring a join
    /// </summary>
    [JsonPropertyName("taskName")]
    public string TaskName { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the reminder should be triggered
    /// </summary>
    [JsonPropertyName("scheduledTime")]
    public DateTime ScheduledTime { get; set; }

    /// <summary>
    /// Whether the reminder has been dismissed
    /// </summary>
    [JsonPropertyName("isDismissed")]
    public bool IsDismissed { get; set; } = false;

    /// <summary>
    /// UTC timestamp when the reminder was dismissed (null if not dismissed)
    /// </summary>
    [JsonPropertyName("dismissedAt")]
    public DateTime? DismissedAt { get; set; }

    /// <summary>
    /// UTC timestamp when the reminder was created
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// UTC timestamp when the reminder was last modified
    /// </summary>
    [JsonPropertyName("lastModifiedAt")]
    public DateTime LastModifiedAt { get; set; }
}
