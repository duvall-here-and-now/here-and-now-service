using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HereAndNowService.Commands;

/// <summary>
/// Command payload for updating an existing recurring task configuration.
/// </summary>
public class UpdateRecurringTaskConfigCommand
{
    /// <summary>
    /// The ID of the recurring task config to update (GUID format).
    /// </summary>
    [Required(ErrorMessage = "id is required")]
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The updated display text for the recurring task.
    /// </summary>
    [Required(ErrorMessage = "text is required")]
    [MinLength(1, ErrorMessage = "text cannot be empty")]
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Updated RRULE string (without RRULE: prefix).
    /// </summary>
    [Required(ErrorMessage = "recurrenceRule is required")]
    [JsonPropertyName("recurrenceRule")]
    public string RecurrenceRule { get; set; } = string.Empty;

    /// <summary>
    /// Updated UTC start date/time for the recurrence pattern.
    /// </summary>
    [Required(ErrorMessage = "startDateAndTime is required")]
    [JsonPropertyName("startDateAndTime")]
    public DateTime StartDateAndTime { get; set; }

    /// <summary>
    /// Whether Android reminder notifications are enabled. Optional — omitted defaults to false (backward compat).
    /// </summary>
    [JsonPropertyName("hasReminder")]
    public bool HasReminder { get; set; } = false;
}
