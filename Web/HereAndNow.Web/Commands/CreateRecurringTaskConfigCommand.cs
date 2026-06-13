using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HereAndNowService.Commands;

/// <summary>
/// Command payload for creating a new recurring task configuration with a client-generated ID.
/// </summary>
public class CreateRecurringTaskConfigCommand
{
    /// <summary>
    /// Client-generated unique identifier for the config (GUID format).
    /// </summary>
    [Required(ErrorMessage = "id is required")]
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The display text for the recurring task.
    /// </summary>
    [Required(ErrorMessage = "text is required")]
    [MinLength(1, ErrorMessage = "text cannot be empty")]
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// RRULE string (without RRULE: prefix). E.g., "FREQ=DAILY;BYHOUR=7;BYMINUTE=0;BYSECOND=0"
    /// </summary>
    [Required(ErrorMessage = "recurrenceRule is required")]
    [JsonPropertyName("recurrenceRule")]
    public string RecurrenceRule { get; set; } = string.Empty;

    /// <summary>
    /// UTC start date/time for the recurrence pattern.
    /// </summary>
    [Required(ErrorMessage = "startDateAndTime is required")]
    [JsonPropertyName("startDateAndTime")]
    public DateTime StartDateAndTime { get; set; }

    /// <summary>
    /// Whether to enable Android reminder notifications for this config at creation time.
    /// Omitted field defaults to false (backward-compatible with pre-v3 clients).
    /// </summary>
    [JsonPropertyName("hasReminder")]
    public bool HasReminder { get; set; } = false;
}
