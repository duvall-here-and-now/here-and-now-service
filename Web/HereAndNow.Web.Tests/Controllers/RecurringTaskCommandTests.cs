using FluentAssertions;
using HereAndNowService.Models;
using HereAndNowService.Models.Exceptions;
using HereAndNowService.Repositories;
using HereAndNowService.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace HereAndNow.Web.Tests.Controllers;

/// <summary>
/// Tests for RecurringTaskService CRUD methods (Story 9.3).
/// Tests service logic with mocked IRecurringTaskRepository.
/// </summary>
public class RecurringTaskCommandTests
{
    private const string TestUserId = "auth0|test-user-123";
    private const string ValidDailyRrule = "FREQ=DAILY;BYHOUR=7;BYMINUTE=0;BYSECOND=0";
    private const string ValidWeeklyRrule = "FREQ=WEEKLY;BYDAY=MO,WE,FR";
    private const string ValidMonthlyRrule = "FREQ=MONTHLY;BYMONTHDAY=1";
    private const string ValidYearlyRrule = "FREQ=YEARLY;BYMONTH=1;BYMONTHDAY=1";
    private const string ValidHourlyRrule = "FREQ=HOURLY;INTERVAL=2";
    private const string MinutelyRrule = "FREQ=MINUTELY;INTERVAL=5";
    private const string SecondlyRrule = "FREQ=SECONDLY;INTERVAL=30";
    private const string InvalidFormatRrule = "NOT_A_VALID_RRULE";

    private static readonly DateTime TestStartDate = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IRecurringTaskRepository> _mockRepo;
    private readonly RecurringTaskService _service;

    public RecurringTaskCommandTests()
    {
        _mockRepo = new Mock<IRecurringTaskRepository>();
        var logger = Mock.Of<ILogger<RecurringTaskService>>();
        _service = new RecurringTaskService(_mockRepo.Object, logger);
    }

    #region CreateConfigAsync Tests (AC: #1, #2, #3, #4)

    [Fact]
    public async Task CreateConfigAsync_ValidInput_CreatesConfig()
    {
        // Arrange (AC: #1)
        var configId = Guid.NewGuid().ToString();
        _mockRepo
            .Setup(r => r.CreateConfigAsync(It.IsAny<RecurringTaskConfigDocument>()))
            .ReturnsAsync((RecurringTaskConfigDocument c) => c);

        // Act
        var result = await _service.CreateConfigAsync(
            TestUserId, configId, "Daily standup", ValidDailyRrule, TestStartDate);

        // Assert
        result.Id.Should().Be(configId);
        result.Text.Should().Be("Daily standup");
        result.Rrule.Should().Be(ValidDailyRrule);
        result.StartDateAndTime.Should().Be(TestStartDate);
        _mockRepo.Verify(r => r.CreateConfigAsync(It.IsAny<RecurringTaskConfigDocument>()), Times.Once);
    }

    [Fact]
    public async Task CreateConfigAsync_SetsUserIdAndCreatedAt()
    {
        // Arrange (AC: #1)
        var configId = Guid.NewGuid().ToString();
        RecurringTaskConfigDocument? captured = null;
        _mockRepo
            .Setup(r => r.CreateConfigAsync(It.IsAny<RecurringTaskConfigDocument>()))
            .Callback<RecurringTaskConfigDocument>(c => captured = c)
            .ReturnsAsync((RecurringTaskConfigDocument c) => c);

        // Act
        var beforeCreate = DateTime.UtcNow;
        var result = await _service.CreateConfigAsync(
            TestUserId, configId, "Test task", ValidDailyRrule, TestStartDate);
        var afterCreate = DateTime.UtcNow;

        // Assert
        captured.Should().NotBeNull();
        captured!.UserId.Should().Be(TestUserId);
        captured.CreatedAt.Should().BeOnOrAfter(beforeCreate).And.BeOnOrBefore(afterCreate);
    }

    [Fact]
    public async Task CreateConfigAsync_StoresRruleWithoutPrefix()
    {
        // Arrange (AC: #4) — RRULE should be stored exactly as provided, no "RRULE:" prefix
        var configId = Guid.NewGuid().ToString();
        _mockRepo
            .Setup(r => r.CreateConfigAsync(It.IsAny<RecurringTaskConfigDocument>()))
            .ReturnsAsync((RecurringTaskConfigDocument c) => c);

        // Act
        var result = await _service.CreateConfigAsync(
            TestUserId, configId, "Test", ValidDailyRrule, TestStartDate);

        // Assert
        result.Rrule.Should().Be(ValidDailyRrule);
        result.Rrule.Should().NotStartWith("RRULE:");
    }

    [Fact]
    public async Task CreateConfigAsync_InvalidRrule_ThrowsInvalidRecurrenceRuleException()
    {
        // Arrange (AC: #2)
        var configId = Guid.NewGuid().ToString();

        // Act
        var act = () => _service.CreateConfigAsync(
            TestUserId, configId, "Test", InvalidFormatRrule, TestStartDate);

        // Assert
        await act.Should().ThrowAsync<InvalidRecurrenceRuleException>()
            .Where(e => e.RecurrenceRule == InvalidFormatRrule);
    }

    [Fact]
    public async Task CreateConfigAsync_SecondlyFrequency_ThrowsInvalidRecurrenceRuleException()
    {
        // Arrange (AC: #2)
        var configId = Guid.NewGuid().ToString();

        // Act
        var act = () => _service.CreateConfigAsync(
            TestUserId, configId, "Test", SecondlyRrule, TestStartDate);

        // Assert
        await act.Should().ThrowAsync<InvalidRecurrenceRuleException>()
            .Where(e => e.RecurrenceRule == SecondlyRrule);
    }

    [Fact]
    public async Task CreateConfigAsync_MinutelyFrequency_ThrowsInvalidRecurrenceRuleException()
    {
        // Arrange (AC: #2)
        var configId = Guid.NewGuid().ToString();

        // Act
        var act = () => _service.CreateConfigAsync(
            TestUserId, configId, "Test", MinutelyRrule, TestStartDate);

        // Assert
        await act.Should().ThrowAsync<InvalidRecurrenceRuleException>()
            .Where(e => e.RecurrenceRule == MinutelyRrule);
    }

    [Fact]
    public async Task CreateConfigAsync_HourlyFrequency_Succeeds()
    {
        // Arrange (AC: #3)
        var configId = Guid.NewGuid().ToString();
        _mockRepo
            .Setup(r => r.CreateConfigAsync(It.IsAny<RecurringTaskConfigDocument>()))
            .ReturnsAsync((RecurringTaskConfigDocument c) => c);

        // Act
        var result = await _service.CreateConfigAsync(
            TestUserId, configId, "Hourly check", ValidHourlyRrule, TestStartDate);

        // Assert
        result.Rrule.Should().Be(ValidHourlyRrule);
    }

    [Fact]
    public async Task CreateConfigAsync_DailyFrequency_Succeeds()
    {
        // Arrange (AC: #3)
        var configId = Guid.NewGuid().ToString();
        _mockRepo
            .Setup(r => r.CreateConfigAsync(It.IsAny<RecurringTaskConfigDocument>()))
            .ReturnsAsync((RecurringTaskConfigDocument c) => c);

        // Act
        var result = await _service.CreateConfigAsync(
            TestUserId, configId, "Daily task", ValidDailyRrule, TestStartDate);

        // Assert
        result.Rrule.Should().Be(ValidDailyRrule);
    }

    [Fact]
    public async Task CreateConfigAsync_WeeklyFrequency_Succeeds()
    {
        // Arrange (AC: #3)
        var configId = Guid.NewGuid().ToString();
        _mockRepo
            .Setup(r => r.CreateConfigAsync(It.IsAny<RecurringTaskConfigDocument>()))
            .ReturnsAsync((RecurringTaskConfigDocument c) => c);

        // Act
        var result = await _service.CreateConfigAsync(
            TestUserId, configId, "Weekly sync", ValidWeeklyRrule, TestStartDate);

        // Assert
        result.Rrule.Should().Be(ValidWeeklyRrule);
    }

    [Fact]
    public async Task CreateConfigAsync_MonthlyFrequency_Succeeds()
    {
        // Arrange (AC: #3)
        var configId = Guid.NewGuid().ToString();
        _mockRepo
            .Setup(r => r.CreateConfigAsync(It.IsAny<RecurringTaskConfigDocument>()))
            .ReturnsAsync((RecurringTaskConfigDocument c) => c);

        // Act
        var result = await _service.CreateConfigAsync(
            TestUserId, configId, "Monthly review", ValidMonthlyRrule, TestStartDate);

        // Assert
        result.Rrule.Should().Be(ValidMonthlyRrule);
    }

    [Fact]
    public async Task CreateConfigAsync_YearlyFrequency_Succeeds()
    {
        // Arrange (AC: #3)
        var configId = Guid.NewGuid().ToString();
        _mockRepo
            .Setup(r => r.CreateConfigAsync(It.IsAny<RecurringTaskConfigDocument>()))
            .ReturnsAsync((RecurringTaskConfigDocument c) => c);

        // Act
        var result = await _service.CreateConfigAsync(
            TestUserId, configId, "Annual review", ValidYearlyRrule, TestStartDate);

        // Assert
        result.Rrule.Should().Be(ValidYearlyRrule);
    }

    [Fact]
    public async Task CreateConfigAsync_DuplicateId_ThrowsRecurringTaskConfigAlreadyExistsException()
    {
        // Arrange — simulate Cosmos DB 409 Conflict on duplicate document ID
        var configId = Guid.NewGuid().ToString();

        _mockRepo
            .Setup(r => r.CreateConfigAsync(It.IsAny<RecurringTaskConfigDocument>()))
            .ThrowsAsync(new Microsoft.Azure.Cosmos.CosmosException(
                "Conflict", System.Net.HttpStatusCode.Conflict, 0, "", 0));

        // Act
        var act = () => _service.CreateConfigAsync(
            TestUserId, configId, "Duplicate", ValidDailyRrule, TestStartDate);

        // Assert
        await act.Should().ThrowAsync<RecurringTaskConfigAlreadyExistsException>()
            .Where(e => e.ConfigId == configId);
    }

    [Fact]
    public async Task CreateConfigAsync_NonUtcStartDateAndTime_ThrowsArgumentException()
    {
        // Arrange — Local kind DateTime should be rejected at the service layer
        var configId = Guid.NewGuid().ToString();
        var localDateTime = new DateTime(2026, 3, 15, 9, 0, 0, DateTimeKind.Local);

        // Act
        var act = () => _service.CreateConfigAsync(
            TestUserId, configId, "Test", ValidDailyRrule, localDateTime);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("startDateAndTime");
    }

    #endregion

    #region UpdateConfigAsync Tests (AC: #5, #6, #8)

    [Fact]
    public async Task UpdateConfigAsync_ValidInput_UpdatesConfig()
    {
        // Arrange (AC: #5)
        var configId = Guid.NewGuid().ToString();
        var existingConfig = new RecurringTaskConfigDocument
        {
            Id = configId,
            UserId = TestUserId,
            Text = "Old text",
            Rrule = ValidDailyRrule,
            StartDateAndTime = TestStartDate,
            CreatedAt = DateTime.UtcNow.AddDays(-7)
        };

        _mockRepo.Setup(r => r.GetConfigByIdAsync(TestUserId, configId))
            .ReturnsAsync(existingConfig);
        _mockRepo.Setup(r => r.UpdateConfigAsync(It.IsAny<RecurringTaskConfigDocument>()))
            .ReturnsAsync((RecurringTaskConfigDocument c) => c);

        var newStartDate = TestStartDate.AddHours(1);

        // Act
        var result = await _service.UpdateConfigAsync(
            TestUserId, configId, "Updated text", ValidWeeklyRrule, newStartDate);

        // Assert
        result.Text.Should().Be("Updated text");
        result.Rrule.Should().Be(ValidWeeklyRrule);
        result.StartDateAndTime.Should().Be(newStartDate);
        _mockRepo.Verify(r => r.UpdateConfigAsync(It.IsAny<RecurringTaskConfigDocument>()), Times.Once);
    }

    [Fact]
    public async Task UpdateConfigAsync_InvalidRrule_ThrowsInvalidRecurrenceRuleException()
    {
        // Arrange (AC: #6)
        var configId = Guid.NewGuid().ToString();

        // Act — should throw before even querying the repo
        var act = () => _service.UpdateConfigAsync(
            TestUserId, configId, "Test", MinutelyRrule, TestStartDate);

        // Assert
        await act.Should().ThrowAsync<InvalidRecurrenceRuleException>();
        _mockRepo.Verify(r => r.GetConfigByIdAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateConfigAsync_ConfigNotFound_ThrowsNotFoundException()
    {
        // Arrange (AC: #8)
        var configId = Guid.NewGuid().ToString();
        _mockRepo.Setup(r => r.GetConfigByIdAsync(TestUserId, configId))
            .ReturnsAsync((RecurringTaskConfigDocument?)null);

        // Act
        var act = () => _service.UpdateConfigAsync(
            TestUserId, configId, "Test", ValidDailyRrule, TestStartDate);

        // Assert
        await act.Should().ThrowAsync<RecurringTaskConfigNotFoundException>()
            .Where(e => e.ConfigId == configId);
    }

    [Fact]
    public async Task UpdateConfigAsync_NonUtcStartDateAndTime_ThrowsArgumentException()
    {
        // Arrange — Unspecified kind DateTime should be rejected at the service layer
        var configId = Guid.NewGuid().ToString();
        var unspecifiedDateTime = new DateTime(2026, 3, 15, 9, 0, 0, DateTimeKind.Unspecified);

        // Act
        var act = () => _service.UpdateConfigAsync(
            TestUserId, configId, "Test", ValidDailyRrule, unspecifiedDateTime);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("startDateAndTime");
        _mockRepo.Verify(r => r.GetConfigByIdAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateConfigAsync_DoesNotOverwriteCreatedAt()
    {
        // Arrange (AC: #5) — CreatedAt must be preserved from the original config
        var configId = Guid.NewGuid().ToString();
        var originalCreatedAt = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var existingConfig = new RecurringTaskConfigDocument
        {
            Id = configId,
            UserId = TestUserId,
            Text = "Old text",
            Rrule = ValidDailyRrule,
            StartDateAndTime = TestStartDate,
            CreatedAt = originalCreatedAt
        };

        _mockRepo.Setup(r => r.GetConfigByIdAsync(TestUserId, configId))
            .ReturnsAsync(existingConfig);
        _mockRepo.Setup(r => r.UpdateConfigAsync(It.IsAny<RecurringTaskConfigDocument>()))
            .ReturnsAsync((RecurringTaskConfigDocument c) => c);

        // Act
        var result = await _service.UpdateConfigAsync(
            TestUserId, configId, "New text", ValidWeeklyRrule, TestStartDate.AddDays(1));

        // Assert
        result.CreatedAt.Should().Be(originalCreatedAt);
    }

    #endregion

    #region DeleteConfigAsync Tests (AC: #7, #8)

    [Fact]
    public async Task DeleteConfigAsync_ExistingConfig_Deletes()
    {
        // Arrange (AC: #7)
        var configId = Guid.NewGuid().ToString();
        _mockRepo.Setup(r => r.DeleteConfigWithOverridesAsync(TestUserId, configId))
            .Returns(Task.CompletedTask);

        // Act
        await _service.DeleteConfigAsync(TestUserId, configId);

        // Assert
        _mockRepo.Verify(r => r.DeleteConfigWithOverridesAsync(TestUserId, configId), Times.Once);
    }

    [Fact]
    public async Task DeleteConfigAsync_ConfigNotFound_ThrowsNotFoundException()
    {
        // Arrange (AC: #8)
        var configId = Guid.NewGuid().ToString();
        _mockRepo.Setup(r => r.DeleteConfigWithOverridesAsync(TestUserId, configId))
            .ThrowsAsync(new RecurringTaskConfigNotFoundException(configId));

        // Act
        var act = () => _service.DeleteConfigAsync(TestUserId, configId);

        // Assert
        await act.Should().ThrowAsync<RecurringTaskConfigNotFoundException>()
            .Where(e => e.ConfigId == configId);
    }

    #endregion

    #region ValidateRrule Tests (AC: #2, #3)

    [Theory]
    [InlineData("FREQ=HOURLY;INTERVAL=1")]
    [InlineData("FREQ=DAILY;BYHOUR=9;BYMINUTE=0;BYSECOND=0")]
    [InlineData("FREQ=WEEKLY;BYDAY=MO,WE,FR")]
    [InlineData("FREQ=MONTHLY;BYMONTHDAY=15")]
    [InlineData("FREQ=YEARLY;BYMONTH=6;BYMONTHDAY=1")]
    public async Task ValidateRrule_ValidFrequencies_DoesNotThrow(string rrule)
    {
        // Arrange (AC: #3) — these frequencies should all pass validation
        var configId = Guid.NewGuid().ToString();
        _mockRepo
            .Setup(r => r.CreateConfigAsync(It.IsAny<RecurringTaskConfigDocument>()))
            .ReturnsAsync((RecurringTaskConfigDocument c) => c);

        // Act — ValidateRrule is called internally by CreateConfigAsync
        var act = () => _service.CreateConfigAsync(
            TestUserId, configId, "Test", rrule, TestStartDate);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateRrule_InvalidFormat_ThrowsException()
    {
        // Arrange (AC: #2) — completely invalid RRULE string
        var configId = Guid.NewGuid().ToString();

        // Act
        var act = () => _service.CreateConfigAsync(
            TestUserId, configId, "Test", "GARBAGE_STRING", TestStartDate);

        // Assert
        await act.Should().ThrowAsync<InvalidRecurrenceRuleException>()
            .Where(e => e.RecurrenceRule == "GARBAGE_STRING");
    }

    #endregion
}
