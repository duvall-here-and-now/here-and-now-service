using Newtonsoft.Json;

namespace HereAndNowService.Persistence;

/// <summary>
/// Internal document model for Cosmos DB storage.
/// Maps to/from the domain ReminderInstance model.
/// </summary>
internal class ReminderDocument
{
    /// <summary>
    /// The document ID (lowercase for Cosmos DB convention).
    /// Maps to ReminderInstance.Id.
    /// </summary>
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The partition key - user identifier.
    /// Maps to ReminderInstance.UserId.
    /// </summary>
    [JsonProperty("userId")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// The reminder text content.
    /// </summary>
    [JsonProperty("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// When the reminder is scheduled.
    /// </summary>
    [JsonProperty("scheduledDateAndTime")]
    public DateTime ScheduledDateAndTime { get; set; }

    /// <summary>
    /// Whether the reminder has been completed.
    /// </summary>
    [JsonProperty("isCompleted")]
    public bool IsCompleted { get; set; }

    /// <summary>
    /// Whether the reminder has been soft-deleted.
    /// </summary>
    [JsonProperty("isDeleted")]
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Whether to play sound when triggered.
    /// </summary>
    [JsonProperty("shouldPlaySound")]
    public bool ShouldPlaySound { get; set; }

    /// <summary>
    /// Whether to vibrate when triggered.
    /// </summary>
    [JsonProperty("shouldDoVibration")]
    public bool ShouldDoVibration { get; set; }
}
