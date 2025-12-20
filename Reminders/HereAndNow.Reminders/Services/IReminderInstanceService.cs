using HereAndNowService.Models;

namespace HereAndNowService.Services;

/// <summary>
/// Service interface for managing reminder instances.
/// </summary>
public interface IReminderInstanceService
{
    /// <summary>
    /// Gets all reminder instances for a specific user.
    /// </summary>
    /// <param name="userId">The user identifier to filter by.</param>
    /// <returns>A collection of reminder instances belonging to the user.</returns>
    IEnumerable<ReminderInstance> GetAll(string userId);

    /// <summary>
    /// Gets a reminder instance by its unique identifier for a specific user.
    /// </summary>
    /// <param name="id">The unique identifier of the reminder.</param>
    /// <param name="userId">The user identifier for partition key lookup.</param>
    /// <returns>The reminder instance if found and belongs to user; otherwise, null.</returns>
    ReminderInstance? GetById(Guid id, string userId);

    /// <summary>
    /// Creates a new reminder instance with client-provided ID.
    /// </summary>
    /// <param name="id">The unique identifier provided by the client.</param>
    /// <param name="userId">The user identifier (from JWT token).</param>
    /// <param name="text">The reminder text content.</param>
    /// <param name="scheduledDateAndTime">When the reminder should trigger.</param>
    /// <param name="shouldPlaySound">Whether to play a sound when triggered.</param>
    /// <param name="shouldDoVibration">Whether to vibrate when triggered.</param>
    /// <returns>The created reminder instance with timestamps.</returns>
    /// <exception cref="InvalidOperationException">Thrown if a reminder with the same ID already exists for the user.</exception>
    ReminderInstance Create(
        Guid id,
        string userId,
        string text,
        DateTime scheduledDateAndTime,
        bool shouldPlaySound,
        bool shouldDoVibration);

    /// <summary>
    /// Partially updates an existing reminder instance.
    /// Only non-null parameters will be updated.
    /// </summary>
    /// <param name="id">The unique identifier of the reminder to update.</param>
    /// <param name="userId">The user identifier for partition key lookup.</param>
    /// <param name="text">New text value, or null to keep existing.</param>
    /// <param name="scheduledDateAndTime">New scheduled time, or null to keep existing.</param>
    /// <param name="shouldPlaySound">New sound setting, or null to keep existing.</param>
    /// <param name="shouldDoVibration">New vibration setting, or null to keep existing.</param>
    /// <returns>The updated reminder instance if found; otherwise, null.</returns>
    ReminderInstance? Update(
        Guid id,
        string userId,
        string? text = null,
        DateTime? scheduledDateAndTime = null,
        bool? shouldPlaySound = null,
        bool? shouldDoVibration = null);

    /// <summary>
    /// Marks a reminder as completed and sets the completion timestamp.
    /// This operation is idempotent - completing an already completed reminder succeeds.
    /// </summary>
    /// <param name="id">The unique identifier of the reminder to complete.</param>
    /// <param name="userId">The user identifier for partition key lookup.</param>
    /// <returns>
    /// The completed reminder instance if found and not deleted;
    /// null if the reminder was not found or belongs to a different user.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown if the reminder is deleted.</exception>
    ReminderInstance? Complete(Guid id, string userId);

    /// <summary>
    /// Soft-deletes a reminder instance by setting IsDeleted flag and timestamp.
    /// This operation is idempotent - deleting an already deleted reminder succeeds.
    /// </summary>
    /// <param name="id">The unique identifier of the reminder to delete.</param>
    /// <param name="userId">The user identifier for partition key lookup.</param>
    /// <returns>True if the reminder was found (deleted or already deleted); otherwise, false.</returns>
    bool Delete(Guid id, string userId);
}
