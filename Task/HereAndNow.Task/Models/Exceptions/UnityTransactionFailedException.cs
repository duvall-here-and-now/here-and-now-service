namespace HereAndNowService.Models.Exceptions;

/// <summary>
/// Exception thrown when a transactional batch operation fails during Task-Reminder Unity.
/// Unity operations atomically update both Task and TaskReminder documents.
/// </summary>
public class UnityTransactionFailedException : Exception
{
    /// <summary>
    /// The ID of the task involved in the failed Unity operation
    /// </summary>
    public string TaskId { get; }

    /// <summary>
    /// Creates a new UnityTransactionFailedException
    /// </summary>
    /// <param name="message">Description of the failure</param>
    /// <param name="taskId">The ID of the task involved in the failed operation</param>
    public UnityTransactionFailedException(string message, string taskId)
        : base(message)
    {
        TaskId = taskId;
    }

    /// <summary>
    /// Creates a new UnityTransactionFailedException with inner exception
    /// </summary>
    /// <param name="message">Description of the failure</param>
    /// <param name="taskId">The ID of the task involved in the failed operation</param>
    /// <param name="innerException">The underlying exception</param>
    public UnityTransactionFailedException(string message, string taskId, Exception innerException)
        : base(message, innerException)
    {
        TaskId = taskId;
    }
}
