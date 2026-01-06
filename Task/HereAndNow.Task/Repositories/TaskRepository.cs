using System.Net;
using System.Text;
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
        _logger.LogDebug("Getting tasks for user {UserId} with state filter {State}", userId, state ?? "all (excluding Deleted)");

        // When no state filter is provided, exclude Deleted tasks by default (soft-delete pattern)
        // When a specific state is requested, return exactly that state (including Deleted if explicitly requested)
        var queryText = state is null
            ? "SELECT * FROM c WHERE c.userId = @userId AND c.type = 'Task' AND c.state != @deletedState"
            : "SELECT * FROM c WHERE c.userId = @userId AND c.type = 'Task' AND c.state = @state";

        var queryDefinition = new QueryDefinition(queryText)
            .WithParameter("@userId", userId);

        if (state is null)
        {
            queryDefinition = queryDefinition.WithParameter("@deletedState", TaskState.Deleted);
        }
        else
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
    /// <remarks>
    /// This method executes two separate queries (items + count) which may yield
    /// slightly inconsistent results if data changes between queries. The totalCount
    /// should be treated as approximate in high-concurrency scenarios. For a single-user
    /// task app, this race condition is negligible.
    /// </remarks>
    public async Task<PagedResult<TaskDocument>> GetByUserIdPagedAsync(
        string userId,
        string? state = null,
        string orderBy = "createdAt",
        string direction = "asc",
        int skip = 0,
        int take = 50)
    {
        _logger.LogDebug(
            "Getting paged tasks for user {UserId}: state={State}, orderBy={OrderBy}, direction={Direction}, skip={Skip}, take={Take}",
            userId, state ?? "all", orderBy, direction, skip, take);

        // Map API parameters to SQL field names (service layer validates these values)
        var orderDirection = direction.Equals("desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        var orderField = orderBy.Equals("completedAt", StringComparison.OrdinalIgnoreCase) ? "c.completedAt" : "c.createdAt";

        // Build the base query for items
        var queryBuilder = new StringBuilder(
            "SELECT * FROM c WHERE c.type = 'Task' AND c.userId = @userId AND c.state != @deletedState");

        if (!string.IsNullOrEmpty(state))
        {
            queryBuilder.Append(" AND c.state = @state");
        }

        queryBuilder.Append($" ORDER BY {orderField} {orderDirection}");
        queryBuilder.Append(" OFFSET @skip LIMIT @take");

        var queryDefinition = new QueryDefinition(queryBuilder.ToString())
            .WithParameter("@userId", userId)
            .WithParameter("@deletedState", TaskState.Deleted)
            .WithParameter("@skip", skip)
            .WithParameter("@take", take);

        if (!string.IsNullOrEmpty(state))
        {
            queryDefinition = queryDefinition.WithParameter("@state", state);
        }

        // Execute query to get items
        var items = new List<TaskDocument>();
        using var iterator = _container.GetItemQueryIterator<TaskDocument>(
            queryDefinition,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(userId)
            });

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            items.AddRange(response);
        }

        // Get total count for pagination metadata
        var totalCount = await GetCountAsync(userId, state);

        _logger.LogDebug("Found {Count} tasks (of {Total}) for user {UserId}", items.Count, totalCount, userId);

        return new PagedResult<TaskDocument>
        {
            Items = items,
            TotalCount = totalCount,
            HasMore = skip + items.Count < totalCount
        };
    }

    /// <summary>
    /// Gets the count of tasks for pagination metadata
    /// </summary>
    private async Task<int> GetCountAsync(string userId, string? state)
    {
        var countQueryText = state is null
            ? "SELECT VALUE COUNT(1) FROM c WHERE c.userId = @userId AND c.type = 'Task' AND c.state != @deletedState"
            : "SELECT VALUE COUNT(1) FROM c WHERE c.userId = @userId AND c.type = 'Task' AND c.state = @state";

        var countQuery = new QueryDefinition(countQueryText)
            .WithParameter("@userId", userId);

        if (state is null)
        {
            countQuery = countQuery.WithParameter("@deletedState", TaskState.Deleted);
        }
        else
        {
            countQuery = countQuery.WithParameter("@state", state);
        }

        using var countIterator = _container.GetItemQueryIterator<int>(
            countQuery,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(userId)
            });

        var countResponse = await countIterator.ReadNextAsync();
        return countResponse.FirstOrDefault();
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
    public async Task<TaskDocument> UpdateReminderIdAsync(string userId, string taskId, string? reminderId)
    {
        _logger.LogDebug("Patching reminderId on task {TaskId} for user {UserId}", taskId, userId);

        var patchOperations = new List<PatchOperation>
        {
            PatchOperation.Set("/reminderId", reminderId)
        };

        try
        {
            var response = await _container.PatchItemAsync<TaskDocument>(
                taskId,
                new PartitionKey(userId),
                patchOperations);

            _logger.LogInformation("Updated reminderId to {ReminderId} on task {TaskId} for user {UserId}",
                reminderId ?? "(null)", taskId, userId);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new Models.Exceptions.TaskNotFoundException(taskId);
        }
    }
}
