namespace HereAndNowService.Models.Exceptions;

/// <summary>
/// Exception thrown when a requested recurring task configuration is not found
/// </summary>
public class RecurringTaskConfigNotFoundException : Exception
{
    /// <summary>
    /// The ID of the recurring task config that was not found
    /// </summary>
    public string ConfigId { get; }

    /// <summary>
    /// Creates a new RecurringTaskConfigNotFoundException
    /// </summary>
    /// <param name="configId">The ID of the config that was not found</param>
    public RecurringTaskConfigNotFoundException(string configId)
        : base($"Recurring task config with ID {configId} not found")
    {
        ConfigId = configId;
    }

    /// <summary>
    /// Creates a new RecurringTaskConfigNotFoundException with inner exception
    /// </summary>
    public RecurringTaskConfigNotFoundException(string configId, Exception innerException)
        : base($"Recurring task config with ID {configId} not found", innerException)
    {
        ConfigId = configId;
    }
}
