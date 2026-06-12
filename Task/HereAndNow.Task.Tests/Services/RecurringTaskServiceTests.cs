using FluentAssertions;
using HereAndNowService.Models;
using HereAndNowService.Repositories;
using HereAndNowService.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace HereAndNowService.TaskTests.Services;

public class RecurringTaskServiceTests
{
    private const string TestUserId = "auth0|test-user-123";
    private const string DailyAt9AmRrule = "FREQ=DAILY;BYHOUR=9;BYMINUTE=0;BYSECOND=0";

    // Fixed reference "now" for deterministic tests: Feb 15, 2026 at 12:00 UTC (noon)
    private static readonly DateTime Feb15AtNoon = new DateTime(2026, 2, 15, 12, 0, 0, DateTimeKind.Utc);

    // Common date range: Feb 14-16, 2026 (produces 3 daily occurrences at 09:00)
    private static readonly DateTime Feb14AtMidnight = new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Feb17AtMidnight = new DateTime(2026, 2, 17, 0, 0, 0, DateTimeKind.Utc);

    // Known occurrence datetimes for DailyAt9AmRrule in the range above
    private static readonly DateTime Feb14At9 = new DateTime(2026, 2, 14, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Feb15At9 = new DateTime(2026, 2, 15, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Feb16At9 = new DateTime(2026, 2, 16, 9, 0, 0, DateTimeKind.Utc);

    private static RecurringTaskConfigDocument CreateConfig(
        string id = "config-1",
        string text = "Test Task",
        string rrule = DailyAt9AmRrule,
        DateTime? startDateAndTime = null)
    {
        return new RecurringTaskConfigDocument
        {
            Id = id,
            Text = text,
            Rrule = rrule,
            StartDateAndTime = startDateAndTime ?? new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            UserId = TestUserId
        };
    }

    private static RecurringTaskStateOverrideDocument CreateOverride(
        string configId,
        DateTime recurrenceDateAndTime,
        string state,
        bool reminderDismissed = false)
    {
        return new RecurringTaskStateOverrideDocument
        {
            Id = RecurringTaskStateOverrideDocument.GenerateId(configId, recurrenceDateAndTime),
            ConfigId = configId,
            RecurrenceDateAndTime = recurrenceDateAndTime,
            State = state,
            UserId = TestUserId,
            ReminderDismissed = reminderDismissed
        };
    }

    private static RecurringTaskService CreateService(IRecurringTaskRepository? repo = null)
    {
        repo ??= Mock.Of<IRecurringTaskRepository>();
        var logger = Mock.Of<ILogger<RecurringTaskService>>();
        return new RecurringTaskService(repo, logger);
    }

    #region ComputeInstances — RRULE Computation (AC1, AC2)

    [Fact]
    public void ComputeInstances_DailyRrule_GeneratesCorrectOccurrences()
    {
        // Arrange — all occurrences in range will be "future" relative to now (Jan 1)
        var config = CreateConfig();
        var nowJan1AtMidnight = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, Array.Empty<RecurringTaskStateOverrideDocument>(),
            Feb14AtMidnight, Feb17AtMidnight, nowJan1AtMidnight);

        // Assert — 3 daily occurrences at 09:00 on Feb 14, 15, 16
        result.Should().HaveCount(3);
        result.Select(r => r.RecurrenceDateAndTime).Should().BeEquivalentTo(
            new[] { Feb14At9, Feb15At9, Feb16At9 });
        result.Should().AllSatisfy(r => r.State.Should().Be(TaskState.Scheduled));
    }

    [Fact]
    public void ComputeInstances_OccurrenceBeforeConfigStartDate_Excluded()
    {
        // Arrange — config starts Feb 15, but range starts Feb 14
        var configStart = new DateTime(2026, 2, 15, 9, 0, 0, DateTimeKind.Utc);
        var config = CreateConfig(startDateAndTime: configStart);
        var nowFeb10AtMidnight = new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc); // all future
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, Array.Empty<RecurringTaskStateOverrideDocument>(),
            Feb14AtMidnight, Feb17AtMidnight, nowFeb10AtMidnight);

        // Assert — Feb 14 occurrence excluded; only Feb 15 and Feb 16
        result.Should().NotContain(r => r.RecurrenceDateAndTime < configStart);
        result.Should().HaveCount(2);
        result.Select(r => r.RecurrenceDateAndTime)
              .Should().BeEquivalentTo(new[] { Feb15At9, Feb16At9 });
    }

    #endregion

    #region ComputeInstances — State Machine (AC3-AC7)

    [Fact]
    public void ComputeInstances_FutureOccurrence_ReturnsScheduled()
    {
        // Arrange — only Feb 16 in range; now is Feb 15 noon → Feb 16 is future
        var config = CreateConfig();
        var fromFeb16AtMidnight = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc);
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, Array.Empty<RecurringTaskStateOverrideDocument>(),
            fromFeb16AtMidnight, Feb17AtMidnight, Feb15AtNoon);

        // Assert
        result.Should().HaveCount(1);
        result[0].RecurrenceDateAndTime.Should().Be(Feb16At9);
        result[0].State.Should().Be(TaskState.Scheduled);
    }

    [Fact]
    public void ComputeInstances_MostRecentPastOccurrence_NoOverride_ReturnsOnDeck()
    {
        // Arrange — only Feb 15 at 09:00; now is Feb 15 at noon (past)
        var config = CreateConfig();
        var fromFeb15AtMidnight = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc);
        var toFeb16AtMidnight = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc);
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, Array.Empty<RecurringTaskStateOverrideDocument>(),
            fromFeb15AtMidnight, toFeb16AtMidnight, Feb15AtNoon);

        // Assert
        result.Should().HaveCount(1);
        result[0].RecurrenceDateAndTime.Should().Be(Feb15At9);
        result[0].State.Should().Be(TaskState.OnDeck);
    }

    [Fact]
    public void ComputeInstances_MultipleOldPastOccurrences_OnlyMostRecentIsOnDeck()
    {
        // Arrange — Feb 14, 15 at 09:00 both past; Feb 15 is most recent
        var config = CreateConfig();
        var toFeb16AtMidnight = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc);
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, Array.Empty<RecurringTaskStateOverrideDocument>(),
            Feb14AtMidnight, toFeb16AtMidnight, Feb15AtNoon);

        // Assert
        result.Should().HaveCount(2);
        var onDeck = result.Where(r => r.State == TaskState.OnDeck).ToList();
        onDeck.Should().HaveCount(1);
        onDeck[0].RecurrenceDateAndTime.Should().Be(Feb15At9);
    }

    [Fact]
    public void ComputeInstances_OlderPastOccurrences_NoOverride_ReturnSkipped()
    {
        // Arrange — Feb 14 and Feb 15 past; Feb 15 is OnDeck, Feb 14 should be Skipped
        var config = CreateConfig();
        var toFeb16AtMidnight = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc);
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, Array.Empty<RecurringTaskStateOverrideDocument>(),
            Feb14AtMidnight, toFeb16AtMidnight, Feb15AtNoon);

        // Assert
        var feb14Instance = result.Single(r => r.RecurrenceDateAndTime == Feb14At9);
        feb14Instance.State.Should().Be(TaskState.Skipped);
    }

    [Fact]
    public void ComputeInstances_InProgressOverride_NoNewerActive_ReturnsInProgress()
    {
        // Arrange — only Feb 14 occurrence with InProgress override; it's the only instance
        var config = CreateConfig();
        var toFeb15AtMidnight = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc);
        var overrides = new[] { CreateOverride("config-1", Feb14At9, TaskState.InProgress) };
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, overrides, Feb14AtMidnight, toFeb15AtMidnight, Feb15AtNoon);

        // Assert
        result.Should().HaveCount(1);
        result[0].RecurrenceDateAndTime.Should().Be(Feb14At9);
        result[0].State.Should().Be(TaskState.InProgress);
    }

    [Fact]
    public void ComputeInstances_InProgressOverride_WithNewerActiveInstance_OlderBecomesSkipped()
    {
        // Arrange — Feb 14 has InProgress override, but Feb 15 is more recent past (no override)
        // Feb 15 wins as OnDeck; Feb 14's InProgress is superseded → Skipped
        var config = CreateConfig();
        var toFeb16AtMidnight = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc);
        var overrides = new[] { CreateOverride("config-1", Feb14At9, TaskState.InProgress) };
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, overrides, Feb14AtMidnight, toFeb16AtMidnight, Feb15AtNoon);

        // Assert
        result.Should().HaveCount(2);
        var feb15 = result.Single(r => r.RecurrenceDateAndTime == Feb15At9);
        feb15.State.Should().Be(TaskState.OnDeck);

        var feb14 = result.Single(r => r.RecurrenceDateAndTime == Feb14At9);
        feb14.State.Should().Be(TaskState.Skipped, "InProgress was superseded by newer active instance");
    }

    [Fact]
    public void ComputeInstances_CompletedOverride_AlwaysRespected()
    {
        // Arrange — Feb 15 has Completed override; even as most-recent-past it should stay Completed
        var config = CreateConfig();
        var toFeb16AtMidnight = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc);
        var overrides = new[] { CreateOverride("config-1", Feb15At9, TaskState.Completed) };
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, overrides, Feb14AtMidnight, toFeb16AtMidnight, Feb15AtNoon);

        // Assert
        var feb15 = result.Single(r => r.RecurrenceDateAndTime == Feb15At9);
        feb15.State.Should().Be(TaskState.Completed, "terminal state is always respected");
    }

    [Fact]
    public void ComputeInstances_SkippedOverride_AlwaysRespected()
    {
        // Arrange — Feb 15 has Skipped override; even as most-recent-past it should stay Skipped
        var config = CreateConfig();
        var toFeb16AtMidnight = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc);
        var overrides = new[] { CreateOverride("config-1", Feb15At9, TaskState.Skipped) };
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, overrides, Feb14AtMidnight, toFeb16AtMidnight, Feb15AtNoon);

        // Assert
        var feb15 = result.Single(r => r.RecurrenceDateAndTime == Feb15At9);
        feb15.State.Should().Be(TaskState.Skipped, "terminal Skipped override is always respected");
    }

    [Fact]
    public void ComputeInstances_CompletedYesterday_BeforeTodaysOccurrence_OlderPastDoesNotBecomeOnDeck()
    {
        // Arrange — reproduces user-reported Kanban "Brush teeth" bug:
        //   Daily 9am task. now = 07:18 on Feb 16 — BEFORE today's 9am occurrence.
        //   Before the drag: Feb 15 9am was OnDeck (most recent past).
        //   User drags Feb 15 to Completed → Completed override stored.
        //   After recompute: Feb 14 9am wrongly becomes OnDeck.
        //   Expected: Feb 14 = Skipped, Feb 15 = Completed, Feb 16 = Scheduled, no OnDeck.
        var config = CreateConfig();
        var nowFeb16At0718 = new DateTime(2026, 2, 16, 7, 18, 0, DateTimeKind.Utc);
        var overrides = new[] { CreateOverride("config-1", Feb15At9, TaskState.Completed) };
        var service = CreateService();

        // Act
        IReadOnlyList<RecurringTaskInstance> result  = service.ComputeInstances(
            new[] { config }, overrides, Feb14AtMidnight, Feb17AtMidnight, nowFeb16At0718);

        // Assert
        result.Should().HaveCount(3);
        var feb14 = result.Single(r => r.RecurrenceDateAndTime == Feb14At9);
        var feb15 = result.Single(r => r.RecurrenceDateAndTime == Feb15At9);
        var feb16 = result.Single(r => r.RecurrenceDateAndTime == Feb16At9);

        feb14.State.Should().Be(TaskState.Skipped,
            "older past occurrence must not leak into OnDeck after the newer past was completed");
        feb15.State.Should().Be(TaskState.Completed, "user's explicit Completed override must be respected");
        feb16.State.Should().Be(TaskState.Scheduled, "today's 9am occurrence is still in the future at 07:18");

        result.Should().NotContain(r => r.State == TaskState.OnDeck,
            "nothing should be OnDeck: most-recent-past was Completed and today's 9am has not arrived");
    }

    [Fact]
    public void ComputeInstances_SkippedMostRecentPast_OlderOccurrenceBecomesSkipped()
    {
        // Arrange — symmetric to the Completed variant. Feb 15 9am has a Skipped override.
        // Feb 14 9am (older past) must also be Skipped — the active slot is consumed by Feb 15.
        // Verifies both terminal states behave identically under the simplified algorithm.
        var config = CreateConfig();
        var toFeb16AtMidnight = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc);
        var overrides = new[] { CreateOverride("config-1", Feb15At9, TaskState.Skipped) };
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, overrides, Feb14AtMidnight, toFeb16AtMidnight, Feb15AtNoon);

        // Assert
        result.Should().HaveCount(2);
        var feb15 = result.Single(r => r.RecurrenceDateAndTime == Feb15At9);
        feb15.State.Should().Be(TaskState.Skipped, "Skipped override is respected as terminal");

        var feb14 = result.Single(r => r.RecurrenceDateAndTime == Feb14At9);
        feb14.State.Should().Be(TaskState.Skipped,
            "older past must not leak into OnDeck when the active slot is already terminal");
    }

    [Fact]
    public void ComputeInstances_HourlyTask_CompletedCurrentOccurrence_OlderOccurrenceBecomesSkipped()
    {
        // Arrange — originally reported scenario, re-expressed on an HOURLY RRULE.
        //   Task recurs every 2 hours. On Feb 15: 17:00, 19:00, 21:00 UTC.
        //   nowFeb15At2029 = 20:29 UTC (between 19:00 past and 21:00 future).
        //   User completed the 19:00 occurrence (most recent past).
        //   Expected: 17:00 = Skipped, 19:00 = Completed, 21:00 = Scheduled, no OnDeck.
        // Covers a second RRULE frequency so the fix is not accidentally Daily-specific.
        var configStart = new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc);
        var config = CreateConfig(
            rrule: "FREQ=HOURLY;INTERVAL=2",
            startDateAndTime: configStart);
        var nowFeb15At2029 = new DateTime(2026, 2, 15, 20, 29, 0, DateTimeKind.Utc);
        var fromFeb15At16 = new DateTime(2026, 2, 15, 16, 0, 0, DateTimeKind.Utc);
        var toFeb15At22 = new DateTime(2026, 2, 15, 22, 0, 0, DateTimeKind.Utc);

        var feb15At17 = new DateTime(2026, 2, 15, 17, 0, 0, DateTimeKind.Utc);
        var feb15At19 = new DateTime(2026, 2, 15, 19, 0, 0, DateTimeKind.Utc);
        var feb15At21 = new DateTime(2026, 2, 15, 21, 0, 0, DateTimeKind.Utc);

        var overrides = new[] { CreateOverride("config-1", feb15At19, TaskState.Completed) };
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, overrides, fromFeb15At16, toFeb15At22, nowFeb15At2029);

        // Assert
        result.Should().HaveCount(3);
        result.Single(r => r.RecurrenceDateAndTime == feb15At17).State
            .Should().Be(TaskState.Skipped, "17:00 is an older past occurrence");
        result.Single(r => r.RecurrenceDateAndTime == feb15At19).State
            .Should().Be(TaskState.Completed, "19:00 has the Completed override");
        result.Single(r => r.RecurrenceDateAndTime == feb15At21).State
            .Should().Be(TaskState.Scheduled, "21:00 is still future at 20:29 nowFeb15At2029");
        result.Should().NotContain(r => r.State == TaskState.OnDeck,
            "no OnDeck should exist: the active slot is Completed and the next occurrence is future");
    }

    #endregion

    #region ComputeInstances — Override Fallback (Defensive)

    [Fact]
    public void ComputeInstances_UnexpectedStoredState_InstanceIncludedWithStoredState()
    {
        // Arrange — OnDeck is a computed state that should never be persisted to Cosmos DB.
        // If it appears in the override store (e.g., a data-migration artifact), the defensive
        // fallback should pass it through rather than silently dropping the occurrence.
        var config = CreateConfig();
        var fromFeb15AtMidnight = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc);
        var toFeb16AtMidnight = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc);
        var overrides = new[] { CreateOverride("config-1", Feb15At9, TaskState.OnDeck) };
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, overrides, fromFeb15AtMidnight, toFeb16AtMidnight, Feb15AtNoon);

        // Assert — occurrence must not be silently dropped; state passed through as-is
        result.Should().HaveCount(1);
        result[0].RecurrenceDateAndTime.Should().Be(Feb15At9);
        result[0].State.Should().Be(TaskState.OnDeck, "unexpected stored state is passed through as-is");
    }

    [Fact]
    public void ComputeInstances_UnexpectedStoredState_Scheduled_InstanceIncludedWithStoredState()
    {
        // Arrange — Scheduled is another computed state never legitimately persisted.
        // Same defensive contract: pass through, do not drop.
        var config = CreateConfig();
        var fromFeb15AtMidnight = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc);
        var toFeb16AtMidnight = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc);
        var overrides = new[] { CreateOverride("config-1", Feb15At9, TaskState.Scheduled) };
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, overrides, fromFeb15AtMidnight, toFeb16AtMidnight, Feb15AtNoon);

        // Assert
        result.Should().HaveCount(1);
        result[0].RecurrenceDateAndTime.Should().Be(Feb15At9);
        result[0].State.Should().Be(TaskState.Scheduled, "unexpected stored state is passed through as-is");
    }

    [Fact]
    public void ComputeInstances_UnexpectedStoredState_OlderPastIsSkipped()
    {
        // Arrange — Under the simplified algorithm, the active instance is ALWAYS the most
        // recent past occurrence, regardless of its override state. Older past occurrences
        // are always Skipped (unless they themselves carry a Completed/Skipped terminal override).
        //
        // Occurrences: Feb 15 and Feb 14 (both past; now = Feb 15 noon).
        // Feb 15 has an unexpected OnDeck override → passed through defensively.
        // Feb 14 has no override → Skipped (older past).
        var config = CreateConfig();
        var toFeb16AtMidnight = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc);
        var overrides = new[] { CreateOverride("config-1", Feb15At9, TaskState.OnDeck) };
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, overrides, Feb14AtMidnight, toFeb16AtMidnight, Feb15AtNoon);

        // Assert — both instances present
        result.Should().HaveCount(2);
        var feb15 = result.Single(r => r.RecurrenceDateAndTime == Feb15At9);
        feb15.State.Should().Be(TaskState.OnDeck, "unexpected state passed through from override");

        var feb14 = result.Single(r => r.RecurrenceDateAndTime == Feb14At9);
        feb14.State.Should().Be(TaskState.Skipped,
            "older past occurrences are always Skipped — only the most recent past is the active instance");
    }

    [Fact]
    public void ComputeInstances_FutureOccurrenceWithCompletedOverride_StoredStatePassedThrough()
    {
        // Arrange — Feb 16 9am is FUTURE (now = Feb 15 noon). A Completed override is
        // anomalous for a future slot (no normal flow writes overrides there) but must
        // be surfaced defensively rather than silently dropped to Scheduled.
        // Range starts at Feb15AtNoon so Feb 15 9am is excluded — isolates the future-override contract.
        var config = CreateConfig();
        var overrides = new[] { CreateOverride("config-1", Feb16At9, TaskState.Completed) };
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, overrides, Feb15AtNoon, Feb17AtMidnight, Feb15AtNoon);

        // Assert
        result.Should().HaveCount(1);
        var feb16 = result.Single(r => r.RecurrenceDateAndTime == Feb16At9);
        feb16.State.Should().Be(TaskState.Completed,
            "future-occurrence overrides must pass through so data anomalies stay visible and fixable");
    }

    [Fact]
    public void ComputeInstances_FutureOccurrenceWithUnexpectedOverride_StoredStatePassedThrough()
    {
        // Arrange — Feb 16 9am future; OnDeck override (a computed state never legitimately
        // persisted). Defensive contract: pass through, matching the past-side UnexpectedStoredState tests.
        var config = CreateConfig();
        var overrides = new[] { CreateOverride("config-1", Feb16At9, TaskState.OnDeck) };
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, overrides, Feb15AtNoon, Feb17AtMidnight, Feb15AtNoon);

        // Assert
        result.Should().HaveCount(1);
        var feb16 = result.Single(r => r.RecurrenceDateAndTime == Feb16At9);
        feb16.State.Should().Be(TaskState.OnDeck,
            "unexpected stored state on a future occurrence is passed through defensively");
    }

    #endregion

    #region ComputeInstances — ReminderDismissed Mapping

    [Fact]
    public void ComputeInstances_NoOverride_ReminderDismissedDefaultsFalse()
    {
        // Arrange — active occurrence (Feb 15 9am, now Feb 15 noon) with no stored override
        var config = CreateConfig();
        var fromFeb15AtMidnight = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc);
        var toFeb16AtMidnight = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc);
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, Array.Empty<RecurringTaskStateOverrideDocument>(),
            fromFeb15AtMidnight, toFeb16AtMidnight, Feb15AtNoon);

        // Assert
        result.Should().HaveCount(1);
        result[0].ReminderDismissed.Should().BeFalse();
    }

    [Fact]
    public void ComputeInstances_ActiveOccurrenceWithDismissedOverride_ReminderDismissedTrue()
    {
        // Arrange — active occurrence (most recent past) carries an InProgress override
        // whose reminder was dismissed
        var config = CreateConfig();
        var fromFeb15AtMidnight = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc);
        var toFeb16AtMidnight = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc);
        var overrides = new[]
            { CreateOverride("config-1", Feb15At9, TaskState.InProgress, reminderDismissed: true) };
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, overrides, fromFeb15AtMidnight, toFeb16AtMidnight, Feb15AtNoon);

        // Assert
        result.Should().HaveCount(1);
        result[0].State.Should().Be(TaskState.InProgress);
        result[0].ReminderDismissed.Should().BeTrue();
    }

    [Fact]
    public void ComputeInstances_OlderPastTerminalOverrideWithDismissed_ReminderDismissedTrue()
    {
        // Arrange — Feb 14 (older past) has a Completed override with dismissed reminder;
        // Feb 15 is the active instance
        var config = CreateConfig();
        var toFeb16AtMidnight = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc);
        var overrides = new[]
            { CreateOverride("config-1", Feb14At9, TaskState.Completed, reminderDismissed: true) };
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, overrides, Feb14AtMidnight, toFeb16AtMidnight, Feb15AtNoon);

        // Assert
        var feb14 = result.Single(r => r.RecurrenceDateAndTime == Feb14At9);
        feb14.State.Should().Be(TaskState.Completed);
        feb14.ReminderDismissed.Should().BeTrue();

        var feb15 = result.Single(r => r.RecurrenceDateAndTime == Feb15At9);
        feb15.ReminderDismissed.Should().BeFalse("no override exists for the active occurrence");
    }

    [Fact]
    public void ComputeInstances_OlderPastNonTerminalOverrideWithDismissed_MirrorsDismissedDespiteSkippedState()
    {
        // Arrange — Feb 14 (older past) has an anomalous InProgress override with dismissed=true.
        // State resolution forces older past non-terminal occurrences to Skipped, but the
        // dismissed flag must still mirror the stored override regardless of how state resolves.
        var config = CreateConfig();
        var toFeb16AtMidnight = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc);
        var overrides = new[]
            { CreateOverride("config-1", Feb14At9, TaskState.InProgress, reminderDismissed: true) };
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, overrides, Feb14AtMidnight, toFeb16AtMidnight, Feb15AtNoon);

        // Assert
        var feb14 = result.Single(r => r.RecurrenceDateAndTime == Feb14At9);
        feb14.State.Should().Be(TaskState.Skipped,
            "older past non-terminal occurrences resolve to Skipped");
        feb14.ReminderDismissed.Should().BeTrue(
            "the dismissed flag mirrors the stored override regardless of state resolution");
    }

    [Fact]
    public void ComputeInstances_FutureAnomalyOverrideWithDismissed_ReminderDismissedTrue()
    {
        // Arrange — Feb 16 9am is future (now Feb 15 noon); anomalous Completed override
        // with dismissed=true is passed through defensively, including the dismissed flag
        var config = CreateConfig();
        var overrides = new[]
            { CreateOverride("config-1", Feb16At9, TaskState.Completed, reminderDismissed: true) };
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, overrides, Feb15AtNoon, Feb17AtMidnight, Feb15AtNoon);

        // Assert
        var feb16 = result.Single(r => r.RecurrenceDateAndTime == Feb16At9);
        feb16.ReminderDismissed.Should().BeTrue();
    }

    #endregion

    #region ComputeInstances — Instance Properties (AC8, AC9)

    [Fact]
    public void ComputeInstances_TextDerivedFromConfig()
    {
        // Arrange
        var config = CreateConfig(text: "Daily standup");
        var fromFeb15AtMidnight = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc);
        var toFeb16AtMidnight = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc);
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, Array.Empty<RecurringTaskStateOverrideDocument>(),
            fromFeb15AtMidnight, toFeb16AtMidnight, Feb15AtNoon);

        // Assert
        result.Should().HaveCount(1);
        result[0].Text.Should().Be("Daily standup");
    }

    [Fact]
    public void ComputeInstances_InstanceIdMatchesCompositeKeyFormat()
    {
        // Arrange — config id "abc-123", occurrence at Feb 15 09:00 UTC
        var config = CreateConfig(id: "abc-123");
        var fromFeb15AtMidnight = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc);
        var toFeb16AtMidnight = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc);
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, Array.Empty<RecurringTaskStateOverrideDocument>(),
            fromFeb15AtMidnight, toFeb16AtMidnight, Feb15AtNoon);

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be("abc-123_2026-02-15T09:00:00Z");
        result[0].RecurringTaskConfigId.Should().Be("abc-123");
    }

    #endregion

    #region ComputeInstances — NFR Tests (AC10, AC11)

    [Fact]
    public void ComputeInstances_NoOccurrencesInRange_ReturnsEmpty()
    {
        // Arrange — weekly rule; range is only 2 days, may not contain any occurrence
        var config = CreateConfig(
            rrule: "FREQ=WEEKLY;BYDAY=MO;BYHOUR=9;BYMINUTE=0;BYSECOND=0",
            startDateAndTime: new DateTime(2026, 2, 2, 9, 0, 0, DateTimeKind.Utc)); // Monday Feb 2
        // Range: Friday Feb 6 to Sunday Feb 8 — no Monday in this range
        var fromFeb6AtMidnight = new DateTime(2026, 2, 6, 0, 0, 0, DateTimeKind.Utc);
        var toFeb9AtMidnight = new DateTime(2026, 2, 9, 0, 0, 0, DateTimeKind.Utc);
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, Array.Empty<RecurringTaskStateOverrideDocument>(),
            fromFeb6AtMidnight, toFeb9AtMidnight, Feb15AtNoon);

        // Assert
        result.Should().BeEmpty("no Monday falls in the Fri-Sun range");
    }

    [Fact]
    public void ComputeInstances_SameInputsDifferentCallTimes_ReturnsSameResult()
    {
        // Arrange — determinism: same now produces identical output (AC10)
        var config = CreateConfig();
        var overrides = new[] { CreateOverride("config-1", Feb14At9, TaskState.Completed) };
        var service = CreateService();

        // Act
        var result1 = service.ComputeInstances(new[] { config }, overrides, Feb14AtMidnight, Feb17AtMidnight, Feb15AtNoon);
        var result2 = service.ComputeInstances(new[] { config }, overrides, Feb14AtMidnight, Feb17AtMidnight, Feb15AtNoon);

        // Assert
        result1.Count.Should().Be(result2.Count);
        for (var i = 0; i < result1.Count; i++)
        {
            result1[i].State.Should().Be(result2[i].State);
            result1[i].RecurrenceDateAndTime.Should().Be(result2[i].RecurrenceDateAndTime);
            result1[i].Id.Should().Be(result2[i].Id);
        }
    }

    #endregion

    #region ComputeInstances — Multiple Configs (AC5)

    [Fact]
    public void ComputeInstances_MultipleConfigs_EachAppliesOneActiveAtATime_Independently()
    {
        // Arrange — two configs, each with their own activeCandidate scope
        var config1 = CreateConfig(id: "config-1");
        var config2 = CreateConfig(id: "config-2", rrule: "FREQ=DAILY;BYHOUR=10;BYMINUTE=0;BYSECOND=0");

        // Config2 start is Jan 1 so it has occurrences too
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config1, config2 }, Array.Empty<RecurringTaskStateOverrideDocument>(),
            Feb14AtMidnight, Feb17AtMidnight, Feb15AtNoon);

        // Assert — each config independently has exactly 1 OnDeck
        var config1Instances = result.Where(r => r.RecurringTaskConfigId == "config-1").ToList();
        var config2Instances = result.Where(r => r.RecurringTaskConfigId == "config-2").ToList();

        config1Instances.Count(r => r.State == TaskState.OnDeck).Should().Be(1,
            "config-1 should have exactly one OnDeck instance");
        config2Instances.Count(r => r.State == TaskState.OnDeck).Should().Be(1,
            "config-2 should have exactly one OnDeck instance (independent scope)");
    }

    #endregion

    #region GetComputedInstancesAsync — DB Query Pattern (AC12, NFR43)

    [Fact]
    public async Task GetComputedInstancesAsync_ExactlyTwoRepositoryCallsMade()
    {
        // Arrange
        var mockRepo = new Mock<IRecurringTaskRepository>(MockBehavior.Strict);

        mockRepo.Setup(r => r.GetAllConfigsAsync(TestUserId))
                .ReturnsAsync(Array.Empty<RecurringTaskConfigDocument>());
        mockRepo.Setup(r => r.GetStateOverridesForDateRangeAsync(TestUserId, Feb14AtMidnight, Feb17AtMidnight))
                .ReturnsAsync(Array.Empty<RecurringTaskStateOverrideDocument>());

        var service = CreateService(mockRepo.Object);

        // Act
        await service.GetComputedInstancesAsync(TestUserId, Feb14AtMidnight, Feb17AtMidnight);

        // Assert — exactly these two calls, no others (MockBehavior.Strict enforces this)
        mockRepo.Verify(r => r.GetAllConfigsAsync(TestUserId), Times.Once);
        mockRepo.Verify(r => r.GetStateOverridesForDateRangeAsync(TestUserId, Feb14AtMidnight, Feb17AtMidnight), Times.Once);
        mockRepo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetComputedInstancesAsync_DateRangeExceeds365Days_ThrowsArgumentException()
    {
        // Arrange
        var fromJan1AtMidnight = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = fromJan1AtMidnight.AddDays(366); // 366 days > 365 limit
        var service = CreateService();

        // Act
        var act = () => service.GetComputedInstancesAsync(TestUserId, fromJan1AtMidnight, to);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*365 days*");
    }

    [Fact]
    public async Task GetComputedInstancesAsync_NonUtcFromDate_ThrowsArgumentException()
    {
        // Arrange — from is Local kind
        var fromFeb14AtMidnightLocal = new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Local);
        var service = CreateService();

        // Act
        var act = () => service.GetComputedInstancesAsync(TestUserId, fromFeb14AtMidnightLocal, Feb17AtMidnight);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*UTC*");
    }

    [Fact]
    public async Task GetComputedInstancesAsync_NonUtcToDate_ThrowsArgumentException()
    {
        // Arrange — to is Unspecified kind
        var toFeb17AtMidnightUnspecified = new DateTime(2026, 2, 17, 0, 0, 0, DateTimeKind.Unspecified);
        var service = CreateService();

        // Act
        var act = () => service.GetComputedInstancesAsync(TestUserId, Feb14AtMidnight, toFeb17AtMidnightUnspecified);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*UTC*");
    }

    [Fact]
    public async Task GetComputedInstancesAsync_ReturnsComputedInstances()
    {
        // Arrange
        var fromFeb15AtMidnight = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc);
        var toFeb16AtMidnight = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc);
        var config = CreateConfig();
        var mockRepo = new Mock<IRecurringTaskRepository>();

        mockRepo.Setup(r => r.GetAllConfigsAsync(TestUserId))
                .ReturnsAsync(new[] { config });
        mockRepo.Setup(r => r.GetStateOverridesForDateRangeAsync(TestUserId, fromFeb15AtMidnight, toFeb16AtMidnight))
                .ReturnsAsync(Array.Empty<RecurringTaskStateOverrideDocument>());

        var service = CreateService(mockRepo.Object);

        // Act
        var result = await service.GetComputedInstancesAsync(TestUserId, fromFeb15AtMidnight, toFeb16AtMidnight);

        // Assert — should return the computed Feb 15 instance
        result.Should().HaveCount(1);
        result[0].RecurrenceDateAndTime.Should().Be(Feb15At9);
        result[0].Text.Should().Be("Test Task");
        result[0].RecurringTaskConfigId.Should().Be("config-1");
    }

    [Fact]
    public async Task GetComputedInstancesAsync_Exactly365DayRange_DoesNotThrow()
    {
        // Arrange — boundary: exactly 365 days is allowed
        var fromJan1AtMidnight = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = fromJan1AtMidnight.AddDays(365);
        var mockRepo = new Mock<IRecurringTaskRepository>();

        mockRepo.Setup(r => r.GetAllConfigsAsync(TestUserId))
                .ReturnsAsync(Array.Empty<RecurringTaskConfigDocument>());
        mockRepo.Setup(r => r.GetStateOverridesForDateRangeAsync(TestUserId, fromJan1AtMidnight, to))
                .ReturnsAsync(Array.Empty<RecurringTaskStateOverrideDocument>());

        var service = CreateService(mockRepo.Object);

        // Act
        var act = () => service.GetComputedInstancesAsync(TestUserId, fromJan1AtMidnight, to);

        // Assert
        await act.Should().NotThrowAsync("exactly 365 days is within the allowed limit");
    }

    #endregion

    #region UpdateConfigAsync — hasReminder Stamping

    [Fact]
    public async Task UpdateConfigAsync_WhenHasReminderSetTrueOnFalseConfig_StampsHasReminderEnabledAt()
    {
        // Arrange
        var existingConfig = CreateConfig();
        existingConfig.HasReminder = false;
        existingConfig.HasReminderEnabledAt = null;

        var mockRepo = new Mock<IRecurringTaskRepository>();
        mockRepo.Setup(r => r.GetConfigByIdAsync(TestUserId, "config-1"))
                .ReturnsAsync(existingConfig);
        mockRepo.Setup(r => r.UpdateConfigAsync(It.IsAny<RecurringTaskConfigDocument>()))
                .ReturnsAsync((RecurringTaskConfigDocument doc) => doc);

        var service = CreateService(mockRepo.Object);
        var before = DateTime.UtcNow;

        // Act
        var result = await service.UpdateConfigAsync(
            TestUserId, "config-1", "Test Task", DailyAt9AmRrule,
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            hasReminder: true);

        var after = DateTime.UtcNow;

        // Assert
        result.HasReminder.Should().BeTrue();
        result.HasReminderEnabledAt.Should().NotBeNull();
        result.HasReminderEnabledAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task UpdateConfigAsync_WhenHasReminderRemainsTrue_PreservesExistingHasReminderEnabledAt()
    {
        // Arrange
        var t1 = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var existingConfig = CreateConfig();
        existingConfig.HasReminder = true;
        existingConfig.HasReminderEnabledAt = t1;

        var mockRepo = new Mock<IRecurringTaskRepository>();
        mockRepo.Setup(r => r.GetConfigByIdAsync(TestUserId, "config-1"))
                .ReturnsAsync(existingConfig);
        mockRepo.Setup(r => r.UpdateConfigAsync(It.IsAny<RecurringTaskConfigDocument>()))
                .ReturnsAsync((RecurringTaskConfigDocument doc) => doc);

        var service = CreateService(mockRepo.Object);

        // Act
        var result = await service.UpdateConfigAsync(
            TestUserId, "config-1", "Test Task", DailyAt9AmRrule,
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            hasReminder: true);

        // Assert
        result.HasReminderEnabledAt.Should().Be(t1);
    }

    [Fact]
    public async Task UpdateConfigAsync_WhenHasReminderToggledFalse_PreservesHasReminderEnabledAt()
    {
        // Arrange
        var t1 = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var existingConfig = CreateConfig();
        existingConfig.HasReminder = true;
        existingConfig.HasReminderEnabledAt = t1;

        var mockRepo = new Mock<IRecurringTaskRepository>();
        mockRepo.Setup(r => r.GetConfigByIdAsync(TestUserId, "config-1"))
                .ReturnsAsync(existingConfig);
        mockRepo.Setup(r => r.UpdateConfigAsync(It.IsAny<RecurringTaskConfigDocument>()))
                .ReturnsAsync((RecurringTaskConfigDocument doc) => doc);

        var service = CreateService(mockRepo.Object);

        // Act
        var result = await service.UpdateConfigAsync(
            TestUserId, "config-1", "Test Task", DailyAt9AmRrule,
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            hasReminder: false);

        // Assert
        result.HasReminder.Should().BeFalse();
        result.HasReminderEnabledAt.Should().Be(t1);
    }

    [Fact]
    public async Task UpdateConfigAsync_WhenHasReminderRemainsFalse_DoesNotSetHasReminderEnabledAt()
    {
        // Arrange
        var existingConfig = CreateConfig();
        existingConfig.HasReminder = false;
        existingConfig.HasReminderEnabledAt = null;

        var mockRepo = new Mock<IRecurringTaskRepository>();
        mockRepo.Setup(r => r.GetConfigByIdAsync(TestUserId, "config-1"))
                .ReturnsAsync(existingConfig);
        mockRepo.Setup(r => r.UpdateConfigAsync(It.IsAny<RecurringTaskConfigDocument>()))
                .ReturnsAsync((RecurringTaskConfigDocument doc) => doc);

        var service = CreateService(mockRepo.Object);

        // Act
        var result = await service.UpdateConfigAsync(
            TestUserId, "config-1", "Test Task", DailyAt9AmRrule,
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            hasReminder: false);

        // Assert
        result.HasReminder.Should().BeFalse();
        result.HasReminderEnabledAt.Should().BeNull();
    }

    #endregion
}
