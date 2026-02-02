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
    /// Generates the composite ID for a state override document.
    /// Format: {configId}_{yyyy-MM-ddTHH:mm:ssZ}
    /// </summary>
    /// <param name="configId">The recurring task config ID</param>
    /// <param name="recurrenceDateAndTime">The UTC recurrence date/time</param>
    /// <returns>The composite ID string</returns>
    public static string GenerateId(string configId, DateTime recurrenceDateAndTime)
    {
        return $"{configId}_{recurrenceDateAndTime:yyyy-MM-ddTHH:mm:ssZ}";
    }
}
