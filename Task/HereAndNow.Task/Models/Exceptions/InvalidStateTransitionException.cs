namespace HereAndNowService.Models.Exceptions;

/// <summary>
/// Exception thrown when an invalid state transition is attempted on a task.
/// For example, attempting to update a Deleted task.
/// </summary>
public class InvalidStateTransitionException : Exception
{
    /// <summary>
    /// The ID of the task on which the invalid transition was attempted
    /// </summary>
    public string TaskId { get; }

    /// <summary>
    /// The current state of the task
    /// </summary>
    public string CurrentState { get; }

    /// <summary>
    /// The action that was attempted
    /// </summary>
    public string AttemptedAction { get; }

    /// <summary>
    /// Creates a new InvalidStateTransitionException
    /// </summary>
    /// <param name="taskId">The ID of the task</param>
    /// <param name="currentState">The current state of the task</param>
    /// <param name="attemptedAction">The action that was attempted</param>
    /// <param name="message">Optional custom message</param>
    public InvalidStateTransitionException(
        string taskId,
        string currentState,
        string attemptedAction,
        string? message = null)
        : base(message ?? $"Cannot perform '{attemptedAction}' on task {taskId} in state '{currentState}'")
    {
        TaskId = taskId;
        CurrentState = currentState;
        AttemptedAction = attemptedAction;
    }

    /// <summary>
    /// Creates a new InvalidStateTransitionException with inner exception
    /// </summary>
    public InvalidStateTransitionException(
        string taskId,
        string currentState,
        string attemptedAction,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        TaskId = taskId;
        CurrentState = currentState;
        AttemptedAction = attemptedAction;
    }
}
