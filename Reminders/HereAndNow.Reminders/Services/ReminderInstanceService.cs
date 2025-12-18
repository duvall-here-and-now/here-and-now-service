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
    /// Gets all reminder instances for a specific user.
    /// </summary>
    /// <param name="userId">The user identifier to filter by.</param>
    /// <returns>A collection of reminder instances belonging to the user.</returns>
    public IEnumerable<ReminderInstance> GetAll(string userId)
    {
        _logger.LogInformation("Retrieving all reminder instances for user: {UserId}", userId);
        var reminders = _reminders.Values
            .Where(r => r.UserId == userId && !r.IsDeleted)
            .ToList();
        _logger.LogInformation("Retrieved {Count} reminder instances for user: {UserId}", reminders.Count, userId);
        return reminders;
    }

    /// <summary>
    /// Gets a reminder instance by its unique identifier for a specific user.
    /// </summary>
    /// <param name="id">The unique identifier of the reminder.</param>
    /// <param name="userId">The user identifier for partition key lookup.</param>
    /// <returns>The reminder instance if found and belongs to user; otherwise, null.</returns>
    public ReminderInstance? GetById(Guid id, string userId)
    {
        _logger.LogInformation("Retrieving reminder instance with ID: {ReminderId} for user: {UserId}", id, userId);
        var found = _reminders.TryGetValue(id, out var reminder);

        if (found && reminder?.UserId == userId && !reminder.IsDeleted)
        {
            _logger.LogInformation("Successfully retrieved reminder instance with ID: {ReminderId}", id);
            return reminder;
        }

        _logger.LogWarning("Reminder instance with ID: {ReminderId} not found for user: {UserId}", id, userId);
        return null;
    }

    /// <summary>
    /// Creates a new reminder instance with server-controlled fields.
    /// </summary>
    public ReminderInstance Create(
        string userId,
        string text,
        DateTime scheduledDateAndTime,
        bool shouldPlaySound,
        bool shouldDoVibration)
    {
        _logger.LogInformation("Creating new reminder instance with Text: {ReminderText}, scheduled for: {ScheduledTime}",
            text, scheduledDateAndTime);

        var reminder = new ReminderInstance
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Text = text,
            ScheduledDateAndTime = scheduledDateAndTime,
            ShouldPlaySound = shouldPlaySound,
            ShouldDoVibration = shouldDoVibration,
            IsCompleted = false,
            IsDeleted = false,
            CreatedDateAndTime = DateTime.UtcNow,
            CompletedDateAndTime = null,
            DeletedDateAndTime = null
        };

        var added = _reminders.TryAdd(reminder.Id, reminder);

        if (added)
        {
            _logger.LogInformation("Successfully created reminder instance with ID: {ReminderId}", reminder.Id);
        }
        else
        {
            _logger.LogError("Failed to add reminder instance with ID: {ReminderId} to dictionary", reminder.Id);
        }

        return reminder;
    }

    /// <summary>
    /// Partially updates an existing reminder instance.
    /// </summary>
    public ReminderInstance? Update(
        Guid id,
        string userId,
        string? text = null,
        DateTime? scheduledDateAndTime = null,
        bool? shouldPlaySound = null,
        bool? shouldDoVibration = null)
    {
        _logger.LogInformation("Attempting to update reminder instance with ID: {ReminderId}", id);

        if (!_reminders.TryGetValue(id, out var existingReminder))
        {
            _logger.LogWarning("Cannot update - reminder instance with ID: {ReminderId} not found", id);
            return null;
        }

        if (existingReminder.UserId != userId)
        {
            _logger.LogWarning("Cannot update - reminder instance with ID: {ReminderId} belongs to different user", id);
            return null;
        }

        if (existingReminder.IsDeleted)
        {
            _logger.LogWarning("Cannot update - reminder instance with ID: {ReminderId} is deleted", id);
            return null;
        }

        _logger.LogInformation("Found existing reminder with ID: {ReminderId}", id);

        var updatedReminder = new ReminderInstance
        {
            Id = existingReminder.Id,
            UserId = existingReminder.UserId,
            Text = text ?? existingReminder.Text,
            ScheduledDateAndTime = scheduledDateAndTime ?? existingReminder.ScheduledDateAndTime,
            ShouldPlaySound = shouldPlaySound ?? existingReminder.ShouldPlaySound,
            ShouldDoVibration = shouldDoVibration ?? existingReminder.ShouldDoVibration,
            IsCompleted = existingReminder.IsCompleted,
            IsDeleted = existingReminder.IsDeleted,
            CreatedDateAndTime = existingReminder.CreatedDateAndTime,
            CompletedDateAndTime = existingReminder.CompletedDateAndTime,
            DeletedDateAndTime = existingReminder.DeletedDateAndTime
        };

        if (_reminders.TryUpdate(id, updatedReminder, existingReminder))
        {
            _logger.LogInformation("Successfully updated reminder instance with ID: {ReminderId}", id);
            return updatedReminder;
        }

        _logger.LogError("Failed to update reminder instance with ID: {ReminderId} due to concurrent modification", id);
        return null;
    }

    /// <summary>
    /// Marks a reminder as completed and sets the completion timestamp.
    /// </summary>
    public ReminderInstance? Complete(Guid id, string userId)
    {
        _logger.LogInformation("Attempting to complete reminder instance with ID: {ReminderId} for user: {UserId}", id, userId);

        if (!_reminders.TryGetValue(id, out var existingReminder))
        {
            _logger.LogWarning("Cannot complete - reminder instance with ID: {ReminderId} not found", id);
            return null;
        }

        if (existingReminder.UserId != userId)
        {
            _logger.LogWarning("Cannot complete - reminder instance with ID: {ReminderId} belongs to different user", id);
            return null;
        }

        if (existingReminder.IsDeleted)
        {
            _logger.LogWarning("Cannot complete - reminder instance with ID: {ReminderId} is deleted", id);
            throw new InvalidOperationException("Cannot complete a deleted reminder.");
        }

        // Idempotent: if already completed, return current state
        if (existingReminder.IsCompleted)
        {
            _logger.LogInformation("Reminder instance with ID: {ReminderId} is already completed", id);
            return existingReminder;
        }

        var completedReminder = new ReminderInstance
        {
            Id = existingReminder.Id,
            UserId = existingReminder.UserId,
            Text = existingReminder.Text,
            ScheduledDateAndTime = existingReminder.ScheduledDateAndTime,
            ShouldPlaySound = existingReminder.ShouldPlaySound,
            ShouldDoVibration = existingReminder.ShouldDoVibration,
            IsCompleted = true,
            IsDeleted = existingReminder.IsDeleted,
            CreatedDateAndTime = existingReminder.CreatedDateAndTime,
            CompletedDateAndTime = DateTime.UtcNow,
            DeletedDateAndTime = existingReminder.DeletedDateAndTime
        };

        if (_reminders.TryUpdate(id, completedReminder, existingReminder))
        {
            _logger.LogInformation("Successfully completed reminder instance with ID: {ReminderId}", id);
            return completedReminder;
        }

        _logger.LogError("Failed to complete reminder instance with ID: {ReminderId} due to concurrent modification", id);
        return null;
    }

    /// <summary>
    /// Soft-deletes a reminder instance by setting its IsDeleted flag to true.
    /// </summary>
    /// <param name="id">The unique identifier of the reminder to delete.</param>
    /// <param name="userId">The user identifier for partition key lookup.</param>
    /// <returns>True if the reminder was soft-deleted; otherwise, false.</returns>
    public bool Delete(Guid id, string userId)
    {
        _logger.LogInformation("Attempting to soft-delete reminder instance with ID: {ReminderId} for user: {UserId}", id, userId);

        if (!_reminders.TryGetValue(id, out var existingReminder))
        {
            _logger.LogWarning("Cannot delete - reminder instance with ID: {ReminderId} not found for user: {UserId}", id, userId);
            return false;
        }

        if (existingReminder.UserId != userId)
        {
            _logger.LogWarning("Cannot delete - reminder instance with ID: {ReminderId} belongs to different user", id);
            return false;
        }

        // Idempotent: if already deleted, return success
        if (existingReminder.IsDeleted)
        {
            _logger.LogInformation("Reminder instance with ID: {ReminderId} is already deleted", id);
            return true;
        }

        var deletedReminder = new ReminderInstance
        {
            Id = existingReminder.Id,
            UserId = existingReminder.UserId,
            Text = existingReminder.Text,
            ScheduledDateAndTime = existingReminder.ScheduledDateAndTime,
            IsCompleted = existingReminder.IsCompleted,
            IsDeleted = true,
            ShouldPlaySound = existingReminder.ShouldPlaySound,
            ShouldDoVibration = existingReminder.ShouldDoVibration,
            CreatedDateAndTime = existingReminder.CreatedDateAndTime,
            CompletedDateAndTime = existingReminder.CompletedDateAndTime,
            DeletedDateAndTime = DateTime.UtcNow
        };

        if (_reminders.TryUpdate(id, deletedReminder, existingReminder))
        {
            _logger.LogInformation("Successfully soft-deleted reminder instance with ID: {ReminderId}", id);
            return true;
        }

        _logger.LogError("Failed to soft-delete reminder instance with ID: {ReminderId} due to concurrent modification", id);
        return false;
    }
}
