namespace HereAndNowService.Models;

/// <summary>
/// Constants for task state values. Using string constants (not enum) to match
/// exact storage format in Cosmos DB, API, and frontend without mapping.
/// </summary>
public static class TaskState
{
    /// <summary>
    /// Task is queued and ready to be worked on
    /// </summary>
    public const string OnDeck = "OnDeck";

    /// <summary>
    /// Task is currently being worked on
    /// </summary>
    public const string InProgress = "InProgress";

    /// <summary>
    /// Task has been completed
    /// </summary>
    public const string Completed = "Completed";

    /// <summary>
    /// Task has been deleted (soft delete)
    /// </summary>
    public const string Deleted = "Deleted";

    /// <summary>
    /// Recurring task instance is scheduled but not yet active (recurring tasks only)
    /// </summary>
    public const string Scheduled = "Scheduled";

    /// <summary>
    /// Recurring task instance was skipped (recurring tasks only)
    /// </summary>
    public const string Skipped = "Skipped";

    /// <summary>
    /// All valid task states (regular tasks)
    /// </summary>
    public static readonly string[] AllStates = { OnDeck, InProgress, Completed, Deleted };

    /// <summary>
    /// All valid states for recurring task instances
    /// </summary>
    public static readonly string[] RecurringTaskStates = { Scheduled, OnDeck, InProgress, Completed, Skipped };

    /// <summary>
    /// Validates if the given state is a valid task state (regular tasks)
    /// </summary>
    public static bool IsValid(string? state) =>
        state is OnDeck or InProgress or Completed or Deleted;

    /// <summary>
    /// Validates if the given state is a valid recurring task state
    /// </summary>
    public static bool IsValidRecurringTaskState(string? state) =>
        state is Scheduled or OnDeck or InProgress or Completed or Skipped;
}
