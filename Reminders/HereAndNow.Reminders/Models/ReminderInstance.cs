namespace HereAndNowService.Models;

/// <summary>
/// Represents a reminder instance with scheduling and completion tracking.
/// This model maps directly to the Cosmos DB storage schema.
/// </summary>
public class ReminderInstance
{
    /// <summary>
    /// Unique identifier for the reminder instance.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The unique identifier of the user who owns this reminder.
    /// Used as the partition key in Cosmos DB.
    /// </summary>
    public required string UserId { get; set; }

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
}
