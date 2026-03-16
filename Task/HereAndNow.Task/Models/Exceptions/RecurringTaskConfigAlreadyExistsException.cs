namespace HereAndNowService.Models.Exceptions;

/// <summary>
/// Exception thrown when attempting to create a recurring task config with an ID that already exists.
/// This typically occurs when a client retries a CreateRecurringTaskConfig command and the previous
/// attempt succeeded, or when there's an ID collision.
/// </summary>
public class RecurringTaskConfigAlreadyExistsException : Exception
{
    /// <summary>
    /// The ID of the config that already exists
    /// </summary>
    public string ConfigId { get; }

    /// <summary>
    /// Creates a new RecurringTaskConfigAlreadyExistsException
    /// </summary>
    /// <param name="configId">The ID of the config that already exists</param>
    public RecurringTaskConfigAlreadyExistsException(string configId)
        : base($"Recurring task config with ID {configId} already exists")
    {
        ConfigId = configId;
    }

    /// <summary>
    /// Creates a new RecurringTaskConfigAlreadyExistsException with inner exception
    /// </summary>
    public RecurringTaskConfigAlreadyExistsException(string configId, Exception innerException)
        : base($"Recurring task config with ID {configId} already exists", innerException)
    {
        ConfigId = configId;
    }
}
