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
