using System.Net;
using HereAndNowService.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace HereAndNowService.Repositories;

/// <summary>
/// Cosmos DB implementation of ITaskRepository
/// </summary>
public class TaskRepository : ITaskRepository
{
    private readonly Container _container;
    private readonly ILogger<TaskRepository> _logger;

    /// <summary>
    /// Creates a new TaskRepository instance
    /// </summary>
    /// <param name="cosmosClient">The Cosmos DB client</param>
    /// <param name="settings">Cosmos DB settings</param>
    /// <param name="logger">Logger instance</param>
    public TaskRepository(
        CosmosClient cosmosClient,
        CosmosDbSettings settings,
        ILogger<TaskRepository> logger)
    {
        _container = cosmosClient.GetContainer(settings.DatabaseName, settings.ContainerName);
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TaskDocument> CreateAsync(TaskDocument task)
    {
        _logger.LogDebug("Creating task {TaskId} for user {UserId}", task.Id, task.UserId);

        var response = await _container.CreateItemAsync(
            task,
            new PartitionKey(task.UserId));

        _logger.LogInformation("Created task {TaskId} for user {UserId}", task.Id, task.UserId);
        return response.Resource;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TaskDocument>> GetByUserIdAsync(string userId, string? state = null)
    {
        _logger.LogDebug("Getting tasks for user {UserId} with state filter {State}", userId, state ?? "all");

        var queryText = state is null
            ? "SELECT * FROM c WHERE c.userId = @userId AND c.type = 'Task'"
            : "SELECT * FROM c WHERE c.userId = @userId AND c.type = 'Task' AND c.state = @state";

        var queryDefinition = new QueryDefinition(queryText)
            .WithParameter("@userId", userId);

        if (state is not null)
        {
            queryDefinition = queryDefinition.WithParameter("@state", state);
        }

        var results = new List<TaskDocument>();
        using var iterator = _container.GetItemQueryIterator<TaskDocument>(
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

        _logger.LogDebug("Found {Count} tasks for user {UserId}", results.Count, userId);
        return results;
    }

    /// <inheritdoc />
    public async Task<TaskDocument?> GetByIdAsync(string taskId, string userId)
    {
        _logger.LogDebug("Getting task {TaskId} for user {UserId}", taskId, userId);

        try
        {
            var response = await _container.ReadItemAsync<TaskDocument>(
                taskId,
                new PartitionKey(userId));

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogDebug("Task {TaskId} not found for user {UserId}", taskId, userId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<TaskDocument> UpdateAsync(TaskDocument task)
    {
        _logger.LogDebug("Updating task {TaskId} for user {UserId}", task.Id, task.UserId);

        var response = await _container.ReplaceItemAsync(
            task,
            task.Id,
            new PartitionKey(task.UserId));

        _logger.LogInformation("Updated task {TaskId} for user {UserId}", task.Id, task.UserId);
        return response.Resource;
    }

    /// <inheritdoc />
    public async System.Threading.Tasks.Task DeleteAsync(string taskId, string userId)
    {
        _logger.LogDebug("Deleting task {TaskId} for user {UserId}", taskId, userId);

        await _container.DeleteItemAsync<TaskDocument>(
            taskId,
            new PartitionKey(userId));

        _logger.LogInformation("Deleted task {TaskId} for user {UserId}", taskId, userId);
    }
}
