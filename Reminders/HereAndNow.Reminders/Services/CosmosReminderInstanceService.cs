using System.Net;
using HereAndNowService.Exceptions;
using HereAndNowService.Models;
using HereAndNowService.Persistence;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace HereAndNowService.Services;

/// <summary>
/// Azure Cosmos DB implementation of the reminder instance service.
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

    /// <summary>
    /// Gets all reminder instances for a specific user.
    /// </summary>
    /// <param name="userId">The user identifier to filter by.</param>
    /// <returns>A collection of reminder instances belonging to the user.</returns>
    public IEnumerable<ReminderInstance> GetAll(string userId)
    {
        _logger.LogInformation("Retrieving all reminder instances for user: {UserId}", userId);

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
                results.AddRange(response.Select(doc => doc.ToDomain()));
            }

            _logger.LogInformation("Retrieved {Count} reminder instances for user: {UserId}", results.Count, userId);
            return results;
        }
        catch (CosmosException ex) when (IsServiceUnavailable(ex))
        {
            _logger.LogError(ex, "Cosmos DB service unavailable while retrieving reminders for user: {UserId}", userId);
            throw new ServiceUnavailableException("CosmosDB", "The reminder service is temporarily unavailable. Please try again later.", ex);
        }
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

        try
        {
            var response = _container.ReadItemAsync<ReminderDocument>(
                id.ToString(),
                new PartitionKey(userId))
                .GetAwaiter().GetResult();

            var document = response.Resource;

            if (document.IsDeleted)
            {
                _logger.LogWarning("Reminder instance with ID: {ReminderId} is deleted", id);
                return null;
            }

            _logger.LogInformation("Successfully retrieved reminder instance with ID: {ReminderId}", id);
            return document.ToDomain();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Reminder instance with ID: {ReminderId} not found for user: {UserId}", id, userId);
            return null;
        }
        catch (CosmosException ex) when (IsServiceUnavailable(ex))
        {
            _logger.LogError(ex, "Cosmos DB service unavailable while retrieving reminder {ReminderId} for user: {UserId}", id, userId);
            throw new ServiceUnavailableException("CosmosDB", "The reminder service is temporarily unavailable. Please try again later.", ex);
        }
    }

    /// <summary>
    /// Creates a new reminder instance with client-provided ID.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if a reminder with the same ID already exists for the user.</exception>
    public ReminderInstance Create(
        Guid id,
        string userId,
        string text,
        DateTime scheduledDateAndTime,
        bool shouldPlaySound,
        bool shouldDoVibration)
    {
        _logger.LogInformation("Creating new reminder instance with ID: {ReminderId} for user: {UserId}", id, userId);

        try
        {
            var reminder = new ReminderInstance
            {
                Id = id,
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

            var document = ReminderDocument.FromDomain(reminder);

            var response = _container.CreateItemAsync(
                document,
                new PartitionKey(document.UserId))
                .GetAwaiter().GetResult();

            _logger.LogInformation("Successfully created reminder instance with ID: {ReminderId}", reminder.Id);
            return response.Resource.ToDomain();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            _logger.LogWarning("Cannot create reminder - a reminder with ID: {ReminderId} already exists for user: {UserId}", id, userId);
            throw new InvalidOperationException($"A reminder with ID {id} already exists.");
        }
        catch (CosmosException ex) when (IsServiceUnavailable(ex))
        {
            _logger.LogError(ex, "Cosmos DB service unavailable while creating reminder for user: {UserId}", userId);
            throw new ServiceUnavailableException("CosmosDB", "The reminder service is temporarily unavailable. Please try again later.", ex);
        }
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
        _logger.LogInformation("Updating reminder instance with ID: {ReminderId} for user: {UserId}", id, userId);

        try
        {
            // First, verify the document exists and belongs to the user
            var existingResponse = _container.ReadItemAsync<ReminderDocument>(
                id.ToString(),
                new PartitionKey(userId))
                .GetAwaiter().GetResult();

            var existingDoc = existingResponse.Resource;

            if (existingDoc.IsDeleted)
            {
                _logger.LogWarning("Cannot update - reminder instance with ID: {ReminderId} is deleted", id);
                return null;
            }

            // Validate state before allowing scheduledDateAndTime update
            if (scheduledDateAndTime.HasValue)
            {
                // Compute current state: Completed > Active > Scheduled
                if (existingDoc.IsCompleted)
                {
                    _logger.LogWarning("Cannot update scheduled time - reminder instance with ID: {ReminderId} is in 'Completed' state", id);
                    throw new InvalidOperationException("Cannot update scheduled time. Reminder is in 'Completed' state.");
                }

                if (DateTime.UtcNow >= existingDoc.ScheduledDateAndTime)
                {
                    _logger.LogWarning("Cannot update scheduled time - reminder instance with ID: {ReminderId} is in 'Active' state", id);
                    throw new InvalidOperationException("Cannot update scheduled time. Reminder is in 'Active' state.");
                }
            }

            // Apply partial updates
            if (text != null) existingDoc.Text = text;
            if (scheduledDateAndTime.HasValue) existingDoc.ScheduledDateAndTime = scheduledDateAndTime.Value;
            if (shouldPlaySound.HasValue) existingDoc.ShouldPlaySound = shouldPlaySound.Value;
            if (shouldDoVibration.HasValue) existingDoc.ShouldDoVibration = shouldDoVibration.Value;

            var response = _container.UpsertItemAsync(
                existingDoc,
                new PartitionKey(existingDoc.UserId))
                .GetAwaiter().GetResult();

            _logger.LogInformation("Successfully updated reminder instance with ID: {ReminderId}", id);
            return response.Resource.ToDomain();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Cannot update - reminder instance with ID: {ReminderId} not found for user: {UserId}", id, userId);
            return null;
        }
        catch (CosmosException ex) when (IsServiceUnavailable(ex))
        {
            _logger.LogError(ex, "Cosmos DB service unavailable while updating reminder {ReminderId} for user: {UserId}", id, userId);
            throw new ServiceUnavailableException("CosmosDB", "The reminder service is temporarily unavailable. Please try again later.", ex);
        }
    }

    /// <summary>
    /// Marks a reminder as completed and sets the completion timestamp.
    /// </summary>
    public ReminderInstance? Complete(Guid id, string userId)
    {
        _logger.LogInformation("Attempting to complete reminder instance with ID: {ReminderId} for user: {UserId}", id, userId);

        try
        {
            var existingResponse = _container.ReadItemAsync<ReminderDocument>(
                id.ToString(),
                new PartitionKey(userId))
                .GetAwaiter().GetResult();

            var document = existingResponse.Resource;

            if (document.IsDeleted)
            {
                _logger.LogWarning("Cannot complete - reminder instance with ID: {ReminderId} is deleted", id);
                throw new InvalidOperationException("Cannot complete a deleted reminder.");
            }

            // Idempotent: if already completed, return current state
            if (document.IsCompleted)
            {
                _logger.LogInformation("Reminder instance with ID: {ReminderId} is already completed", id);
                return document.ToDomain();
            }

            document.IsCompleted = true;
            document.CompletedDateAndTime = DateTime.UtcNow;

            var response = _container.UpsertItemAsync(
                document,
                new PartitionKey(userId))
                .GetAwaiter().GetResult();

            _logger.LogInformation("Successfully completed reminder instance with ID: {ReminderId}", id);
            return response.Resource.ToDomain();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Cannot complete - reminder instance with ID: {ReminderId} not found for user: {UserId}", id, userId);
            return null;
        }
        catch (CosmosException ex) when (IsServiceUnavailable(ex))
        {
            _logger.LogError(ex, "Cosmos DB service unavailable while completing reminder {ReminderId} for user: {UserId}", id, userId);
            throw new ServiceUnavailableException("CosmosDB", "The reminder service is temporarily unavailable. Please try again later.", ex);
        }
    }

    /// <summary>
    /// Soft-deletes a reminder instance by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the reminder to delete.</param>
    /// <param name="userId">The user identifier for partition key lookup.</param>
    /// <returns>True if the reminder was deleted; otherwise, false.</returns>
    public bool Delete(Guid id, string userId)
    {
        _logger.LogInformation("Attempting to soft-delete reminder instance with ID: {ReminderId} for user: {UserId}", id, userId);

        try
        {
            var existingResponse = _container.ReadItemAsync<ReminderDocument>(
                id.ToString(),
                new PartitionKey(userId))
                .GetAwaiter().GetResult();

            var document = existingResponse.Resource;

            // Idempotent: if already deleted, return success
            if (document.IsDeleted)
            {
                _logger.LogInformation("Reminder instance with ID: {ReminderId} is already deleted", id);
                return true;
            }

            document.IsDeleted = true;
            document.DeletedDateAndTime = DateTime.UtcNow;

            _container.UpsertItemAsync(
                document,
                new PartitionKey(userId))
                .GetAwaiter().GetResult();

            _logger.LogInformation("Successfully soft-deleted reminder instance with ID: {ReminderId}", id);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Cannot delete - reminder instance with ID: {ReminderId} not found for user: {UserId}", id, userId);
            return false;
        }
        catch (CosmosException ex) when (IsServiceUnavailable(ex))
        {
            _logger.LogError(ex, "Cosmos DB service unavailable while deleting reminder {ReminderId} for user: {UserId}", id, userId);
            throw new ServiceUnavailableException("CosmosDB", "The reminder service is temporarily unavailable. Please try again later.", ex);
        }
    }

    /// <summary>
    /// Determines if a Cosmos exception indicates service unavailability.
    /// </summary>
    private static bool IsServiceUnavailable(CosmosException ex)
    {
        return ex.StatusCode == HttpStatusCode.ServiceUnavailable
            || ex.StatusCode == HttpStatusCode.RequestTimeout
            || ex.StatusCode == HttpStatusCode.GatewayTimeout
            || ex.StatusCode == HttpStatusCode.InternalServerError;
    }
}
