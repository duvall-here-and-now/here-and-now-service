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
    public async Task<IReadOnlyList<RecurringTaskConfigDocument>> GetAllConfigsAsync(string userId)
    {
        var configs = await _repository.GetAllConfigsAsync(userId);
        return configs.ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<RecurringTaskConfigDocument> GetConfigByIdAsync(string userId, string configId)
    {
        var config = await _repository.GetConfigByIdAsync(userId, configId);
        if (config == null)
            throw new RecurringTaskConfigNotFoundException(configId);
        return config;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RecurringTaskInstance>> GetComputedInstancesForAllConfigsAsync(
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
        Dictionary<string, RecurringTaskStateOverrideDocument> overrideLookup = overrides.ToDictionary(
            o => $"{o.ConfigId}_{o.RecurrenceDateAndTime:O}");

        foreach (var config in configs)
        {
            // Exclude occurrences before config's start date (FR75/AC2)
            var effectiveFrom = config.StartDateAndTime > from ? config.StartDateAndTime : from;
            var occurrences = GetOccurrences(config.Rrule, config.StartDateAndTime, effectiveFrom, to);

            // 4-step computation:
            //   1. Every occurrence starts as Scheduled (set here at construction).
            //   2. Future occurrences stay Scheduled unless a stored override exists
            //      (defensive pass-through for data anomalies; normal flows never write these).
            //   3. Older past occurrences become Skipped (or keep a Completed/Skipped override).
            //   4. The active instance (most recent past) becomes OnDeck unless an override applies.
            var instances = occurrences
                .Select(o => new RecurringTaskInstance(config, o, TaskState.Scheduled))
                .ToList();

            ApplyFutureOverrides(instances, overrideLookup, config.Id, utcNow);
            MarkPastInstances(instances, overrideLookup, config.Id, utcNow);
            ApplyActiveOverride(instances, overrideLookup, config.Id, utcNow);

            results.AddRange(instances);
        }

        return results;
    }

    /// <summary>
    /// Step 3: Resolve every past occurrence EXCEPT the active one (most recent past).
    /// Default: Skipped. Exception: a Completed or Skipped override is a terminal state and is kept.
    /// </summary>
    private static void MarkPastInstances(
        IList<RecurringTaskInstance> instances,
        IReadOnlyDictionary<string, RecurringTaskStateOverrideDocument> overrideLookup,
        string configId,
        DateTime utcNow)
    {
        var pastNewestFirst = instances
            .Where(i => i.RecurrenceDateAndTime <= utcNow)
            .OrderByDescending(i => i.RecurrenceDateAndTime)
            .ToList();

        // Skip index 0 — the active instance is handled in step 4.
        for (var i = 1; i < pastNewestFirst.Count; i++)
        {
            var instance = pastNewestFirst[i];
            var key = $"{configId}_{instance.RecurrenceDateAndTime:O}";

            if (overrideLookup.TryGetValue(key, out var stored))
            {
                // Dismissed flag mirrors the stored override regardless of how state resolves
                instance.ReminderDismissed = stored.ReminderDismissed;
                instance.State = stored.State == TaskState.Completed || stored.State == TaskState.Skipped
                    ? stored.State
                    : TaskState.Skipped;
            }
            else
            {
                instance.State = TaskState.Skipped;
            }
        }
    }

    /// <summary>
    /// Step 4: Resolve the active instance (most recent past).
    /// - Stored override present → pass the stored state through as-is
    /// - No override → OnDeck
    /// </summary>
    private static void ApplyActiveOverride(
        IList<RecurringTaskInstance> instances,
        IReadOnlyDictionary<string, RecurringTaskStateOverrideDocument> overrideLookup,
        string configId,
        DateTime utcNow)
    {
        var active = instances
            .Where(i => i.RecurrenceDateAndTime <= utcNow)
            .MaxBy(i => i.RecurrenceDateAndTime);
        if (active is null) return;

        var key = $"{configId}_{active.RecurrenceDateAndTime:O}";
        if (overrideLookup.TryGetValue(key, out var stored))
        {
            active.State = stored.State;
            active.ReminderDismissed = stored.ReminderDismissed;
        }
        else
        {
            active.State = TaskState.OnDeck;
        }
    }

    /// <summary>
    /// Defensive step: resolve any stored override on a FUTURE occurrence.
    /// No override is ever expected here — normal write flows only persist overrides for
    /// past occurrences (state commands require past-only states). When one does exist —
    /// migration artifact, clock skew, or data anomaly — pass the stored state through so
    /// the anomaly is visible in computed output, and warn unconditionally.
    /// </summary>
    private void ApplyFutureOverrides(
        IList<RecurringTaskInstance> instances,
        IReadOnlyDictionary<string, RecurringTaskStateOverrideDocument> overrideLookup,
        string configId,
        DateTime utcNow)
    {
        foreach (var instance in instances.Where(i => i.RecurrenceDateAndTime > utcNow))
        {
            var key = $"{configId}_{instance.RecurrenceDateAndTime:O}";
            if (!overrideLookup.TryGetValue(key, out var stored)) continue;

            _logger.LogWarning(
                "RecurringTaskService: unexpected stored override (state '{State}') for future key '{Key}'. " +
                "No overrides are expected for future occurrences. Passing through as-is.",
                stored.State, key);
            instance.State = stored.State;
            instance.ReminderDismissed = stored.ReminderDismissed;
        }
    }

    /// <inheritdoc />
    public async Task<RecurringTaskConfigDocument> CreateConfigAsync(
        string userId, string id, string text, string rrule, DateTime startDateAndTime, bool hasReminder = false)
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

        if (hasReminder)
        {
            config.HasReminder = true;
            config.HasReminderEnabledAt = DateTime.UtcNow;
        }

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
        string userId, string id, string text, string rrule, DateTime startDateAndTime, bool hasReminder = false)
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

        if (hasReminder && !existing.HasReminder)
            existing.HasReminderEnabledAt = DateTime.UtcNow;
        existing.HasReminder = hasReminder;

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
        var instance = await GetTargetInstanceAsync(userId, configId, recurrenceDateAndTime);

        if (instance.State != TaskState.OnDeck)
        {
            throw new InvalidStateTransitionException(
                instance.Id, instance.State, "StartRecurringTask",
                $"Cannot start recurring task instance in state '{instance.State}'. Only OnDeck instances can be started.");
        }

        await UpsertTransitionAsync(userId, configId, recurrenceDateAndTime, TaskState.InProgress);
    }

    /// <inheritdoc />
    public async Task RevertRecurringTaskToOnDeckAsync(string userId, string configId, DateTime recurrenceDateAndTime)
    {
        recurrenceDateAndTime = EnsureUtc(recurrenceDateAndTime);
        var instance = await GetTargetInstanceAsync(userId, configId, recurrenceDateAndTime);

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
        var instance = await GetTargetInstanceAsync(userId, configId, recurrenceDateAndTime);

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
                // Allowed only if no newer active instance (sibling list fetched lazily —
                // extra Cosmos reads on this rare path are accepted by design)
                await ValidateNoNewerActiveInstanceAsync(userId, instance, "CompleteRecurringTask");
                break;
            default:
                throw new InvalidStateTransitionException(
                    instance.Id, instance.State, "CompleteRecurringTask",
                    $"Cannot complete recurring task instance in state '{instance.State}'.");
        }

        await UpsertTransitionAsync(userId, configId, recurrenceDateAndTime, TaskState.Completed);
    }

    /// <inheritdoc />
    public async Task SkipRecurringTaskAsync(string userId, string configId, DateTime recurrenceDateAndTime)
    {
        recurrenceDateAndTime = EnsureUtc(recurrenceDateAndTime);
        var instance = await GetTargetInstanceAsync(userId, configId, recurrenceDateAndTime);

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
                // Allowed only if no newer active instance (sibling list fetched lazily —
                // extra Cosmos reads on this rare path are accepted by design)
                await ValidateNoNewerActiveInstanceAsync(userId, instance, "SkipRecurringTask");
                break;
            default:
                throw new InvalidStateTransitionException(
                    instance.Id, instance.State, "SkipRecurringTask",
                    $"Cannot skip recurring task instance in state '{instance.State}'.");
        }

        await UpsertTransitionAsync(userId, configId, recurrenceDateAndTime, TaskState.Skipped);
    }

    /// <summary>
    /// Resolves the target instance's current state by computing instances around the target date.
    /// Standalone — callers needing the sibling list use <see cref="ComputeInstancesForConfigAsync"/>.
    /// </summary>
    private async Task<RecurringTaskInstance> GetTargetInstanceAsync(
        string userId, string configId, DateTime recurrenceDateAndTime)
    {
        var allInstances = await ComputeInstancesForConfigAsync(userId, configId, recurrenceDateAndTime);

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

        return targetInstance;
    }

    /// <summary>
    /// Runs the full compute pipeline (config fetch → overrides fetch → ComputeInstances) for one
    /// config over a ±365-day window around <paramref name="around"/> (max range).
    /// </summary>
    private async Task<IReadOnlyList<RecurringTaskInstance>> ComputeInstancesForConfigAsync(
        string userId, string configId, DateTime around)
    {
        var config = await _repository.GetConfigByIdAsync(userId, configId);
        if (config == null)
            throw new RecurringTaskConfigNotFoundException(configId);

        var from = around.AddDays(-365);
        var to = around.AddDays(365);

        var overrides = (await _repository.GetStateOverridesForDateRangeAsync(userId, from, to)).ToList();

        return ComputeInstances(new[] { config }, overrides, from, to, DateTime.UtcNow);
    }

    /// <summary>
    /// Lazily computes the sibling list (a fresh snapshot — extra Cosmos reads by design) and
    /// validates no newer active instance exists. Only called on the rare resurrection branches.
    /// </summary>
    private async Task ValidateNoNewerActiveInstanceAsync(
        string userId, RecurringTaskInstance targetInstance, string attemptedAction)
    {
        var allInstances = await ComputeInstancesForConfigAsync(
            userId, targetInstance.RecurringTaskConfigId, targetInstance.RecurrenceDateAndTime);
        ValidateNoNewerActiveInstance(targetInstance, allInstances, attemptedAction);
    }

    /// <summary>
    /// Persists a state transition via read-modify-write: the existing override document (if any)
    /// is read and only the pertinent fields are changed, so unrelated fields like
    /// <c>reminderDismissed</c> survive every upsert-based transition (Revert deletes the document
    /// instead and does not flow through here). Creates a fresh document when none exists.
    /// </summary>
    private async Task UpsertTransitionAsync(
        string userId, string configId, DateTime recurrenceDateAndTime, string newState)
    {
        var overrideId = RecurringTaskStateOverrideDocument.GenerateId(configId, recurrenceDateAndTime);
        var existing = await _repository.GetStateOverrideByIdAsync(userId, overrideId);

        if (existing != null)
        {
            existing.State = newState;
            existing.UpdatedAt = DateTime.UtcNow;
            await _repository.UpsertStateOverrideAsync(existing);
        }
        else
        {
            var newOverrideDocument = CreateOverrideDocument(
                userId, configId, recurrenceDateAndTime, newState);
            await _repository.UpsertStateOverrideAsync(newOverrideDocument);
        }
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
