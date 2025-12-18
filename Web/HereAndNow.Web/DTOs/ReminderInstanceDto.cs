namespace HereAndNowService.DTOs;

/// <summary>
/// Data transfer object for reminder instances exposed through the API.
/// </summary>
public class ReminderInstanceDto
{
    /// <summary>
    /// Unique identifier for the reminder instance.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The text content of the reminder.
    /// </summary>
    public required string Text { get; set; }

    /// <summary>
    /// The date and time when the reminder is scheduled to occur.
    /// </summary>
    public DateTime ScheduledDateAndTime { get; set; }

    /// <summary>
    /// Indicates whether the reminder has been completed.
    /// </summary>
    public bool IsCompleted { get; set; }

    /// <summary>
    /// Indicates whether the reminder has been soft-deleted.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Indicates whether the reminder should play a sound when triggered.
    /// </summary>
    public bool ShouldPlaySound { get; set; }

    /// <summary>
    /// Indicates whether the reminder should vibrate the device when triggered.
    /// </summary>
    public bool ShouldDoVibration { get; set; }

    /// <summary>
    /// The date and time when the reminder was created.
    /// </summary>
    public DateTime CreatedDateAndTime { get; set; }

    /// <summary>
    /// The date and time when the reminder was completed. Null if not completed.
    /// </summary>
    public DateTime? CompletedDateAndTime { get; set; }

    /// <summary>
    /// The date and time when the reminder was deleted. Null if not deleted.
    /// </summary>
    public DateTime? DeletedDateAndTime { get; set; }

    /// <summary>
    /// The computed state of the reminder based on its flags and scheduled time.
    /// </summary>
    public ReminderState State => this switch
    {
        { IsDeleted: true } => ReminderState.Deleted,
        { IsCompleted: true } => ReminderState.Completed,
        _ when DateTime.UtcNow >= ScheduledDateAndTime => ReminderState.Active,
        _ => ReminderState.Scheduled
    };
}
