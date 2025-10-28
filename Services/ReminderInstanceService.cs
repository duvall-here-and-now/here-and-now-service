using System.Collections.Concurrent;
using HereAndNowService.Models;

namespace HereAndNowService.Services;

/// <summary>
/// Service implementation for managing reminder instances in memory.
/// </summary>
public class ReminderInstanceService : IReminderInstanceService
{
    private readonly ConcurrentDictionary<Guid, ReminderInstance> _reminders = new();

    /// <summary>
    /// Gets all reminder instances.
    /// </summary>
    /// <returns>A collection of all reminder instances.</returns>
    public IEnumerable<ReminderInstance> GetAll()
    {
        return _reminders.Values.ToList();
    }

    /// <summary>
    /// Gets a reminder instance by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the reminder.</param>
    /// <returns>The reminder instance if found; otherwise, null.</returns>
    public ReminderInstance? GetById(Guid id)
    {
        _reminders.TryGetValue(id, out var reminder);
        return reminder;
    }

    /// <summary>
    /// Creates a new reminder instance.
    /// </summary>
    /// <param name="reminder">The reminder instance to create.</param>
    /// <returns>The created reminder instance with a generated ID.</returns>
    public ReminderInstance Create(ReminderInstance reminder)
    {
        reminder.id = Guid.NewGuid();
        _reminders.TryAdd(reminder.id, reminder);
        return reminder;
    }

    /// <summary>
    /// Updates an existing reminder instance.
    /// </summary>
    /// <param name="id">The unique identifier of the reminder to update.</param>
    /// <param name="reminder">The updated reminder data.</param>
    /// <returns>The updated reminder instance if found; otherwise, null.</returns>
    public ReminderInstance? Update(Guid id, ReminderInstance reminder)
    {
        if (!_reminders.ContainsKey(id))
        {
            return null;
        }

        reminder.id = id;
        _reminders[id] = reminder;
        return reminder;
    }

    /// <summary>
    /// Deletes a reminder instance by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the reminder to delete.</param>
    /// <returns>True if the reminder was deleted; otherwise, false.</returns>
    public bool Delete(Guid id)
    {
        return _reminders.TryRemove(id, out _);
    }
}
