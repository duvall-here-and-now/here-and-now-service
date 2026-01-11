using System.Text.Json.Serialization;

namespace HereAndNowService.Models;

/// <summary>
/// Represents a Task document stored in Cosmos DB
/// </summary>
public class TaskDocument
{
    /// <summary>
    /// Unique identifier for the task
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Document type discriminator for Cosmos DB
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "Task";

    /// <summary>
    /// The user ID who owns this task (partition key)
    /// </summary>
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// The name/title of the task
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Current state of the task (OnDeck, InProgress, Completed, Deleted)
    /// </summary>
    [JsonPropertyName("state")]
    public string State { get; set; } = TaskState.OnDeck;

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

    /// <summary>
    /// UTC timestamp when the task was last modified
    /// </summary>
    [JsonPropertyName("lastModifiedAt")]
    public DateTime LastModifiedAt { get; set; }
}
