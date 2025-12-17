using System.Net;
using HereAndNowService.Exceptions;
using HereAndNowService.Models;
using HereAndNowService.Persistence;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace HereAndNowService.Services;

/// <summary>
/// Cosmos DB implementation of the reminder instance service.
/// Uses the userId as the partition key for efficient queries.
/// </summary>
public class CosmosReminderInstanceService : IReminderInstanceService
{
    private readonly Container _container;
    private readonly ILogger<CosmosReminderInstanceService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosReminderInstanceService"/> class.
    /// </summary>
    /// <param name="cosmosClient">The Cosmos DB client.</param>
    /// <param name="databaseName">The database name.</param>
    /// <param name="containerName">The container name.</param>
    /// <param name="logger">The logger instance.</param>
    public CosmosReminderInstanceService(
        CosmosClient cosmosClient,
        string databaseName,
        string containerName,
        ILogger<CosmosReminderInstanceService> logger)
    {
        _container = cosmosClient.GetContainer(databaseName, containerName);
        _logger = logger;
    }

    /// <inheritdoc/>
    public IEnumerable<ReminderInstance> GetAll(string userId)
    {
        _logger.LogInformation("Querying all reminders for user: {UserId}", userId);

        try
        {
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.userId = @userId AND c.isDeleted = false")
                .WithParameter("@userId", userId);

            var results = new List<ReminderInstance>();

            using var iterator = _container.GetItemQueryIterator<ReminderDocument>(
                query,
                requestOptions: new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(userId)
                });

            while (iterator.HasMoreResults)
            {
                var response = iterator.ReadNextAsync().GetAwaiter().GetResult();
                foreach (var document in response)
                {
                    results.Add(ReminderDocumentMapper.ToDomain(document));
                }
            }

            _logger.LogInformation("Retrieved {Count} reminders for user: {UserId}", results.Count, userId);
            return results;
        }
        catch (CosmosException ex) when (IsServiceUnavailable(ex))
        {
            _logger.LogError(ex, "Cosmos DB service unavailable while querying reminders for user: {UserId}", userId);
            throw new ServiceUnavailableException("Cosmos DB", "Database service is temporarily unavailable", ex);
        }
    }

    /// <summary>
    /// Determines if the CosmosException indicates a service unavailability.
    /// </summary>
    private static bool IsServiceUnavailable(CosmosException ex)
    {
        return ex.StatusCode == HttpStatusCode.ServiceUnavailable ||
               ex.StatusCode == HttpStatusCode.RequestTimeout ||
               ex.StatusCode == HttpStatusCode.TooManyRequests ||
               ex.StatusCode == HttpStatusCode.GatewayTimeout;
    }

    /// <inheritdoc/>
    public ReminderInstance? GetById(Guid id, string userId)
    {
        _logger.LogInformation("Getting reminder {ReminderId} for user: {UserId}", id, userId);

        try
        {
            var response = _container.ReadItemAsync<ReminderDocument>(
                id.ToString(),
                new PartitionKey(userId)).GetAwaiter().GetResult();

            _logger.LogInformation("Found reminder {ReminderId} for user: {UserId}", id, userId);
            return ReminderDocumentMapper.ToDomain(response.Resource);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Reminder {ReminderId} not found for user: {UserId}", id, userId);
            return null;
        }
        catch (CosmosException ex) when (IsServiceUnavailable(ex))
        {
            _logger.LogError(ex, "Cosmos DB service unavailable while getting reminder {ReminderId}", id);
            throw new ServiceUnavailableException("Cosmos DB", "Database service is temporarily unavailable", ex);
        }
    }

    /// <inheritdoc/>
    public ReminderInstance Create(ReminderInstance reminder)
    {
        _logger.LogInformation("Creating reminder for user: {UserId}", reminder.UserId);

        try
        {
            reminder.Id = Guid.NewGuid();
            var document = ReminderDocumentMapper.ToDocument(reminder);

            var response = _container.CreateItemAsync(
                document,
                new PartitionKey(reminder.UserId)).GetAwaiter().GetResult();

            _logger.LogInformation("Created reminder {ReminderId} for user: {UserId}. RU charge: {RUCharge}",
                reminder.Id, reminder.UserId, response.RequestCharge);

            return reminder;
        }
        catch (CosmosException ex) when (IsServiceUnavailable(ex))
        {
            _logger.LogError(ex, "Cosmos DB service unavailable while creating reminder for user: {UserId}", reminder.UserId);
            throw new ServiceUnavailableException("Cosmos DB", "Database service is temporarily unavailable", ex);
        }
    }

    /// <inheritdoc/>
    public ReminderInstance? Update(Guid id, ReminderInstance reminder)
    {
        _logger.LogInformation("Updating reminder {ReminderId} for user: {UserId}", id, reminder.UserId);

        try
        {
            // First verify the document exists and belongs to the user
            var existingResponse = _container.ReadItemAsync<ReminderDocument>(
                id.ToString(),
                new PartitionKey(reminder.UserId)).GetAwaiter().GetResult();

            // Update with the new values
            reminder.Id = id;
            var document = ReminderDocumentMapper.ToDocument(reminder);

            var response = _container.UpsertItemAsync(
                document,
                new PartitionKey(reminder.UserId)).GetAwaiter().GetResult();

            _logger.LogInformation("Updated reminder {ReminderId}. RU charge: {RUCharge}",
                id, response.RequestCharge);

            return reminder;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Cannot update - reminder {ReminderId} not found for user: {UserId}", id, reminder.UserId);
            return null;
        }
        catch (CosmosException ex) when (IsServiceUnavailable(ex))
        {
            _logger.LogError(ex, "Cosmos DB service unavailable while updating reminder {ReminderId}", id);
            throw new ServiceUnavailableException("Cosmos DB", "Database service is temporarily unavailable", ex);
        }
    }

    /// <inheritdoc/>
    public bool Delete(Guid id, string userId)
    {
        _logger.LogInformation("Soft-deleting reminder {ReminderId} for user: {UserId}", id, userId);

        try
        {
            // Read the existing document
            var existingResponse = _container.ReadItemAsync<ReminderDocument>(
                id.ToString(),
                new PartitionKey(userId)).GetAwaiter().GetResult();

            var document = existingResponse.Resource;
            document.IsDeleted = true;

            // Update with soft-delete flag
            var response = _container.UpsertItemAsync(
                document,
                new PartitionKey(userId)).GetAwaiter().GetResult();

            _logger.LogInformation("Soft-deleted reminder {ReminderId}. RU charge: {RUCharge}",
                id, response.RequestCharge);

            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Cannot delete - reminder {ReminderId} not found for user: {UserId}", id, userId);
            return false;
        }
        catch (CosmosException ex) when (IsServiceUnavailable(ex))
        {
            _logger.LogError(ex, "Cosmos DB service unavailable while deleting reminder {ReminderId}", id);
            throw new ServiceUnavailableException("Cosmos DB", "Database service is temporarily unavailable", ex);
        }
    }
}
