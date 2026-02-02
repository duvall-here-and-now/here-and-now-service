using System.Net;
using HereAndNowService.Models;
using HereAndNowService.Models.Exceptions;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace HereAndNowService.Repositories;

/// <summary>
/// Cosmos DB implementation of IRecurringTaskRepository
/// </summary>
public class RecurringTaskRepository : IRecurringTaskRepository
{
    private readonly Container _container;
    private readonly ILogger<RecurringTaskRepository> _logger;

    /// <summary>
    /// Creates a new RecurringTaskRepository instance
    /// </summary>
    /// <param name="cosmosClient">The Cosmos DB client</param>
    /// <param name="settings">Cosmos DB settings</param>
    /// <param name="logger">Logger instance</param>
    public RecurringTaskRepository(
        CosmosClient cosmosClient,
        CosmosDbSettings settings,
        ILogger<RecurringTaskRepository> logger)
    {
        _container = cosmosClient.GetContainer(settings.DatabaseName, settings.ContainerName);
        _logger = logger;
    }

    #region RecurringTaskConfig Operations

    /// <inheritdoc />
    public async Task<RecurringTaskConfigDocument> CreateConfigAsync(RecurringTaskConfigDocument config)
    {
        _logger.LogDebug("Creating recurring task config {ConfigId} for user {UserId}", config.Id, config.UserId);

        var response = await _container.CreateItemAsync(
            config,
            new PartitionKey(config.UserId));

        _logger.LogInformation("Created recurring task config {ConfigId} for user {UserId}", config.Id, config.UserId);
        return response.Resource;
    }

    /// <inheritdoc />
    public async Task<RecurringTaskConfigDocument?> GetConfigByIdAsync(string userId, string configId)
    {
        _logger.LogDebug("Getting recurring task config {ConfigId} for user {UserId}", configId, userId);

        try
        {
            var response = await _container.ReadItemAsync<RecurringTaskConfigDocument>(
                configId,
                new PartitionKey(userId));

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogDebug("Recurring task config {ConfigId} not found for user {UserId}", configId, userId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RecurringTaskConfigDocument>> GetAllConfigsAsync(string userId)
    {
        _logger.LogDebug("Getting all recurring task configs for user {UserId}", userId);

        var queryText = "SELECT * FROM c WHERE c.type = 'RecurringTaskConfig' AND c.userId = @userId";
        var queryDefinition = new QueryDefinition(queryText)
            .WithParameter("@userId", userId);

        var results = new List<RecurringTaskConfigDocument>();
        using var iterator = _container.GetItemQueryIterator<RecurringTaskConfigDocument>(
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

        _logger.LogDebug("Found {Count} recurring task configs for user {UserId}", results.Count, userId);
        return results;
    }

    /// <inheritdoc />
    public async Task<RecurringTaskConfigDocument> UpdateConfigAsync(RecurringTaskConfigDocument config)
    {
        _logger.LogDebug("Updating recurring task config {ConfigId} for user {UserId}", config.Id, config.UserId);

        var response = await _container.ReplaceItemAsync(
            config,
            config.Id,
            new PartitionKey(config.UserId));

        _logger.LogInformation("Updated recurring task config {ConfigId} for user {UserId}", config.Id, config.UserId);
        return response.Resource;
    }

    /// <inheritdoc />
    public async Task DeleteConfigWithOverridesAsync(string userId, string configId)
    {
        _logger.LogDebug("Deleting recurring task config {ConfigId} with overrides for user {UserId}", configId, userId);

        // First verify the config exists
        var config = await GetConfigByIdAsync(userId, configId);
        if (config == null)
        {
            throw new RecurringTaskConfigNotFoundException(configId);
        }

        // Query all state overrides for this config
        var overrideIds = await GetStateOverrideIdsForConfigAsync(userId, configId);

        // Create transactional batch with same partition key
        var batch = _container.CreateTransactionalBatch(new PartitionKey(userId));

        // Add delete for the config
        batch.DeleteItem(configId);

        // Add delete for each override
        foreach (var overrideId in overrideIds)
        {
            batch.DeleteItem(overrideId);
        }

        // Execute the batch
        using var batchResponse = await batch.ExecuteAsync();

        if (!batchResponse.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Transactional batch delete failed with status {StatusCode} for config {ConfigId}",
                batchResponse.StatusCode, configId);

            throw new InvalidOperationException(
                $"Failed to delete recurring task config {configId} with overrides. Status: {batchResponse.StatusCode}");
        }

        _logger.LogInformation(
            "Deleted recurring task config {ConfigId} and {OverrideCount} overrides for user {UserId}",
            configId, overrideIds.Count, userId);
    }

    /// <summary>
    /// Gets all state override IDs for a specific config (helper for cascade delete)
    /// </summary>
    private async Task<List<string>> GetStateOverrideIdsForConfigAsync(string userId, string configId)
    {
        var queryText = @"SELECT c.id FROM c
            WHERE c.type = 'RecurringTaskStateOverride'
            AND c.userId = @userId
            AND c.configId = @configId";

        var queryDefinition = new QueryDefinition(queryText)
            .WithParameter("@userId", userId)
            .WithParameter("@configId", configId);

        var overrideIds = new List<string>();
        using var iterator = _container.GetItemQueryIterator<dynamic>(
            queryDefinition,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(userId)
            });

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            foreach (var item in response)
            {
                overrideIds.Add((string)item.id);
            }
        }

        return overrideIds;
    }

    #endregion

    #region RecurringTaskStateOverride Operations

    /// <inheritdoc />
    public async Task<RecurringTaskStateOverrideDocument> UpsertStateOverrideAsync(RecurringTaskStateOverrideDocument stateOverride)
    {
        _logger.LogDebug(
            "Upserting state override {OverrideId} for user {UserId}",
            stateOverride.Id, stateOverride.UserId);

        var response = await _container.UpsertItemAsync(
            stateOverride,
            new PartitionKey(stateOverride.UserId));

        _logger.LogInformation(
            "Upserted state override {OverrideId} with state {State} for user {UserId}",
            stateOverride.Id, stateOverride.State, stateOverride.UserId);

        return response.Resource;
    }

    /// <inheritdoc />
    public async Task DeleteStateOverrideAsync(string userId, string overrideId)
    {
        _logger.LogDebug("Deleting state override {OverrideId} for user {UserId}", overrideId, userId);

        try
        {
            await _container.DeleteItemAsync<RecurringTaskStateOverrideDocument>(
                overrideId,
                new PartitionKey(userId));

            _logger.LogInformation("Deleted state override {OverrideId} for user {UserId}", overrideId, userId);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogDebug("State override {OverrideId} not found for deletion, treating as success", overrideId);
            // Idempotent delete - if already gone, that's fine
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RecurringTaskStateOverrideDocument>> GetStateOverridesForDateRangeAsync(
        string userId,
        DateTime from,
        DateTime to)
    {
        _logger.LogDebug(
            "Getting state overrides for user {UserId} from {From} to {To}",
            userId, from, to);

        var queryText = @"SELECT * FROM c
            WHERE c.type = 'RecurringTaskStateOverride'
            AND c.userId = @userId
            AND c.recurrenceDateAndTime >= @from
            AND c.recurrenceDateAndTime <= @to";

        var queryDefinition = new QueryDefinition(queryText)
            .WithParameter("@userId", userId)
            .WithParameter("@from", from)
            .WithParameter("@to", to);

        var results = new List<RecurringTaskStateOverrideDocument>();
        using var iterator = _container.GetItemQueryIterator<RecurringTaskStateOverrideDocument>(
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

        _logger.LogDebug("Found {Count} state overrides for user {UserId} in date range", results.Count, userId);
        return results;
    }

    #endregion
}
