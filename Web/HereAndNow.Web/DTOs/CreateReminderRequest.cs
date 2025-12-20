using System.ComponentModel.DataAnnotations;

namespace HereAndNowService.DTOs;

/// <summary>
/// Request DTO for creating a new reminder.
/// Client must provide the Id (UUID) for idempotent create operations.
/// Server will set: UserId, CreatedDateAndTime, IsCompleted=false, IsDeleted=false
/// </summary>
public record CreateReminderRequest
{
    /// <summary>
    /// The unique identifier for the reminder. Must be a valid UUID provided by the client.
    /// This enables idempotent retry logic and offline-first patterns.
    /// </summary>
    [Required]
    public Guid Id { get; init; }

    /// <summary>
    /// The reminder text content.
    /// </summary>
    [Required]
    [StringLength(1000, MinimumLength = 1)]
    public required string Text { get; init; }

    /// <summary>
    /// When the reminder should trigger.
    /// </summary>
    [Required]
    public DateTime ScheduledDateAndTime { get; init; }

    /// <summary>
    /// Whether to play a sound when the reminder triggers.
    /// </summary>
    public bool ShouldPlaySound { get; init; }

    /// <summary>
    /// Whether to vibrate when the reminder triggers.
    /// </summary>
    public bool ShouldDoVibration { get; init; }
}
