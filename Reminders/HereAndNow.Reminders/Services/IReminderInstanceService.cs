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
    /// Creates a new reminder instance. UserId should be set on the reminder model.
    /// </summary>
    /// <param name="reminder">The reminder instance to create (must have UserId set).</param>
    /// <returns>The created reminder instance with a generated ID.</returns>
    ReminderInstance Create(ReminderInstance reminder);

    /// <summary>
    /// Updates an existing reminder instance. UserId should be set on the reminder model.
    /// </summary>
    /// <param name="id">The unique identifier of the reminder to update.</param>
    /// <param name="reminder">The updated reminder data (must have UserId set).</param>
    /// <returns>The updated reminder instance if found; otherwise, null.</returns>
    ReminderInstance? Update(Guid id, ReminderInstance reminder);

    /// <summary>
    /// Soft-deletes a reminder instance by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the reminder to delete.</param>
    /// <param name="userId">The user identifier for partition key lookup.</param>
    /// <returns>True if the reminder was deleted; otherwise, false.</returns>
    bool Delete(Guid id, string userId);
}
