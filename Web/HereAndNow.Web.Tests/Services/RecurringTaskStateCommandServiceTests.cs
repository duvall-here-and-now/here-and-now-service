using FluentAssertions;
using HereAndNowService.Models;
using HereAndNowService.Models.Exceptions;
using HereAndNowService.Repositories;
using HereAndNowService.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace HereAndNow.Web.Tests.Services;

/// <summary>
/// Tests for RecurringTaskService state command methods (Story 9.4).
/// Covers Start, Revert, Complete, Skip commands with state transition validation.
/// </summary>
public class RecurringTaskStateCommandServiceTests
{
    private const string TestUserId = "auth0|test-user-123";
    private const string ValidDailyRrule = "FREQ=DAILY;BYHOUR=9;BYMINUTE=0;BYSECOND=0";

    private static readonly DateTime UtcNow = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);

    // Past occurrence = OnDeck (most recent past, no override)
    private static readonly DateTime PastOccurrence = new DateTime(2026, 3, 15, 9, 0, 0, DateTimeKind.Utc);
    // Older past occurrence = would be Skipped if there's a more recent active
    private static readonly DateTime OlderPastOccurrence = new DateTime(2026, 3, 14, 9, 0, 0, DateTimeKind.Utc);
    // Future occurrence = Scheduled
    private static readonly DateTime FutureOccurrence = new DateTime(2026, 3, 16, 9, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime ConfigStartDate = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IRecurringTaskRepository> _mockRepo;
    private readonly RecurringTaskService _service;
    private readonly string _configId;

    public RecurringTaskStateCommandServiceTests()
    {
        _mockRepo = new Mock<IRecurringTaskRepository>();
        var logger = Mock.Of<ILogger<RecurringTaskService>>();
        _service = new RecurringTaskService(_mockRepo.Object, logger);
        _configId = Guid.NewGuid().ToString().ToLowerInvariant();
    }

    private RecurringTaskConfigDocument CreateConfig(string? configId = null)
    {
        return new RecurringTaskConfigDocument
        {
            Id = configId ?? _configId,
            UserId = TestUserId,
            Text = "Daily standup",
            Rrule = ValidDailyRrule,
            StartDateAndTime = ConfigStartDate,
            CreatedAt = ConfigStartDate
        };
    }

    private RecurringTaskStateOverrideDocument CreateOverride(
        string configId, DateTime recurrenceDateAndTime, string state)
    {
        return new RecurringTaskStateOverrideDocument
        {
            Id = RecurringTaskStateOverrideDocument.GenerateId(configId, recurrenceDateAndTime),
            UserId = TestUserId,
            ConfigId = configId,
            RecurrenceDateAndTime = recurrenceDateAndTime,
            State = state,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private void SetupConfigExists(RecurringTaskConfigDocument? config = null)
    {
        config ??= CreateConfig();
        _mockRepo.Setup(r => r.GetConfigByIdAsync(TestUserId, config.Id))
            .ReturnsAsync(config);
    }

    private void SetupOverrides(params RecurringTaskStateOverrideDocument[] overrides)
    {
        _mockRepo.Setup(r => r.GetStateOverridesForDateRangeAsync(
                TestUserId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(overrides.ToList());
    }

    private void SetupConfigNotFound()
    {
        _mockRepo.Setup(r => r.GetConfigByIdAsync(TestUserId, _configId))
            .ReturnsAsync((RecurringTaskConfigDocument?)null);
    }

    #region StartRecurringTask Tests (AC: 1)

    [Fact]
    public async Task StartRecurringTask_OnDeck_UpsertsInProgressOverride()
    {
        // Arrange — OnDeck instance (most recent past, no override)
        SetupConfigExists();
        SetupOverrides(); // no overrides → PastOccurrence will be OnDeck

        RecurringTaskStateOverrideDocument? captured = null;
        _mockRepo.Setup(r => r.UpsertStateOverrideAsync(It.IsAny<RecurringTaskStateOverrideDocument>()))
            .Callback<RecurringTaskStateOverrideDocument>(doc => captured = doc)
            .ReturnsAsync((RecurringTaskStateOverrideDocument doc) => doc);

        // Act
        await _service.StartRecurringTaskAsync(TestUserId, _configId, PastOccurrence);

        // Assert
        captured.Should().NotBeNull();
        captured!.State.Should().Be(TaskState.InProgress);
        captured.ConfigId.Should().Be(_configId);
        captured.RecurrenceDateAndTime.Should().Be(PastOccurrence);
        captured.UserId.Should().Be(TestUserId);
        captured.Id.Should().Be(RecurringTaskStateOverrideDocument.GenerateId(_configId, PastOccurrence));
    }

    [Fact]
    public async Task StartRecurringTask_InProgress_Rejects()
    {
        // Arrange — InProgress override exists
        SetupConfigExists();
        SetupOverrides(CreateOverride(_configId, PastOccurrence, TaskState.InProgress));

        // Act & Assert
        var act = () => _service.StartRecurringTaskAsync(TestUserId, _configId, PastOccurrence);
        await act.Should().ThrowAsync<InvalidStateTransitionException>()
            .Where(ex => ex.CurrentState == TaskState.InProgress &&
                         ex.AttemptedAction == "StartRecurringTask");
    }

    [Fact]
    public async Task StartRecurringTask_Scheduled_Rejects()
    {
        // Arrange — Future occurrence = Scheduled
        SetupConfigExists();
        SetupOverrides();

        // Act & Assert
        var act = () => _service.StartRecurringTaskAsync(TestUserId, _configId, FutureOccurrence);
        await act.Should().ThrowAsync<InvalidStateTransitionException>()
            .Where(ex => ex.CurrentState == TaskState.Scheduled);
    }

    [Fact]
    public async Task StartRecurringTask_Completed_Rejects()
    {
        // Arrange — Completed override exists
        SetupConfigExists();
        SetupOverrides(CreateOverride(_configId, PastOccurrence, TaskState.Completed));

        // Act & Assert
        var act = () => _service.StartRecurringTaskAsync(TestUserId, _configId, PastOccurrence);
        await act.Should().ThrowAsync<InvalidStateTransitionException>()
            .Where(ex => ex.CurrentState == TaskState.Completed);
    }

    [Fact]
    public async Task StartRecurringTask_ConfigNotFound_ThrowsNotFoundException()
    {
        // Arrange
        SetupConfigNotFound();
        SetupOverrides();

        // Act & Assert
        var act = () => _service.StartRecurringTaskAsync(TestUserId, _configId, PastOccurrence);
        await act.Should().ThrowAsync<RecurringTaskConfigNotFoundException>();
    }

    #endregion

    #region RevertRecurringTaskToOnDeck Tests (AC: 4)

    [Fact]
    public async Task RevertToOnDeck_InProgress_DeletesOverride()
    {
        // Arrange — InProgress override exists
        SetupConfigExists();
        SetupOverrides(CreateOverride(_configId, PastOccurrence, TaskState.InProgress));

        var expectedOverrideId = RecurringTaskStateOverrideDocument.GenerateId(_configId, PastOccurrence);

        // Act
        await _service.RevertRecurringTaskToOnDeckAsync(TestUserId, _configId, PastOccurrence);

        // Assert — delete was called (not upsert)
        _mockRepo.Verify(r => r.DeleteStateOverrideAsync(TestUserId, expectedOverrideId), Times.Once);
        _mockRepo.Verify(r => r.UpsertStateOverrideAsync(It.IsAny<RecurringTaskStateOverrideDocument>()), Times.Never);
    }

    [Fact]
    public async Task RevertToOnDeck_OnDeck_Rejects()
    {
        // Arrange — OnDeck (no override, most recent past)
        SetupConfigExists();
        SetupOverrides();

        // Act & Assert
        var act = () => _service.RevertRecurringTaskToOnDeckAsync(TestUserId, _configId, PastOccurrence);
        await act.Should().ThrowAsync<InvalidStateTransitionException>()
            .Where(ex => ex.CurrentState == TaskState.OnDeck &&
                         ex.AttemptedAction == "RevertRecurringTaskToOnDeck");
    }

    [Fact]
    public async Task RevertToOnDeck_Completed_Rejects()
    {
        // Arrange
        SetupConfigExists();
        SetupOverrides(CreateOverride(_configId, PastOccurrence, TaskState.Completed));

        // Act & Assert
        var act = () => _service.RevertRecurringTaskToOnDeckAsync(TestUserId, _configId, PastOccurrence);
        await act.Should().ThrowAsync<InvalidStateTransitionException>()
            .Where(ex => ex.CurrentState == TaskState.Completed);
    }

    #endregion

    #region CompleteRecurringTask Tests (AC: 2, 5, 6)

    [Fact]
    public async Task CompleteRecurringTask_OnDeck_UpsertsCompletedOverride()
    {
        // Arrange
        SetupConfigExists();
        SetupOverrides();

        RecurringTaskStateOverrideDocument? captured = null;
        _mockRepo.Setup(r => r.UpsertStateOverrideAsync(It.IsAny<RecurringTaskStateOverrideDocument>()))
            .Callback<RecurringTaskStateOverrideDocument>(doc => captured = doc)
            .ReturnsAsync((RecurringTaskStateOverrideDocument doc) => doc);

        // Act
        await _service.CompleteRecurringTaskAsync(TestUserId, _configId, PastOccurrence);

        // Assert
        captured.Should().NotBeNull();
        captured!.State.Should().Be(TaskState.Completed);
    }

    [Fact]
    public async Task CompleteRecurringTask_InProgress_UpsertsCompletedOverride()
    {
        // Arrange
        SetupConfigExists();
        SetupOverrides(CreateOverride(_configId, PastOccurrence, TaskState.InProgress));

        RecurringTaskStateOverrideDocument? captured = null;
        _mockRepo.Setup(r => r.UpsertStateOverrideAsync(It.IsAny<RecurringTaskStateOverrideDocument>()))
            .Callback<RecurringTaskStateOverrideDocument>(doc => captured = doc)
            .ReturnsAsync((RecurringTaskStateOverrideDocument doc) => doc);

        // Act
        await _service.CompleteRecurringTaskAsync(TestUserId, _configId, PastOccurrence);

        // Assert
        captured.Should().NotBeNull();
        captured!.State.Should().Be(TaskState.Completed);
    }

    [Fact]
    public async Task CompleteRecurringTask_Skipped_NoNewerActive_UpsertsCompleted()
    {
        // Arrange — Skipped instance with no newer active instance
        // Make the target occurrence the most recent past (no newer OnDeck/InProgress)
        SetupConfigExists();
        SetupOverrides(CreateOverride(_configId, PastOccurrence, TaskState.Skipped));

        RecurringTaskStateOverrideDocument? captured = null;
        _mockRepo.Setup(r => r.UpsertStateOverrideAsync(It.IsAny<RecurringTaskStateOverrideDocument>()))
            .Callback<RecurringTaskStateOverrideDocument>(doc => captured = doc)
            .ReturnsAsync((RecurringTaskStateOverrideDocument doc) => doc);

        // Act
        await _service.CompleteRecurringTaskAsync(TestUserId, _configId, PastOccurrence);

        // Assert
        captured.Should().NotBeNull();
        captured!.State.Should().Be(TaskState.Completed);
    }

    [Fact]
    public async Task CompleteRecurringTask_Skipped_NewerActiveExists_Rejects()
    {
        // Arrange — Skipped older instance, but newer instance is OnDeck (active)
        SetupConfigExists();
        SetupOverrides(CreateOverride(_configId, OlderPastOccurrence, TaskState.Skipped));
        // PastOccurrence will be OnDeck (no override, most recent past) = newer active exists

        // Act & Assert
        var act = () => _service.CompleteRecurringTaskAsync(TestUserId, _configId, OlderPastOccurrence);
        await act.Should().ThrowAsync<InvalidStateTransitionException>()
            .Where(ex => ex.AttemptedAction == "CompleteRecurringTask");
    }

    [Fact]
    public async Task CompleteRecurringTask_Scheduled_Rejects()
    {
        // Arrange
        SetupConfigExists();
        SetupOverrides();

        // Act & Assert
        var act = () => _service.CompleteRecurringTaskAsync(TestUserId, _configId, FutureOccurrence);
        await act.Should().ThrowAsync<InvalidStateTransitionException>()
            .Where(ex => ex.CurrentState == TaskState.Scheduled);
    }

    [Fact]
    public async Task CompleteRecurringTask_AlreadyCompleted_IsIdempotent()
    {
        // Arrange — Already Completed, should be a no-op
        SetupConfigExists();
        SetupOverrides(CreateOverride(_configId, PastOccurrence, TaskState.Completed));

        // Act
        await _service.CompleteRecurringTaskAsync(TestUserId, _configId, PastOccurrence);

        // Assert — no upsert should be called (idempotent)
        _mockRepo.Verify(r => r.UpsertStateOverrideAsync(It.IsAny<RecurringTaskStateOverrideDocument>()), Times.Never);
    }

    #endregion

    #region SkipRecurringTask Tests (AC: 3, 5, 6)

    [Fact]
    public async Task SkipRecurringTask_OnDeck_UpsertsSkippedOverride()
    {
        // Arrange
        SetupConfigExists();
        SetupOverrides();

        RecurringTaskStateOverrideDocument? captured = null;
        _mockRepo.Setup(r => r.UpsertStateOverrideAsync(It.IsAny<RecurringTaskStateOverrideDocument>()))
            .Callback<RecurringTaskStateOverrideDocument>(doc => captured = doc)
            .ReturnsAsync((RecurringTaskStateOverrideDocument doc) => doc);

        // Act
        await _service.SkipRecurringTaskAsync(TestUserId, _configId, PastOccurrence);

        // Assert
        captured.Should().NotBeNull();
        captured!.State.Should().Be(TaskState.Skipped);
    }

    [Fact]
    public async Task SkipRecurringTask_InProgress_UpsertsSkippedOverride()
    {
        // Arrange
        SetupConfigExists();
        SetupOverrides(CreateOverride(_configId, PastOccurrence, TaskState.InProgress));

        RecurringTaskStateOverrideDocument? captured = null;
        _mockRepo.Setup(r => r.UpsertStateOverrideAsync(It.IsAny<RecurringTaskStateOverrideDocument>()))
            .Callback<RecurringTaskStateOverrideDocument>(doc => captured = doc)
            .ReturnsAsync((RecurringTaskStateOverrideDocument doc) => doc);

        // Act
        await _service.SkipRecurringTaskAsync(TestUserId, _configId, PastOccurrence);

        // Assert
        captured.Should().NotBeNull();
        captured!.State.Should().Be(TaskState.Skipped);
    }

    [Fact]
    public async Task SkipRecurringTask_Completed_NoNewerActive_UpsertsSkipped()
    {
        // Arrange — Completed instance with no newer active
        SetupConfigExists();
        SetupOverrides(CreateOverride(_configId, PastOccurrence, TaskState.Completed));

        RecurringTaskStateOverrideDocument? captured = null;
        _mockRepo.Setup(r => r.UpsertStateOverrideAsync(It.IsAny<RecurringTaskStateOverrideDocument>()))
            .Callback<RecurringTaskStateOverrideDocument>(doc => captured = doc)
            .ReturnsAsync((RecurringTaskStateOverrideDocument doc) => doc);

        // Act
        await _service.SkipRecurringTaskAsync(TestUserId, _configId, PastOccurrence);

        // Assert
        captured.Should().NotBeNull();
        captured!.State.Should().Be(TaskState.Skipped);
    }

    [Fact]
    public async Task SkipRecurringTask_Completed_NewerActiveExists_Rejects()
    {
        // Arrange — Completed older instance, but newer instance is OnDeck
        SetupConfigExists();
        SetupOverrides(CreateOverride(_configId, OlderPastOccurrence, TaskState.Completed));

        // Act & Assert
        var act = () => _service.SkipRecurringTaskAsync(TestUserId, _configId, OlderPastOccurrence);
        await act.Should().ThrowAsync<InvalidStateTransitionException>()
            .Where(ex => ex.AttemptedAction == "SkipRecurringTask");
    }

    [Fact]
    public async Task SkipRecurringTask_Scheduled_Rejects()
    {
        // Arrange
        SetupConfigExists();
        SetupOverrides();

        // Act & Assert
        var act = () => _service.SkipRecurringTaskAsync(TestUserId, _configId, FutureOccurrence);
        await act.Should().ThrowAsync<InvalidStateTransitionException>()
            .Where(ex => ex.CurrentState == TaskState.Scheduled);
    }

    [Fact]
    public async Task SkipRecurringTask_AlreadySkipped_IsIdempotent()
    {
        // Arrange — Already Skipped, should be a no-op
        SetupConfigExists();
        SetupOverrides(CreateOverride(_configId, PastOccurrence, TaskState.Skipped));

        // Act
        await _service.SkipRecurringTaskAsync(TestUserId, _configId, PastOccurrence);

        // Assert — no upsert should be called (idempotent)
        _mockRepo.Verify(r => r.UpsertStateOverrideAsync(It.IsAny<RecurringTaskStateOverrideDocument>()), Times.Never);
    }

    #endregion

    #region Scheduled Instance Rejection Tests (AC: 8)

    [Fact]
    public async Task AllCommands_Scheduled_Reject()
    {
        // All four commands should reject Scheduled instances
        SetupConfigExists();
        SetupOverrides();

        var act1 = () => _service.StartRecurringTaskAsync(TestUserId, _configId, FutureOccurrence);
        var act2 = () => _service.RevertRecurringTaskToOnDeckAsync(TestUserId, _configId, FutureOccurrence);
        var act3 = () => _service.CompleteRecurringTaskAsync(TestUserId, _configId, FutureOccurrence);
        var act4 = () => _service.SkipRecurringTaskAsync(TestUserId, _configId, FutureOccurrence);

        await act1.Should().ThrowAsync<InvalidStateTransitionException>();
        await act2.Should().ThrowAsync<InvalidStateTransitionException>();
        await act3.Should().ThrowAsync<InvalidStateTransitionException>();
        await act4.Should().ThrowAsync<InvalidStateTransitionException>();
    }

    #endregion

    #region Atomic State Persistence Tests (AC: 10)

    [Fact]
    public async Task StartRecurringTask_UsesCorrectCompositeId()
    {
        // Verify the correct composite ID format is used
        SetupConfigExists();
        SetupOverrides();

        RecurringTaskStateOverrideDocument? captured = null;
        _mockRepo.Setup(r => r.UpsertStateOverrideAsync(It.IsAny<RecurringTaskStateOverrideDocument>()))
            .Callback<RecurringTaskStateOverrideDocument>(doc => captured = doc)
            .ReturnsAsync((RecurringTaskStateOverrideDocument doc) => doc);

        await _service.StartRecurringTaskAsync(TestUserId, _configId, PastOccurrence);

        var expectedId = RecurringTaskStateOverrideDocument.GenerateId(_configId, PastOccurrence);
        captured!.Id.Should().Be(expectedId);
    }

    [Fact]
    public async Task StartRecurringTask_SetsUpdatedAt()
    {
        // Verify UpdatedAt is set to current UTC time
        SetupConfigExists();
        SetupOverrides();

        RecurringTaskStateOverrideDocument? captured = null;
        _mockRepo.Setup(r => r.UpsertStateOverrideAsync(It.IsAny<RecurringTaskStateOverrideDocument>()))
            .Callback<RecurringTaskStateOverrideDocument>(doc => captured = doc)
            .ReturnsAsync((RecurringTaskStateOverrideDocument doc) => doc);

        var before = DateTime.UtcNow;
        await _service.StartRecurringTaskAsync(TestUserId, _configId, PastOccurrence);
        var after = DateTime.UtcNow;

        captured!.UpdatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    #endregion
}
