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
    private static readonly DateTime UtcNow = new DateTime(2026, 2, 15, 12, 0, 0, DateTimeKind.Utc);

    // Common date range: Feb 14-16, 2026 (produces 3 daily occurrences at 09:00)
    private static readonly DateTime RangeFrom = new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime RangeTo = new DateTime(2026, 2, 17, 0, 0, 0, DateTimeKind.Utc);

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
        string state)
    {
        return new RecurringTaskStateOverrideDocument
        {
            Id = RecurringTaskStateOverrideDocument.GenerateId(configId, recurrenceDateAndTime),
            ConfigId = configId,
            RecurrenceDateAndTime = recurrenceDateAndTime,
            State = state,
            UserId = TestUserId
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
        // Arrange — all occurrences in range will be "future" relative to utcNow (Jan 1)
        var config = CreateConfig();
        var utcNow = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, Array.Empty<RecurringTaskStateOverrideDocument>(),
            RangeFrom, RangeTo, utcNow);

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
        var from = new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 2, 17, 0, 0, 0, DateTimeKind.Utc);
        var utcNow = new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc); // all future
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, Array.Empty<RecurringTaskStateOverrideDocument>(),
            from, to, utcNow);

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
        // Arrange — only Feb 16 in range; utcNow is Feb 15 noon → Feb 16 is future
        var config = CreateConfig();
        var from = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 2, 17, 0, 0, 0, DateTimeKind.Utc);
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, Array.Empty<RecurringTaskStateOverrideDocument>(),
            from, to, UtcNow);

        // Assert
        result.Should().HaveCount(1);
        result[0].RecurrenceDateAndTime.Should().Be(Feb16At9);
        result[0].State.Should().Be(TaskState.Scheduled);
    }

    [Fact]
    public void ComputeInstances_MostRecentPastOccurrence_NoOverride_ReturnsOnDeck()
    {
        // Arrange — only Feb 15 at 09:00; utcNow is Feb 15 at noon (past)
        var config = CreateConfig();
        var from = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc);
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, Array.Empty<RecurringTaskStateOverrideDocument>(),
            from, to, UtcNow);

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
        var from = new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc);
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, Array.Empty<RecurringTaskStateOverrideDocument>(),
            from, to, UtcNow);

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
        var from = new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc);
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, Array.Empty<RecurringTaskStateOverrideDocument>(),
            from, to, UtcNow);

        // Assert
        var feb14Instance = result.Single(r => r.RecurrenceDateAndTime == Feb14At9);
        feb14Instance.State.Should().Be(TaskState.Skipped);
    }

    [Fact]
    public void ComputeInstances_InProgressOverride_NoNewerActive_ReturnsInProgress()
    {
        // Arrange — only Feb 14 occurrence with InProgress override; it's the only instance
        var config = CreateConfig();
        var from = new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc);
        var overrides = new[] { CreateOverride("config-1", Feb14At9, TaskState.InProgress) };
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, overrides, from, to, UtcNow);

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
        var from = new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc);
        var overrides = new[] { CreateOverride("config-1", Feb14At9, TaskState.InProgress) };
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, overrides, from, to, UtcNow);

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
        var from = new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc);
        var overrides = new[] { CreateOverride("config-1", Feb15At9, TaskState.Completed) };
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, overrides, from, to, UtcNow);

        // Assert
        var feb15 = result.Single(r => r.RecurrenceDateAndTime == Feb15At9);
        feb15.State.Should().Be(TaskState.Completed, "terminal state is always respected");
    }

    [Fact]
    public void ComputeInstances_SkippedOverride_AlwaysRespected()
    {
        // Arrange — Feb 15 has Skipped override; even as most-recent-past it should stay Skipped
        var config = CreateConfig();
        var from = new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc);
        var overrides = new[] { CreateOverride("config-1", Feb15At9, TaskState.Skipped) };
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, overrides, from, to, UtcNow);

        // Assert
        var feb15 = result.Single(r => r.RecurrenceDateAndTime == Feb15At9);
        feb15.State.Should().Be(TaskState.Skipped, "terminal Skipped override is always respected");
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
        var from = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc);
        var overrides = new[] { CreateOverride("config-1", Feb15At9, TaskState.OnDeck) };
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, overrides, from, to, UtcNow);

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
        var from = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc);
        var overrides = new[] { CreateOverride("config-1", Feb15At9, TaskState.Scheduled) };
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, overrides, from, to, UtcNow);

        // Assert
        result.Should().HaveCount(1);
        result[0].RecurrenceDateAndTime.Should().Be(Feb15At9);
        result[0].State.Should().Be(TaskState.Scheduled, "unexpected stored state is passed through as-is");
    }

    [Fact]
    public void ComputeInstances_UnexpectedStoredState_DoesNotConsumeActiveCandidate()
    {
        // Arrange — Critical contract: the fallback branch must NOT set activeCandidate.
        // If it did, the next older occurrence would become Skipped instead of OnDeck.
        //
        // Occurrences: Feb 15 and Feb 14 (both past; utcNow = Feb 15 noon).
        // Feb 15 has an unexpected OnDeck override (pass-through, no activeCandidate set).
        // Feb 14 has no override — since activeCandidate is still null, it should become OnDeck.
        var config = CreateConfig();
        var from = new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc);
        var overrides = new[] { CreateOverride("config-1", Feb15At9, TaskState.OnDeck) };
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, overrides, from, to, UtcNow);

        // Assert — both instances are present
        result.Should().HaveCount(2);
        var feb15 = result.Single(r => r.RecurrenceDateAndTime == Feb15At9);
        feb15.State.Should().Be(TaskState.OnDeck, "unexpected state passed through from override");

        var feb14 = result.Single(r => r.RecurrenceDateAndTime == Feb14At9);
        feb14.State.Should().Be(TaskState.OnDeck,
            "fallback did not consume activeCandidate, so Feb 14 correctly becomes OnDeck");
    }

    #endregion

    #region ComputeInstances — Instance Properties (AC8, AC9)

    [Fact]
    public void ComputeInstances_TextDerivedFromConfig()
    {
        // Arrange
        var config = CreateConfig(text: "Daily standup");
        var from = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc);
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, Array.Empty<RecurringTaskStateOverrideDocument>(),
            from, to, UtcNow);

        // Assert
        result.Should().HaveCount(1);
        result[0].Text.Should().Be("Daily standup");
    }

    [Fact]
    public void ComputeInstances_InstanceIdMatchesCompositeKeyFormat()
    {
        // Arrange — config id "abc-123", occurrence at Feb 15 09:00 UTC
        var config = CreateConfig(id: "abc-123");
        var from = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc);
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, Array.Empty<RecurringTaskStateOverrideDocument>(),
            from, to, UtcNow);

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
        var from = new DateTime(2026, 2, 6, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 2, 9, 0, 0, 0, DateTimeKind.Utc);
        var service = CreateService();

        // Act
        var result = service.ComputeInstances(
            new[] { config }, Array.Empty<RecurringTaskStateOverrideDocument>(),
            from, to, UtcNow);

        // Assert
        result.Should().BeEmpty("no Monday falls in the Fri-Sun range");
    }

    [Fact]
    public void ComputeInstances_SameInputsDifferentCallTimes_ReturnsSameResult()
    {
        // Arrange — determinism: same utcNow produces identical output (AC10)
        var config = CreateConfig();
        var overrides = new[] { CreateOverride("config-1", Feb14At9, TaskState.Completed) };
        var service = CreateService();
        var fixedUtcNow = new DateTime(2026, 2, 15, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var result1 = service.ComputeInstances(new[] { config }, overrides, RangeFrom, RangeTo, fixedUtcNow);
        var result2 = service.ComputeInstances(new[] { config }, overrides, RangeFrom, RangeTo, fixedUtcNow);

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
            RangeFrom, RangeTo, UtcNow);

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
        var from = new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 2, 17, 0, 0, 0, DateTimeKind.Utc);
        var mockRepo = new Mock<IRecurringTaskRepository>(MockBehavior.Strict);

        mockRepo.Setup(r => r.GetAllConfigsAsync(TestUserId))
                .ReturnsAsync(Array.Empty<RecurringTaskConfigDocument>());
        mockRepo.Setup(r => r.GetStateOverridesForDateRangeAsync(TestUserId, from, to))
                .ReturnsAsync(Array.Empty<RecurringTaskStateOverrideDocument>());

        var service = CreateService(mockRepo.Object);

        // Act
        await service.GetComputedInstancesAsync(TestUserId, from, to);

        // Assert — exactly these two calls, no others (MockBehavior.Strict enforces this)
        mockRepo.Verify(r => r.GetAllConfigsAsync(TestUserId), Times.Once);
        mockRepo.Verify(r => r.GetStateOverridesForDateRangeAsync(TestUserId, from, to), Times.Once);
        mockRepo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetComputedInstancesAsync_DateRangeExceeds365Days_ThrowsArgumentException()
    {
        // Arrange
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddDays(366); // 366 days > 365 limit
        var service = CreateService();

        // Act
        var act = () => service.GetComputedInstancesAsync(TestUserId, from, to);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*365 days*");
    }

    [Fact]
    public async Task GetComputedInstancesAsync_NonUtcFromDate_ThrowsArgumentException()
    {
        // Arrange — from is Local kind
        var from = new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Local);
        var to = new DateTime(2026, 2, 17, 0, 0, 0, DateTimeKind.Utc);
        var service = CreateService();

        // Act
        var act = () => service.GetComputedInstancesAsync(TestUserId, from, to);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*UTC*");
    }

    [Fact]
    public async Task GetComputedInstancesAsync_NonUtcToDate_ThrowsArgumentException()
    {
        // Arrange — to is Unspecified kind
        var from = new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 2, 17, 0, 0, 0, DateTimeKind.Unspecified);
        var service = CreateService();

        // Act
        var act = () => service.GetComputedInstancesAsync(TestUserId, from, to);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*UTC*");
    }

    [Fact]
    public async Task GetComputedInstancesAsync_ReturnsComputedInstances()
    {
        // Arrange
        var from = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc);
        var config = CreateConfig();
        var mockRepo = new Mock<IRecurringTaskRepository>();

        mockRepo.Setup(r => r.GetAllConfigsAsync(TestUserId))
                .ReturnsAsync(new[] { config });
        mockRepo.Setup(r => r.GetStateOverridesForDateRangeAsync(TestUserId, from, to))
                .ReturnsAsync(Array.Empty<RecurringTaskStateOverrideDocument>());

        var service = CreateService(mockRepo.Object);

        // Act
        var result = await service.GetComputedInstancesAsync(TestUserId, from, to);

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
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddDays(365);
        var mockRepo = new Mock<IRecurringTaskRepository>();

        mockRepo.Setup(r => r.GetAllConfigsAsync(TestUserId))
                .ReturnsAsync(Array.Empty<RecurringTaskConfigDocument>());
        mockRepo.Setup(r => r.GetStateOverridesForDateRangeAsync(TestUserId, from, to))
                .ReturnsAsync(Array.Empty<RecurringTaskStateOverrideDocument>());

        var service = CreateService(mockRepo.Object);

        // Act
        var act = () => service.GetComputedInstancesAsync(TestUserId, from, to);

        // Assert
        await act.Should().NotThrowAsync("exactly 365 days is within the allowed limit");
    }

    #endregion
}
