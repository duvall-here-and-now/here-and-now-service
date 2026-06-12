using System.Text.Json.Serialization;

namespace HereAndNowService.Models;

/// <summary>
/// Represents a RecurringTaskStateOverride document stored in Cosmos DB.
/// Stores explicit state changes for specific recurring task instances.
/// </summary>
public class RecurringTaskStateOverrideDocument
{
    /// <summary>
    /// Composite identifier: {configId}_{yyyy-MM-ddTHH:mm:ssZ}
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Document type discriminator for Cosmos DB
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "RecurringTaskStateOverride";

    /// <summary>
    /// The user ID who owns this override (partition key)
    /// </summary>
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Reference to the parent RecurringTaskConfig.Id
    /// </summary>
    [JsonPropertyName("configId")]
    public string ConfigId { get; set; } = string.Empty;

    /// <summary>
    /// The specific UTC date/time of the recurrence instance this override applies to
    /// </summary>
    [JsonPropertyName("recurrenceDateAndTime")]
    public DateTime RecurrenceDateAndTime { get; set; }

    /// <summary>
    /// The overridden state (OnDeck, InProgress, Completed, Skipped)
    /// </summary>
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when this override was last updated
    /// </summary>
    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Whether the derived reminder for this instance has been dismissed by the user
    /// </summary>
    [JsonPropertyName("reminderDismissed")]
    public bool ReminderDismissed { get; set; } = false;

    /// <summary>
    /// Generates the composite ID for a state override document.
    /// Format: {configId}_{yyyy-MM-ddTHH:mm:ssZ}
    /// </summary>
    /// <param name="configId">The recurring task config ID</param>
    /// <param name="recurrenceDateAndTime">The UTC recurrence date/time (must have DateTimeKind.Utc)</param>
    /// <returns>The composite ID string</returns>
    /// <exception cref="ArgumentException">Thrown if recurrenceDateAndTime is not UTC</exception>
    public static string GenerateId(string configId, DateTime recurrenceDateAndTime)
    {
        if (recurrenceDateAndTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                $"DateTime must be UTC (Kind was {recurrenceDateAndTime.Kind})",
                nameof(recurrenceDateAndTime));
        }

        return $"{configId}_{recurrenceDateAndTime:yyyy-MM-ddTHH:mm:ssZ}";
    }
}
