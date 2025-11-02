using HereAndNowService.Models;

namespace HereAndNowService.Services;

/// <summary>
/// Service interface for managing reminder instances.
/// </summary>
public interface IReminderInstanceService
{
    /// <summary>
    /// Gets all reminder instances.
    /// </summary>
    /// <returns>A collection of all reminder instances.</returns>
    IEnumerable<ReminderInstance> GetAll();

    /// <summary>
    /// Gets a reminder instance by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the reminder.</param>
    /// <returns>The reminder instance if found; otherwise, null.</returns>
    ReminderInstance? GetById(Guid id);

    /// <summary>
    /// Creates a new reminder instance.
    /// </summary>
    /// <param name="reminder">The reminder instance to create.</param>
    /// <returns>The created reminder instance with a generated ID.</returns>
    ReminderInstance Create(ReminderInstance reminder);

    /// <summary>
    /// Updates an existing reminder instance.
    /// </summary>
    /// <param name="id">The unique identifier of the reminder to update.</param>
    /// <param name="reminder">The updated reminder data.</param>
    /// <returns>The updated reminder instance if found; otherwise, null.</returns>
    ReminderInstance? Update(Guid id, ReminderInstance reminder);

    /// <summary>
    /// Deletes a reminder instance by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the reminder to delete.</param>
    /// <returns>True if the reminder was deleted; otherwise, false.</returns>
    bool Delete(Guid id);
}
