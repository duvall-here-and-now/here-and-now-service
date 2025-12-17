namespace HereAndNowService.DTOs;

/// <summary>
/// Represents the computed state of a reminder for API responses.
/// </summary>
public enum ReminderState
{
    /// <summary>
    /// Reminder is scheduled for a future time.
    /// </summary>
    Scheduled,

    /// <summary>
    /// Reminder time has arrived or passed and is awaiting action.
    /// </summary>
    Active,

    /// <summary>
    /// Reminder has been marked as completed.
    /// </summary>
    Completed,

    /// <summary>
    /// Reminder has been soft-deleted.
    /// </summary>
    Deleted
}
