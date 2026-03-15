using HereAndNowService.Models;

namespace HereAndNowService.Services;

/// <summary>
/// Service interface for computing recurring task instances from config and state overrides.
/// Story 9.2 scope: computation and read orchestration only.
/// Config CRUD → Story 9.3. State commands → Story 9.4. Query endpoints → Story 9.5.
/// </summary>
public interface IRecurringTaskService
{
    /// <summary>
    /// Fetches all configs and in-range state overrides from the repository,
    /// then computes instances in memory using <see cref="ComputeInstances"/>.
    /// Enforces a 365-day date range cap (NFR43).
    /// Makes exactly 2 database queries (NFR44).
    /// </summary>
    /// <param name="userId">The user ID (partition key).</param>
    /// <param name="from">Start of the date range (inclusive, must be UTC).</param>
    /// <param name="to">End of the date range (inclusive, must be UTC).</param>
    /// <returns>Computed instances for all configs within the date range.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="from"/> or <paramref name="to"/> are not UTC,
    /// or if the date range exceeds 365 days.
    /// </exception>
    Task<IReadOnlyList<RecurringTaskInstance>> GetComputedInstancesAsync(
        string userId, DateTime from, DateTime to);

    /// <summary>
    /// Pure function — no side effects, no I/O. Testable in isolation.
    /// Generates occurrences for each config using its RRULE, then resolves each
    /// occurrence's state via the one-active-at-a-time rule:
    /// <list type="bullet">
    ///   <item>future → <see cref="TaskState.Scheduled"/></item>
    ///   <item>most-recent-past with no non-terminal override → <see cref="TaskState.OnDeck"/></item>
    ///   <item>older-past with no non-terminal override → <see cref="TaskState.Skipped"/></item>
    ///   <item>terminal override (Completed/Skipped) → returned as-is</item>
    ///   <item>InProgress override — subject to one-active-at-a-time; superseded → <see cref="TaskState.Skipped"/></item>
    /// </list>
    /// </summary>
    /// <param name="configs">All recurring task configs for the user.</param>
    /// <param name="overrides">State overrides within the date range.</param>
    /// <param name="from">Start of the date range (inclusive).</param>
    /// <param name="to">End of the date range (inclusive).</param>
    /// <param name="utcNow">The reference "now" used for past/future classification. Must be UTC.</param>
    /// <returns>Computed instances with resolved states.</returns>
    IReadOnlyList<RecurringTaskInstance> ComputeInstances(
        IReadOnlyList<RecurringTaskConfigDocument> configs,
        IReadOnlyList<RecurringTaskStateOverrideDocument> overrides,
        DateTime from,
        DateTime to,
        DateTime utcNow);

    /// <summary>
    /// Creates a new recurring task configuration after validating the RRULE.
    /// </summary>
    /// <param name="userId">The user ID (partition key).</param>
    /// <param name="id">Client-generated config ID.</param>
    /// <param name="text">Display text for the recurring task.</param>
    /// <param name="rrule">RRULE string without prefix — validated for supported frequencies.</param>
    /// <param name="startDateAndTime">UTC start date/time for the recurrence pattern.</param>
    /// <returns>The created config document.</returns>
    /// <exception cref="HereAndNowService.Models.Exceptions.InvalidRecurrenceRuleException">
    /// Thrown if the RRULE is malformed or uses an unsupported frequency (Secondly, Minutely).
    /// </exception>
    Task<RecurringTaskConfigDocument> CreateConfigAsync(
        string userId, string id, string text, string rrule, DateTime startDateAndTime);

    /// <summary>
    /// Updates an existing recurring task configuration after validating the RRULE.
    /// </summary>
    /// <param name="userId">The user ID (partition key).</param>
    /// <param name="id">The config ID to update.</param>
    /// <param name="text">Updated display text.</param>
    /// <param name="rrule">Updated RRULE string — validated for supported frequencies.</param>
    /// <param name="startDateAndTime">Updated UTC start date/time.</param>
    /// <returns>The updated config document.</returns>
    /// <exception cref="HereAndNowService.Models.Exceptions.InvalidRecurrenceRuleException">
    /// Thrown if the RRULE is malformed or uses an unsupported frequency.
    /// </exception>
    /// <exception cref="HereAndNowService.Models.Exceptions.RecurringTaskConfigNotFoundException">
    /// Thrown if no config with the given ID exists for the user.
    /// </exception>
    Task<RecurringTaskConfigDocument> UpdateConfigAsync(
        string userId, string id, string text, string rrule, DateTime startDateAndTime);

    /// <summary>
    /// Deletes a recurring task configuration and all its state overrides atomically.
    /// </summary>
    /// <param name="userId">The user ID (partition key).</param>
    /// <param name="id">The config ID to delete.</param>
    /// <exception cref="HereAndNowService.Models.Exceptions.RecurringTaskConfigNotFoundException">
    /// Thrown if no config with the given ID exists for the user.
    /// </exception>
    Task DeleteConfigAsync(string userId, string id);
}
