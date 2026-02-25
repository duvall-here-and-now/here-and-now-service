using System.Text.Json.Serialization;

namespace HereAndNowService.Models;

/// <summary>
/// Represents a RecurringTaskConfig document stored in Cosmos DB.
/// Defines the recurrence pattern for a repeating task.
/// </summary>
public class RecurringTaskConfigDocument
{
    /// <summary>
    /// Unique identifier for the recurring task configuration (GUID)
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Document type discriminator for Cosmos DB
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "RecurringTaskConfig";

    /// <summary>
    /// The user ID who owns this configuration (partition key)
    /// </summary>
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// The display text/name of the recurring task
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// The RRULE string defining the recurrence pattern (without 'RRULE:' prefix).
    /// Example: "FREQ=DAILY;BYHOUR=7;BYMINUTE=0;BYSECOND=0"
    /// </summary>
    [JsonPropertyName("rrule")]
    public string Rrule { get; set; } = string.Empty;

    /// <summary>
    /// The UTC start date and time for the recurrence pattern
    /// </summary>
    [JsonPropertyName("startDateAndTime")]
    public DateTime StartDateAndTime { get; set; }

    /// <summary>
    /// UTC timestamp when the configuration was created
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
}
