using HereAndNowService.Models;

namespace HereAndNowService.Services;

/// <summary>
/// Service interface for managing reminder instances.
/// All operations are scoped to a specific user via the userId parameter.
/// </summary>
public interface IReminderInstanceService
{
    /// <summary>
    /// Gets all reminder instances for a specific user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <returns>A collection of reminder instances belonging to the user.</returns>
    IEnumerable<ReminderInstance> GetAll(string userId);

    /// <summary>
    /// Gets a reminder instance by its unique identifier for a specific user.
    /// </summary>
    /// <param name="id">The unique identifier of the reminder.</param>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <returns>The reminder instance if found and belongs to the user; otherwise, null.</returns>
    ReminderInstance? GetById(Guid id, string userId);

    /// <summary>
    /// Creates a new reminder instance.
    /// The userId should be set on the reminder model before calling this method.
    /// </summary>
    /// <param name="reminder">The reminder instance to create (must include UserId).</param>
    /// <returns>The created reminder instance with a generated ID.</returns>
    ReminderInstance Create(ReminderInstance reminder);

    /// <summary>
    /// Updates an existing reminder instance.
    /// The userId should be set on the reminder model before calling this method.
    /// </summary>
    /// <param name="id">The unique identifier of the reminder to update.</param>
    /// <param name="reminder">The updated reminder data (must include UserId).</param>
    /// <returns>The updated reminder instance if found; otherwise, null.</returns>
    ReminderInstance? Update(Guid id, ReminderInstance reminder);

    /// <summary>
    /// Soft-deletes a reminder instance by its unique identifier for a specific user.
    /// </summary>
    /// <param name="id">The unique identifier of the reminder to delete.</param>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <returns>True if the reminder was deleted; otherwise, false.</returns>
    bool Delete(Guid id, string userId);
}
