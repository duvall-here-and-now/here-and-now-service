using System.Net;
using HereAndNowService.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace HereAndNowService.Repositories;

/// <summary>
/// Cosmos DB implementation of ITaskReminderRepository
/// </summary>
public class TaskReminderRepository : ITaskReminderRepository
{
    private readonly Container _container;
    private readonly ILogger<TaskReminderRepository> _logger;

    /// <summary>
    /// Creates a new TaskReminderRepository instance
    /// </summary>
    /// <param name="cosmosClient">The Cosmos DB client</param>
    /// <param name="settings">Cosmos DB settings</param>
    /// <param name="logger">Logger instance</param>
    public TaskReminderRepository(
        CosmosClient cosmosClient,
        CosmosDbSettings settings,
        ILogger<TaskReminderRepository> logger)
    {
        _container = cosmosClient.GetContainer(settings.DatabaseName, settings.ContainerName);
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TaskReminderDocument> CreateAsync(TaskReminderDocument reminder)
    {
        _logger.LogDebug("Creating reminder {ReminderId} for user {UserId}", reminder.Id, reminder.UserId);

        var response = await _container.CreateItemAsync(
            reminder,
            new PartitionKey(reminder.UserId));

        _logger.LogInformation("Created reminder {ReminderId} for task {TaskId} and user {UserId}",
            reminder.Id, reminder.TaskId, reminder.UserId);
        return response.Resource;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TaskReminderDocument>> GetByUserIdAsync(string userId)
    {
        _logger.LogDebug("Getting reminders for user {UserId}", userId);

        var queryDefinition = new QueryDefinition(
            @"SELECT * FROM c
              WHERE c.type = 'TaskReminder'
              AND c.userId = @userId
              AND c.isDismissed = false
              ORDER BY c.scheduledTime ASC")
            .WithParameter("@userId", userId);

        var results = new List<TaskReminderDocument>();
        using var iterator = _container.GetItemQueryIterator<TaskReminderDocument>(
            queryDefinition,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(userId)
            });

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }

        _logger.LogDebug("Found {Count} reminders for user {UserId}", results.Count, userId);
        return results;
    }

    /// <inheritdoc />
    public async Task<TaskReminderDocument?> GetByIdAsync(string reminderId, string userId)
    {
        _logger.LogDebug("Getting reminder {ReminderId} for user {UserId}", reminderId, userId);

        try
        {
            var response = await _container.ReadItemAsync<TaskReminderDocument>(
                reminderId,
                new PartitionKey(userId));

            // Verify it's actually a TaskReminder document (not a Task with same ID)
            if (response.Resource.Type != "TaskReminder")
            {
                _logger.LogDebug("Document {ReminderId} is not a TaskReminder", reminderId);
                return null;
            }

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogDebug("Reminder {ReminderId} not found for user {UserId}", reminderId, userId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<TaskReminderDocument?> GetByTaskIdAsync(string taskId, string userId)
    {
        _logger.LogDebug("Getting reminder for task {TaskId} and user {UserId}", taskId, userId);

        var queryDefinition = new QueryDefinition(
            @"SELECT * FROM c
              WHERE c.type = 'TaskReminder'
              AND c.userId = @userId
              AND c.taskId = @taskId")
            .WithParameter("@userId", userId)
            .WithParameter("@taskId", taskId);

        using var iterator = _container.GetItemQueryIterator<TaskReminderDocument>(
            queryDefinition,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(userId)
            });

        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            var reminder = response.FirstOrDefault();
            if (reminder != null)
            {
                _logger.LogDebug("Found reminder {ReminderId} for task {TaskId}", reminder.Id, taskId);
            }
            return reminder;
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<TaskReminderDocument> UpdateAsync(TaskReminderDocument reminder)
    {
        _logger.LogDebug("Updating reminder {ReminderId} for user {UserId}", reminder.Id, reminder.UserId);

        var response = await _container.ReplaceItemAsync(
            reminder,
            reminder.Id,
            new PartitionKey(reminder.UserId));

        _logger.LogInformation("Updated reminder {ReminderId} for user {UserId}", reminder.Id, reminder.UserId);
        return response.Resource;
    }
}
