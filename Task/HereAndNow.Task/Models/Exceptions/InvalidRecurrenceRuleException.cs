namespace HereAndNowService.Models.Exceptions;

/// <summary>
/// Thrown when a recurrence rule (RRULE) fails validation — e.g., unsupported frequency.
/// </summary>
public class InvalidRecurrenceRuleException : Exception
{
    /// <summary>
    /// The RRULE string that failed validation.
    /// </summary>
    public string RecurrenceRule { get; }

    public InvalidRecurrenceRuleException(string recurrenceRule, string message)
        : base(message)
    {
        RecurrenceRule = recurrenceRule;
    }

    public InvalidRecurrenceRuleException(string recurrenceRule, string message, Exception innerException)
        : base(message, innerException)
    {
        RecurrenceRule = recurrenceRule;
    }
}
