using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HereAndNowService.Commands;

/// <summary>
/// Command payload for rescheduling a task reminder to a new time.
/// Used to snooze or reschedule when a user wants the reminder at a different time.
/// </summary>
public class UpdateTaskReminderScheduledTimeCommand
{
    /// <summary>
    /// The ID of the task reminder to reschedule.
    /// Must be a valid GUID format (e.g., "660e8400-e29b-41d4-a716-446655440001").
    /// </summary>
    [Required(ErrorMessage = "taskReminderId is required")]
    [JsonPropertyName("taskReminderId")]
    public string TaskReminderId { get; set; } = string.Empty;

    /// <summary>
    /// The new scheduled time for the reminder in UTC.
    /// Must be a future datetime (validated at controller level).
    /// </summary>
    [Required(ErrorMessage = "scheduledTime is required")]
    [JsonPropertyName("scheduledTime")]
    public DateTime ScheduledTime { get; set; }
}
