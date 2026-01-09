using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HereAndNowService.Validation;

namespace HereAndNowService.DTOs;

/// <summary>
/// Request body for snoozing (rescheduling) a reminder to a new time.
/// </summary>
public class SnoozeReminderDto
{
    /// <summary>
    /// The new scheduled time for the reminder. Must be in the future (UTC).
    /// </summary>
    [Required(ErrorMessage = "ScheduledTime is required")]
    [FutureTimeValidation(ErrorMessage = "Scheduled time must be in the future")]
    [JsonPropertyName("scheduledTime")]
    public DateTime ScheduledTime { get; set; }
}
