using System.Security.Claims;
using FluentAssertions;
using HereAndNowService.Controllers;
using HereAndNowService.DTOs;
using HereAndNowService.Models;
using HereAndNowService.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace HereAndNow.Web.Tests.Controllers;

/// <summary>
/// Unit tests for RecurringTasksController (Story 9.5).
/// Tests computed instance queries, date validation, and DTO mapping.
/// </summary>
public class RecurringTasksControllerTests
{
    private readonly Mock<IRecurringTaskService> _mockRecurringTaskService;
    private readonly Mock<ILogger<RecurringTasksController>> _mockLogger;
    private readonly RecurringTasksController _controller;
    private const string TestUserId = "auth0|test-user-123";

    public RecurringTasksControllerTests()
    {
        _mockRecurringTaskService = new Mock<IRecurringTaskService>();
        _mockLogger = new Mock<ILogger<RecurringTasksController>>();
        _controller = new RecurringTasksController(
            _mockRecurringTaskService.Object,
            _mockLogger.Object);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, TestUserId)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    private static RecurringTaskConfigDocument CreateTestConfig(string id = "config-1", string text = "Daily standup")
    {
        return new RecurringTaskConfigDocument
        {
            Id = id,
            UserId = "auth0|test-user-123",
            Text = text,
            Rrule = "FREQ=DAILY;BYHOUR=9",
            StartDateAndTime = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
    }

    #region GetInstances Tests

    [Fact]
    public async Task GetInstances_ValidDateRange_ReturnsComputedInstancesMappedToFlatDtos()
    {
        // Arrange
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 1, 31, 23, 59, 59, DateTimeKind.Utc);
        var config = CreateTestConfig();
        var instances = new List<RecurringTaskInstance>
        {
            new RecurringTaskInstance(config, new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc), "OnDeck"),
            new RecurringTaskInstance(config, new DateTime(2026, 1, 20, 9, 0, 0, DateTimeKind.Utc), "Scheduled")
        }.AsReadOnly();

        _mockRecurringTaskService
            .Setup(s => s.GetComputedInstancesAsync(TestUserId, from, to))
            .ReturnsAsync(instances);

        // Act
        var result = await _controller.GetInstances(from, to);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<RecurringTaskDto>>().Subject.ToList();
        dtos.Should().HaveCount(2);

        // Verify flat DTO structure (AC7)
        dtos[0].Id.Should().Be("config-1_2026-01-15T09:00:00Z");
        dtos[0].ConfigId.Should().Be("config-1");
        dtos[0].Text.Should().Be("Daily standup");
        dtos[0].RecurrenceDateAndTime.Should().Be(new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc));
        dtos[0].State.Should().Be("OnDeck");
        dtos[0].RecurrenceRule.Should().Be("FREQ=DAILY;BYHOUR=9");

        dtos[1].State.Should().Be("Scheduled");
    }

    [Fact]
    public async Task GetInstances_MissingFromParameter_Returns400ValidationError()
    {
        // Act
        var result = await _controller.GetInstances(null, DateTime.UtcNow);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("VALIDATION_ERROR");
        errorResponse.Error.Message.Should().Contain("'from'");
        errorResponse.Error.Message.Should().Contain("'to'");
    }

    [Fact]
    public async Task GetInstances_MissingToParameter_Returns400ValidationError()
    {
        // Act
        var result = await _controller.GetInstances(DateTime.UtcNow, null);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task GetInstances_DateRangeExceeds365Days_Returns400ValidationError()
    {
        // Arrange
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2027, 2, 1, 0, 0, 0, DateTimeKind.Utc); // > 365 days

        _mockRecurringTaskService
            .Setup(s => s.GetComputedInstancesAsync(TestUserId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ThrowsAsync(new ArgumentException("Date range cannot exceed 365 days (NFR43)"));

        // Act
        var result = await _controller.GetInstances(from, to);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("VALIDATION_ERROR");
        errorResponse.Error.Message.Should().Contain("365");
    }

    [Fact]
    public async Task GetInstances_EmptyResult_Returns200WithEmptyArray()
    {
        // Arrange
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc);

        _mockRecurringTaskService
            .Setup(s => s.GetComputedInstancesAsync(TestUserId, from, to))
            .ReturnsAsync(new List<RecurringTaskInstance>().AsReadOnly());

        // Act
        var result = await _controller.GetInstances(from, to);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<RecurringTaskDto>>().Subject.ToList();
        dtos.Should().BeEmpty();
    }

    [Fact]
    public async Task GetInstances_FlatDtoStructure_ContainsAllExpectedFields()
    {
        // Arrange — verify all 6 fields of the flat DTO
        var from = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc);
        var config = new RecurringTaskConfigDocument
        {
            Id = "my-config-id",
            UserId = TestUserId,
            Text = "Weekly review",
            Rrule = "FREQ=WEEKLY;BYDAY=MO",
            StartDateAndTime = new DateTime(2026, 1, 6, 10, 0, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var occurrence = new DateTime(2026, 3, 9, 10, 0, 0, DateTimeKind.Utc);
        var instance = new RecurringTaskInstance(config, occurrence, "Completed");

        _mockRecurringTaskService
            .Setup(s => s.GetComputedInstancesAsync(TestUserId, from, to))
            .ReturnsAsync(new List<RecurringTaskInstance> { instance }.AsReadOnly());

        // Act
        var result = await _controller.GetInstances(from, to);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<RecurringTaskDto>>().Subject.ToList();
        dtos.Should().HaveCount(1);

        var dto = dtos[0];
        dto.Id.Should().Be("my-config-id_2026-03-09T10:00:00Z");
        dto.ConfigId.Should().Be("my-config-id");
        dto.Text.Should().Be("Weekly review");
        dto.RecurrenceDateAndTime.Should().Be(occurrence);
        dto.State.Should().Be("Completed");
        dto.RecurrenceRule.Should().Be("FREQ=WEEKLY;BYDAY=MO");
    }

    [Fact]
    public async Task GetInstances_BothParametersMissing_Returns400ValidationError()
    {
        // Act
        var result = await _controller.GetInstances(null, null);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        errorResponse.Error.Code.Should().Be("VALIDATION_ERROR");
    }

    #endregion
}
