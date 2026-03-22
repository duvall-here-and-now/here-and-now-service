using System.Net;
using HereAndNowService.Models;
using HereAndNowService.Models.Exceptions;
using HereAndNowService.Repositories;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace HereAndNowService.Services;

/// <summary>
/// Service for computing recurring task instances using RRULE-based occurrence generation
/// and in-memory state resolution. Implements the two-query pattern (NFR44): one query
/// for all configs, one query for state overrides in range; all computation is in-memory.
/// </summary>
public class RecurringTaskService : IRecurringTaskService
{
    private readonly IRecurringTaskRepository _repository;
    private readonly ILogger<RecurringTaskService> _logger;

    /// <summary>
    /// Creates a new <see cref="RecurringTaskService"/> instance.
    /// </summary>
    /// <param name="repository">The recurring task repository for DB reads.</param>
    /// <param name="logger">Logger instance.</param>
    public RecurringTaskService(
        IRecurringTaskRepository repository,
        ILogger<RecurringTaskService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RecurringTaskInstance>> GetComputedInstancesAsync(
        string userId, DateTime from, DateTime to)
    {
        if (from.Kind != DateTimeKind.Utc || to.Kind != DateTimeKind.Utc)
            throw new ArgumentException("from and to must be UTC DateTimes");
        if ((to - from).TotalDays > 365)
            throw new ArgumentException("Date range cannot exceed 365 days (NFR43)");

        // 2-query pattern (NFR44) — all computation in-memory after these two calls
        var configs = (await _repository.GetAllConfigsAsync(userId)).ToList();
        var overrides = (await _repository.GetStateOverridesForDateRangeAsync(userId, from, to)).ToList();

        return ComputeInstances(configs, overrides, from, to, DateTime.UtcNow);
    }

    /// <inheritdoc />
    public IReadOnlyList<RecurringTaskInstance> ComputeInstances(
        IReadOnlyList<RecurringTaskConfigDocument> configs,
        IReadOnlyList<RecurringTaskStateOverrideDocument> overrides,
        DateTime from,
        DateTime to,
        DateTime utcNow)
    {
        var results = new List<RecurringTaskInstance>();

        // Build lookup from stored overrides — key uses :O (round-trip) format
        // CRITICAL: Both sides of the lookup MUST use the same format (:O) for consistent matching
        var overrideLookup = overrides.ToDictionary(
            o => $"{o.ConfigId}_{o.RecurrenceDateAndTime:O}");

        foreach (var config in configs)
        {
            // Exclude occurrences before config's start date (FR75/AC2)
            var effectiveFrom = config.StartDateAndTime > from ? config.StartDateAndTime : from;
            var occurrences = GetOccurrences(config.Rrule, config.StartDateAndTime, effectiveFrom, to);

            RecurringTaskInstance? activeCandidate = null;
            var configInstances = new List<RecurringTaskInstance>();

            // Traverse newest-first to apply one-active-at-a-time (AC5, AC6)
            foreach (var occurrence in occurrences.OrderByDescending(o => o))
            {
                // Must match key format used in overrideLookup above
                var key = $"{config.Id}_{occurrence:O}";

                if (overrideLookup.TryGetValue(key, out var stateOverride))
                {
                    var storedState = stateOverride.State;

                    // Terminal states — always respected, never recalculated (AC7)
                    if (storedState == TaskState.Completed || storedState == TaskState.Skipped)
                    {
                        configInstances.Add(new RecurringTaskInstance(config, occurrence, storedState));
                    }
                    // InProgress is an active state — subject to one-active-at-a-time (AC6)
                    else if (storedState == TaskState.InProgress)
                    {
                        if (activeCandidate == null)
                        {
                            // Most recent active — InProgress override stands
                            activeCandidate = new RecurringTaskInstance(config, occurrence, TaskState.InProgress);
                            configInstances.Add(activeCandidate);
                        }
                        else
                        {
                            // Superseded by a more recent active instance (AC6) — becomes Skipped
                            configInstances.Add(new RecurringTaskInstance(config, occurrence, TaskState.Skipped));
                        }
                    }
                    else
                    {
                        // Defensive fallback: unexpected stored state (e.g. data migration artifact or
                        // a future state added without a corresponding branch here).
                        // Pass through as-is to prevent silent data loss. In normal operation this
                        // branch should never execute because only InProgress / Completed / Skipped
                        // are ever written to Cosmos DB.
                        _logger.LogWarning(
                            "RecurringTaskService: unexpected stored override state '{State}' for key '{Key}'. " +
                            "Passing through as-is.",
                            storedState, key);
                        configInstances.Add(new RecurringTaskInstance(config, occurrence, storedState));
                    }
                }
                else if (occurrence > utcNow)
                {
                    // Future occurrence, no override → Scheduled (AC3)
                    configInstances.Add(new RecurringTaskInstance(config, occurrence, TaskState.Scheduled));
                }
                else if (activeCandidate == null)
                {
                    // Most recent past, no override → OnDeck (AC4)
                    activeCandidate = new RecurringTaskInstance(config, occurrence, TaskState.OnDeck);
                    configInstances.Add(activeCandidate);
                }
                else
                {
                    // Older past, no override, already have an active candidate → Skipped (AC5)
                    configInstances.Add(new RecurringTaskInstance(config, occurrence, TaskState.Skipped));
                }
            }

            results.AddRange(configInstances);
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<RecurringTaskConfigDocument> CreateConfigAsync(
        string userId, string id, string text, string rrule, DateTime startDateAndTime)
    {
        if (startDateAndTime.Kind != DateTimeKind.Utc)
            throw new ArgumentException("startDateAndTime must be UTC", nameof(startDateAndTime));

        ValidateRrule(rrule);

        var config = new RecurringTaskConfigDocument
        {
            Id = id,
            UserId = userId,
            Text = text,
            Rrule = rrule,   // Store as-is, no RRULE: prefix, no re-serialization
            StartDateAndTime = startDateAndTime,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            return await _repository.CreateConfigAsync(config);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            throw new RecurringTaskConfigAlreadyExistsException(id, ex);
        }
    }

    /// <inheritdoc />
    public async Task<RecurringTaskConfigDocument> UpdateConfigAsync(
        string userId, string id, string text, string rrule, DateTime startDateAndTime)
    {
        if (startDateAndTime.Kind != DateTimeKind.Utc)
            throw new ArgumentException("startDateAndTime must be UTC", nameof(startDateAndTime));

        ValidateRrule(rrule);

        var existing = await _repository.GetConfigByIdAsync(userId, id);
        if (existing == null)
            throw new RecurringTaskConfigNotFoundException(id);

        existing.Text = text;
        existing.Rrule = rrule;
        existing.StartDateAndTime = startDateAndTime;
        // Do NOT update CreatedAt — it's the original creation timestamp
        // Orphaned state overrides are accepted for MVP when RRULE changes

        return await _repository.UpdateConfigAsync(existing);
    }

    /// <inheritdoc />
    public async Task DeleteConfigAsync(string userId, string id)
    {
        await _repository.DeleteConfigWithOverridesAsync(userId, id);
        // DeleteConfigWithOverridesAsync already throws RecurringTaskConfigNotFoundException
        // if the config doesn't exist, and handles chunked batch deletion for >99 overrides
    }

    /// <inheritdoc />
    public async Task StartRecurringTaskAsync(string userId, string configId, DateTime recurrenceDateAndTime)
    {
        recurrenceDateAndTime = EnsureUtc(recurrenceDateAndTime);
        var (instance, _) = await GetTargetInstanceAsync(userId, configId, recurrenceDateAndTime);

        if (instance.State != TaskState.OnDeck)
        {
            throw new InvalidStateTransitionException(
                instance.Id, instance.State, "StartRecurringTask",
                $"Cannot start recurring task instance in state '{instance.State}'. Only OnDeck instances can be started.");
        }

        var overrideDoc = CreateOverrideDocument(userId, configId, recurrenceDateAndTime, TaskState.InProgress);
        await _repository.UpsertStateOverrideAsync(overrideDoc);
    }

    /// <inheritdoc />
    public async Task RevertRecurringTaskToOnDeckAsync(string userId, string configId, DateTime recurrenceDateAndTime)
    {
        recurrenceDateAndTime = EnsureUtc(recurrenceDateAndTime);
        var (instance, _) = await GetTargetInstanceAsync(userId, configId, recurrenceDateAndTime);

        if (instance.State != TaskState.InProgress)
        {
            throw new InvalidStateTransitionException(
                instance.Id, instance.State, "RevertRecurringTaskToOnDeck",
                $"Cannot revert recurring task instance in state '{instance.State}'. Only InProgress instances can be reverted.");
        }

        var overrideId = RecurringTaskStateOverrideDocument.GenerateId(configId, recurrenceDateAndTime);
        await _repository.DeleteStateOverrideAsync(userId, overrideId);
    }

    /// <inheritdoc />
    public async Task CompleteRecurringTaskAsync(string userId, string configId, DateTime recurrenceDateAndTime)
    {
        recurrenceDateAndTime = EnsureUtc(recurrenceDateAndTime);
        var (instance, allInstances) = await GetTargetInstanceAsync(userId, configId, recurrenceDateAndTime);

        switch (instance.State)
        {
            case TaskState.OnDeck:
            case TaskState.InProgress:
                // Allowed — proceed
                break;
            case TaskState.Completed:
                // Idempotent — already completed, no-op
                return;
            case TaskState.Skipped:
                // Allowed only if no newer active instance
                ValidateNoNewerActiveInstance(instance, allInstances, "CompleteRecurringTask");
                break;
            default:
                throw new InvalidStateTransitionException(
                    instance.Id, instance.State, "CompleteRecurringTask",
                    $"Cannot complete recurring task instance in state '{instance.State}'.");
        }

        var overrideDoc = CreateOverrideDocument(userId, configId, recurrenceDateAndTime, TaskState.Completed);
        await _repository.UpsertStateOverrideAsync(overrideDoc);
    }

    /// <inheritdoc />
    public async Task SkipRecurringTaskAsync(string userId, string configId, DateTime recurrenceDateAndTime)
    {
        recurrenceDateAndTime = EnsureUtc(recurrenceDateAndTime);
        var (instance, allInstances) = await GetTargetInstanceAsync(userId, configId, recurrenceDateAndTime);

        switch (instance.State)
        {
            case TaskState.OnDeck:
            case TaskState.InProgress:
                // Allowed — proceed
                break;
            case TaskState.Skipped:
                // Idempotent — already skipped, no-op
                return;
            case TaskState.Completed:
                // Allowed only if no newer active instance
                ValidateNoNewerActiveInstance(instance, allInstances, "SkipRecurringTask");
                break;
            default:
                throw new InvalidStateTransitionException(
                    instance.Id, instance.State, "SkipRecurringTask",
                    $"Cannot skip recurring task instance in state '{instance.State}'.");
        }

        var overrideDoc = CreateOverrideDocument(userId, configId, recurrenceDateAndTime, TaskState.Skipped);
        await _repository.UpsertStateOverrideAsync(overrideDoc);
    }

    /// <summary>
    /// Fetches the target config and computes instances to find the specific instance's current state.
    /// Returns both the target instance and all instances for the config (needed for newer-active checks).
    /// </summary>
    private async Task<(RecurringTaskInstance instance, IReadOnlyList<RecurringTaskInstance> allInstances)>
        GetTargetInstanceAsync(string userId, string configId, DateTime recurrenceDateAndTime)
    {
        // Verify config exists
        var config = await _repository.GetConfigByIdAsync(userId, configId);
        if (config == null)
            throw new RecurringTaskConfigNotFoundException(configId);

        // Compute instances for a wide range around the target date to capture nearby instances
        // for the newer-active-instance check. Use ±365 days from the target date (max range).
        var from = recurrenceDateAndTime.AddDays(-365);
        var to = recurrenceDateAndTime.AddDays(365);

        var overrides = (await _repository.GetStateOverridesForDateRangeAsync(userId, from, to)).ToList();

        var allInstances = ComputeInstances(
            new[] { config },
            overrides,
            from,
            to,
            DateTime.UtcNow);

        // Find the specific target instance
        var targetInstance = allInstances
            .FirstOrDefault(i => i.RecurringTaskConfigId == configId &&
                                 i.RecurrenceDateAndTime == recurrenceDateAndTime);

        if (targetInstance == null)
        {
            throw new InvalidStateTransitionException(
                RecurringTaskStateOverrideDocument.GenerateId(configId, recurrenceDateAndTime),
                TaskState.Scheduled,
                "StateCommand",
                $"No recurring task instance found for config '{configId}' at {recurrenceDateAndTime:O}. " +
                "The recurrenceDateAndTime may not match any occurrence in the RRULE pattern.");
        }

        return (targetInstance, allInstances);
    }

    /// <summary>
    /// Validates that no newer instance (with a more recent recurrenceDateAndTime) is in OnDeck or InProgress state.
    /// This prevents modifying old completed/skipped instances while a newer one demands attention.
    /// </summary>
    private static void ValidateNoNewerActiveInstance(
        RecurringTaskInstance targetInstance,
        IReadOnlyList<RecurringTaskInstance> allInstances,
        string attemptedAction)
    {
        var hasNewerActive = allInstances.Any(i =>
            i.RecurringTaskConfigId == targetInstance.RecurringTaskConfigId &&
            i.RecurrenceDateAndTime > targetInstance.RecurrenceDateAndTime &&
            (i.State == TaskState.OnDeck || i.State == TaskState.InProgress));

        if (hasNewerActive)
        {
            throw new InvalidStateTransitionException(
                targetInstance.Id, targetInstance.State, attemptedAction,
                $"Cannot change state of instance at {targetInstance.RecurrenceDateAndTime:O} " +
                "because a more recent instance is currently active (OnDeck or InProgress).");
        }
    }

    /// <summary>
    /// Creates a state override document for upsert operations.
    /// </summary>
    private static RecurringTaskStateOverrideDocument CreateOverrideDocument(
        string userId, string configId, DateTime recurrenceDateAndTime, string state)
    {
        return new RecurringTaskStateOverrideDocument
        {
            Id = RecurringTaskStateOverrideDocument.GenerateId(configId, recurrenceDateAndTime),
            UserId = userId,
            ConfigId = configId,
            RecurrenceDateAndTime = recurrenceDateAndTime,
            State = state,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Ensures the DateTime has UTC kind. Specifies Utc if Unspecified.
    /// </summary>
    private static DateTime EnsureUtc(DateTime dateTime)
    {
        if (dateTime.Kind == DateTimeKind.Utc)
            return dateTime;
        if (dateTime.Kind == DateTimeKind.Unspecified)
            return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        throw new ArgumentException("recurrenceDateAndTime must be UTC", nameof(dateTime));
    }

    /// <summary>
    /// Validates an RRULE string: parses it via Ical.Net and rejects SECONDLY/MINUTELY frequencies.
    /// </summary>
    private static void ValidateRrule(string rrule)
    {
        RecurrencePattern pattern;
        try
        {
            pattern = new RecurrencePattern(rrule);
        }
        catch (Exception ex)
        {
            throw new InvalidRecurrenceRuleException(rrule,
                $"Invalid recurrence rule format: {rrule}", ex);
        }

        if (pattern.Frequency == FrequencyType.Secondly ||
            pattern.Frequency == FrequencyType.Minutely)
        {
            throw new InvalidRecurrenceRuleException(rrule,
                $"Unsupported frequency: {pattern.Frequency}. " +
                "Supported frequencies are: Hourly, Daily, Weekly, Monthly, Yearly");
        }
    }

    /// <summary>
    /// Computes RRULE occurrences for a given date range using Ical.Net.
    /// DTSTART is always <paramref name="dtStart"/> (the config's original start) to preserve
    /// the occurrence pattern. The <paramref name="from"/> parameter is the effective query
    /// range start; Ical.Net filters occurrences to [from, to].
    /// </summary>
    /// <param name="rrule">RRULE string without "RRULE:" prefix.</param>
    /// <param name="dtStart">Config start date/time — ALWAYS used as DTSTART. NEVER substitute effectiveFrom here.</param>
    /// <param name="from">Effective range start (query filter). Occurrences before this are excluded.</param>
    /// <param name="to">Range end (query filter).</param>
    /// <returns>UTC DateTimes of occurrences within [from, to].</returns>
    private static IReadOnlyList<DateTime> GetOccurrences(
        string rrule,
        DateTime dtStart,
        DateTime from,
        DateTime to)
    {
        var calEvent = new CalendarEvent
        {
            Start = new CalDateTime(dtStart.Year, dtStart.Month, dtStart.Day,
                                    dtStart.Hour, dtStart.Minute, dtStart.Second, "UTC"),
            RecurrenceRules = { new RecurrencePattern(rrule) }
        };

        var calendar = new Calendar();
        calendar.Events.Add(calEvent);

        // v5 API: GetOccurrences takes only a start time; use TakeWhileBefore for end-date filtering
        return calendar
            .GetOccurrences<CalendarEvent>(new CalDateTime(from, "UTC"))
            .TakeWhileBefore(new CalDateTime(to, "UTC"))
            .Select(o => DateTime.SpecifyKind(o.Period.StartTime.Value, DateTimeKind.Utc))
            .ToList();
    }
}
