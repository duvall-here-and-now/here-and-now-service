using System.Collections.Concurrent;
using HereAndNowService.Models;
using Microsoft.Extensions.Logging;

namespace HereAndNowService.Services;

/// <summary>
/// Service implementation for managing reminder instances in memory.
/// </summary>
public class ReminderInstanceService : IReminderInstanceService
{
    private readonly ConcurrentDictionary<Guid, ReminderInstance> _reminders = new();
    private readonly ILogger<ReminderInstanceService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReminderInstanceService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public ReminderInstanceService(ILogger<ReminderInstanceService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets all reminder instances.
    /// </summary>
    /// <returns>A collection of all reminder instances.</returns>
    public IEnumerable<ReminderInstance> GetAll()
    {
        _logger.LogInformation("Retrieving all reminder instances");
        var reminders = _reminders.Values.ToList();
        _logger.LogInformation("Retrieved {Count} reminder instances", reminders.Count);
        return reminders;
    }

    /// <summary>
    /// Gets a reminder instance by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the reminder.</param>
    /// <returns>The reminder instance if found; otherwise, null.</returns>
    public ReminderInstance? GetById(Guid id)
    {
        _logger.LogInformation("Retrieving reminder instance with ID: {ReminderId}", id);
        var found = _reminders.TryGetValue(id, out var reminder);

        if (found)
        {
            _logger.LogInformation("Successfully retrieved reminder instance with ID: {ReminderId}", id);
        }
        else
        {
            _logger.LogWarning("Reminder instance with ID: {ReminderId} not found", id);
        }

        return reminder;
    }

    /// <summary>
    /// Creates a new reminder instance.
    /// </summary>
    /// <param name="reminder">The reminder instance to create.</param>
    /// <returns>The created reminder instance with a generated ID.</returns>
    public ReminderInstance Create(ReminderInstance reminder)
    {
        _logger.LogInformation("Creating new reminder instance with text: {ReminderText}, scheduled for: {ScheduledTime}, status: {Status}",
            reminder.text, reminder.scheduledDateAndTime, reminder.status);

        reminder.id = Guid.NewGuid();
        var added = _reminders.TryAdd(reminder.id, reminder);

        if (added)
        {
            _logger.LogInformation("Successfully created reminder instance with ID: {ReminderId}", reminder.id);
        }
        else
        {
            _logger.LogError("Failed to add reminder instance with ID: {ReminderId} to dictionary", reminder.id);
        }

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
        _logger.LogInformation("Attempting to update reminder instance with ID: {ReminderId}", id);

        if (_reminders.TryGetValue(id, out var existingReminder))
        {
            _logger.LogInformation("Found existing reminder with ID: {ReminderId}. Old status: {OldStatus}, New status: {NewStatus}",
                id, existingReminder.status, reminder.status);

            reminder.id = id;
            if (_reminders.TryUpdate(id, reminder, existingReminder))
            {
                _logger.LogInformation("Successfully updated reminder instance with ID: {ReminderId}", id);
                return reminder;
            }
            else
            {
                _logger.LogError("Failed to update reminder instance with ID: {ReminderId} due to concurrent modification", id);
            }
        }
        else
        {
            _logger.LogWarning("Cannot update - reminder instance with ID: {ReminderId} not found", id);
        }

        return null;
    }

    /// <summary>
    /// Deletes a reminder instance by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the reminder to delete.</param>
    /// <returns>True if the reminder was deleted; otherwise, false.</returns>
    public bool Delete(Guid id)
    {
        _logger.LogInformation("Attempting to delete reminder instance with ID: {ReminderId}", id);
        var deleted = _reminders.TryRemove(id, out _);

        if (deleted)
        {
            _logger.LogInformation("Successfully deleted reminder instance with ID: {ReminderId}", id);
        }
        else
        {
            _logger.LogWarning("Cannot delete - reminder instance with ID: {ReminderId} not found", id);
        }

        return deleted;
    }
}
