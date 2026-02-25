using HereAndNowService.Models;

namespace HereAndNowService.Repositories;

/// <summary>
/// Repository interface for RecurringTaskConfig and RecurringTaskStateOverride document persistence operations.
/// All methods require userId as first parameter to enforce partition key usage.
/// </summary>
public interface IRecurringTaskRepository
{
    #region RecurringTaskConfig Operations (AC#3)

    /// <summary>
    /// Creates a new recurring task configuration document in Cosmos DB
    /// </summary>
    /// <param name="config">The configuration document to create</param>
    /// <returns>The created configuration document</returns>
    Task<RecurringTaskConfigDocument> CreateConfigAsync(RecurringTaskConfigDocument config);

    /// <summary>
    /// Gets a specific recurring task configuration by ID within user partition
    /// </summary>
    /// <param name="userId">The user ID (partition key)</param>
    /// <param name="configId">The configuration ID</param>
    /// <returns>The configuration document or null if not found</returns>
    Task<RecurringTaskConfigDocument?> GetConfigByIdAsync(string userId, string configId);

    /// <summary>
    /// Gets all recurring task configurations for a specific user
    /// </summary>
    /// <param name="userId">The user ID (partition key)</param>
    /// <returns>List of configurations belonging to the user</returns>
    Task<IEnumerable<RecurringTaskConfigDocument>> GetAllConfigsAsync(string userId);

    /// <summary>
    /// Updates an existing recurring task configuration
    /// </summary>
    /// <param name="config">The configuration document with updated values</param>
    /// <returns>The updated configuration document</returns>
    Task<RecurringTaskConfigDocument> UpdateConfigAsync(RecurringTaskConfigDocument config);

    /// <summary>
    /// Atomically deletes a recurring task configuration and ALL its associated state overrides
    /// using a Cosmos DB transactional batch. This ensures cascade delete is atomic.
    /// </summary>
    /// <param name="userId">The user ID (partition key)</param>
    /// <param name="configId">The configuration ID to delete</param>
    /// <exception cref="Models.Exceptions.RecurringTaskConfigNotFoundException">If the config is not found</exception>
    Task DeleteConfigWithOverridesAsync(string userId, string configId);

    #endregion

    #region RecurringTaskStateOverride Operations (AC#4)

    /// <summary>
    /// Creates or updates a state override document (upsert semantics).
    /// If a document with the same ID exists, it will be replaced.
    /// </summary>
    /// <param name="stateOverride">The state override document to upsert</param>
    /// <returns>The upserted state override document</returns>
    Task<RecurringTaskStateOverrideDocument> UpsertStateOverrideAsync(RecurringTaskStateOverrideDocument stateOverride);

    /// <summary>
    /// Deletes a specific state override document
    /// </summary>
    /// <param name="userId">The user ID (partition key)</param>
    /// <param name="overrideId">The state override ID to delete</param>
    Task DeleteStateOverrideAsync(string userId, string overrideId);

    /// <summary>
    /// Gets all state overrides for a user within a date range.
    /// Useful for retrieving overrides for a visible time window.
    /// </summary>
    /// <param name="userId">The user ID (partition key)</param>
    /// <param name="from">Start of date range (inclusive, UTC)</param>
    /// <param name="to">End of date range (inclusive, UTC)</param>
    /// <returns>List of state overrides within the date range</returns>
    Task<IEnumerable<RecurringTaskStateOverrideDocument>> GetStateOverridesForDateRangeAsync(
        string userId,
        DateTime from,
        DateTime to);

    #endregion
}
