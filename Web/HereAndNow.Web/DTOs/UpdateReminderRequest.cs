using System.ComponentModel.DataAnnotations;

namespace HereAndNowService.DTOs;

/// <summary>
/// Request DTO for updating reminder properties.
/// Only non-null fields will be updated (partial update semantics).
/// Cannot modify: IsCompleted, IsDeleted, timestamps.
/// </summary>
public record UpdateReminderRequest
{
    /// <summary>
    /// New reminder text. Null means no change.
    /// </summary>
    [StringLength(1000, MinimumLength = 1)]
    public string? Text { get; init; }

    /// <summary>
    /// New scheduled time. Null means no change.
    /// </summary>
    public DateTime? ScheduledDateAndTime { get; init; }

    /// <summary>
    /// New sound setting. Null means no change.
    /// </summary>
    public bool? ShouldPlaySound { get; init; }

    /// <summary>
    /// New vibration setting. Null means no change.
    /// </summary>
    public bool? ShouldDoVibration { get; init; }
}
