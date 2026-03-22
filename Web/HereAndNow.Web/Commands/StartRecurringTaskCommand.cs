using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HereAndNowService.Commands;

/// <summary>
/// Command payload for starting a recurring task instance (OnDeck → InProgress).
/// </summary>
public class StartRecurringTaskCommand
{
    /// <summary>
    /// The ID of the recurring task configuration.
    /// </summary>
    [Required(ErrorMessage = "recurringTaskConfigId is required")]
    [JsonPropertyName("recurringTaskConfigId")]
    public string RecurringTaskConfigId { get; set; } = string.Empty;

    /// <summary>
    /// The specific UTC recurrence date/time identifying the instance.
    /// </summary>
    [Required(ErrorMessage = "recurrenceDateAndTime is required")]
    [JsonPropertyName("recurrenceDateAndTime")]
    public DateTime RecurrenceDateAndTime { get; set; }
}
