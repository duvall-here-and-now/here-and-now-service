using System.Text.Json.Serialization;

namespace HereAndNowService.DTOs;

/// <summary>
/// Flat data transfer object for computed recurring task instances.
/// Maps from RecurringTaskInstance — no nested config object (AC7).
/// </summary>
public class RecurringTaskDto
{
    /// <summary>
    /// Composite ID: {configId}_{yyyy-MM-ddTHH:mm:ssZ}
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The parent recurring task configuration ID
    /// </summary>
    [JsonPropertyName("configId")]
    public string ConfigId { get; set; } = string.Empty;

    /// <summary>
    /// Task display text, derived from parent config
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// UTC occurrence date/time from RRULE computation
    /// </summary>
    [JsonPropertyName("recurrenceDateAndTime")]
    public DateTime RecurrenceDateAndTime { get; set; }

    /// <summary>
    /// Computed or overridden state: Scheduled | OnDeck | InProgress | Completed | Skipped
    /// </summary>
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// RRULE string (no "RRULE:" prefix) from parent config
    /// </summary>
    [JsonPropertyName("recurrenceRule")]
    public string RecurrenceRule { get; set; } = string.Empty;
}
