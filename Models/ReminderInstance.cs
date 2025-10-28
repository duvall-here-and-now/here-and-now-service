namespace HereAndNowService.Models;

/// <summary>
/// Represents a reminder instance with scheduling and Status tracking.
/// </summary>
public class ReminderInstance
{
    /// <summary>
    /// Unique identifier for the reminder instance.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The Text content of the reminder.
    /// </summary>
    public required string Text { get; set; }

    /// <summary>
    /// The date and time when the reminder is scheduled to occur.
    /// </summary>
    public DateTime ScheduledDateAndTime { get; set; }

    /// <summary>
    /// The current Status of the reminder.
    /// </summary>
    public ReminderStatus Status { get; set; }
}
