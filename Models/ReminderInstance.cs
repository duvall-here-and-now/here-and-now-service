namespace HereAndNowService.Models;

/// <summary>
/// Represents a reminder instance with scheduling and status tracking.
/// </summary>
public class ReminderInstance
{
    /// <summary>
    /// Unique identifier for the reminder instance.
    /// </summary>
    public Guid id { get; set; }

    /// <summary>
    /// The text content of the reminder.
    /// </summary>
    public string text { get; set; }

    /// <summary>
    /// The date and time when the reminder is scheduled to occur.
    /// </summary>
    public DateTime scheduledDateAndTime { get; set; }

    /// <summary>
    /// The current status of the reminder.
    /// </summary>
    public ReminderStatus status { get; set; }
}
