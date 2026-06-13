using System.Text.Json.Serialization;

namespace HereAndNowService.DTOs;

/// <summary>
/// Data transfer object for RecurringTaskConfig API responses
/// </summary>
public class RecurringTaskConfigDto
{
    /// <summary>
    /// Unique identifier for the recurring task configuration
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The display text/name of the recurring task
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// The RRULE string defining the recurrence pattern (without 'RRULE:' prefix)
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

    /// <summary>
    /// Whether this config has Android reminder notifications enabled
    /// </summary>
    [JsonPropertyName("hasReminder")]
    public bool HasReminder { get; set; }
}
