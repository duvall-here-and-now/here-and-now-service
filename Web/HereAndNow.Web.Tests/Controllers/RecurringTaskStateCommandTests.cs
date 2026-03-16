using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using HereAndNowService.Commands;
using HereAndNowService.Controllers;
using HereAndNowService.DTOs;
using HereAndNowService.Models;
using HereAndNowService.Models.Exceptions;
using HereAndNowService.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace HereAndNow.Web.Tests.Controllers;

/// <summary>
/// Controller-level tests for recurring task state commands (Story 9.4).
/// Tests payload validation, deserialization, error mapping, and service delegation.
/// </summary>
public class RecurringTaskStateCommandTests
{
    private readonly Mock<ITaskService> _mockTaskService;
    private readonly Mock<ITaskReminderService> _mockReminderService;
    private readonly Mock<IRecurringTaskService> _mockRecurringTaskService;
    private readonly Mock<ILogger<CommandsController>> _mockLogger;
    private readonly CommandsController _controller;
    private const string TestUserId = "auth0|test-user-123";

    public RecurringTaskStateCommandTests()
    {
        _mockTaskService = new Mock<ITaskService>();
        _mockReminderService = new Mock<ITaskReminderService>();
        _mockRecurringTaskService = new Mock<IRecurringTaskService>();
        _mockLogger = new Mock<ILogger<CommandsController>>();
        _controller = new CommandsController(
            _mockTaskService.Object,
            _mockReminderService.Object,
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

    private static CommandRequest CreateCommandRequest(string command, object payload)
    {
        var payloadJson = JsonSerializer.Serialize(payload);
        var payloadElement = JsonSerializer.Deserialize<JsonElement>(payloadJson);
        return new CommandRequest { Command = command, Payload = payloadElement };
    }

    #region StartRecurringTask Command Tests

    [Fact]
    public async Task StartRecurringTask_Success_Returns200()
    {
        var configId = Guid.NewGuid().ToString();
        var recurrenceDate = DateTime.UtcNow.AddHours(-1);
        var request = CreateCommandRequest("StartRecurringTask", new
        {
            recurringTaskConfigId = configId,
            recurrenceDateAndTime = recurrenceDate
        });

        _mockRecurringTaskService
            .Setup(s => s.StartRecurringTaskAsync(TestUserId, configId.ToLowerInvariant(), recurrenceDate))
            .Returns(Task.CompletedTask);

        var result = await _controller.ExecuteCommand(request);

        result.Should().BeOfType<OkResult>();
    }

    [Fact]
    public async Task StartRecurringTask_MissingConfigId_Returns400()
    {
        var request = CreateCommandRequest("StartRecurringTask", new
        {
            recurringTaskConfigId = "",
            recurrenceDateAndTime = DateTime.UtcNow
        });

        var result = await _controller.ExecuteCommand(request);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = badRequest.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        error.Error.Code.Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task StartRecurringTask_InvalidGuid_Returns400()
    {
        var request = CreateCommandRequest("StartRecurringTask", new
        {
            recurringTaskConfigId = "not-a-guid",
            recurrenceDateAndTime = DateTime.UtcNow
        });

        var result = await _controller.ExecuteCommand(request);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = badRequest.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        error.Error.Code.Should().Be("VALIDATION_ERROR");
        error.Error.Message.Should().Contain("GUID");
    }

    [Fact]
    public async Task StartRecurringTask_ConfigNotFound_Returns404()
    {
        var configId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("StartRecurringTask", new
        {
            recurringTaskConfigId = configId,
            recurrenceDateAndTime = DateTime.UtcNow
        });

        _mockRecurringTaskService
            .Setup(s => s.StartRecurringTaskAsync(TestUserId, It.IsAny<string>(), It.IsAny<DateTime>()))
            .ThrowsAsync(new RecurringTaskConfigNotFoundException(configId));

        var result = await _controller.ExecuteCommand(request);

        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var error = notFoundResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        error.Error.Code.Should().Be("RECURRING_TASK_CONFIG_NOT_FOUND");
    }

    [Fact]
    public async Task StartRecurringTask_InvalidTransition_Returns400()
    {
        var configId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("StartRecurringTask", new
        {
            recurringTaskConfigId = configId,
            recurrenceDateAndTime = DateTime.UtcNow
        });

        _mockRecurringTaskService
            .Setup(s => s.StartRecurringTaskAsync(TestUserId, It.IsAny<string>(), It.IsAny<DateTime>()))
            .ThrowsAsync(new InvalidStateTransitionException("id", "Scheduled", "StartRecurringTask"));

        var result = await _controller.ExecuteCommand(request);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = badRequest.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        error.Error.Code.Should().Be("INVALID_STATE_TRANSITION");
    }

    [Fact]
    public async Task StartRecurringTask_NormalizesGuidToLowercase()
    {
        var configId = Guid.NewGuid().ToString().ToUpperInvariant();
        var recurrenceDate = DateTime.UtcNow;
        var request = CreateCommandRequest("StartRecurringTask", new
        {
            recurringTaskConfigId = configId,
            recurrenceDateAndTime = recurrenceDate
        });

        _mockRecurringTaskService
            .Setup(s => s.StartRecurringTaskAsync(TestUserId, configId.ToLowerInvariant(), recurrenceDate))
            .Returns(Task.CompletedTask);

        await _controller.ExecuteCommand(request);

        _mockRecurringTaskService.Verify(
            s => s.StartRecurringTaskAsync(TestUserId, configId.ToLowerInvariant(), It.IsAny<DateTime>()),
            Times.Once);
    }

    #endregion

    #region RevertRecurringTaskToOnDeck Command Tests

    [Fact]
    public async Task RevertToOnDeck_Success_Returns200()
    {
        var configId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("RevertRecurringTaskToOnDeck", new
        {
            recurringTaskConfigId = configId,
            recurrenceDateAndTime = DateTime.UtcNow
        });

        _mockRecurringTaskService
            .Setup(s => s.RevertRecurringTaskToOnDeckAsync(TestUserId, configId.ToLowerInvariant(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.ExecuteCommand(request);

        result.Should().BeOfType<OkResult>();
    }

    [Fact]
    public async Task RevertToOnDeck_InvalidTransition_Returns400()
    {
        var configId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("RevertRecurringTaskToOnDeck", new
        {
            recurringTaskConfigId = configId,
            recurrenceDateAndTime = DateTime.UtcNow
        });

        _mockRecurringTaskService
            .Setup(s => s.RevertRecurringTaskToOnDeckAsync(TestUserId, It.IsAny<string>(), It.IsAny<DateTime>()))
            .ThrowsAsync(new InvalidStateTransitionException("id", "OnDeck", "RevertRecurringTaskToOnDeck"));

        var result = await _controller.ExecuteCommand(request);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = badRequest.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        error.Error.Code.Should().Be("INVALID_STATE_TRANSITION");
    }

    #endregion

    #region CompleteRecurringTask Command Tests

    [Fact]
    public async Task CompleteRecurringTask_Success_Returns200()
    {
        var configId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("CompleteRecurringTask", new
        {
            recurringTaskConfigId = configId,
            recurrenceDateAndTime = DateTime.UtcNow
        });

        _mockRecurringTaskService
            .Setup(s => s.CompleteRecurringTaskAsync(TestUserId, configId.ToLowerInvariant(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.ExecuteCommand(request);

        result.Should().BeOfType<OkResult>();
    }

    [Fact]
    public async Task CompleteRecurringTask_ConfigNotFound_Returns404()
    {
        var configId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("CompleteRecurringTask", new
        {
            recurringTaskConfigId = configId,
            recurrenceDateAndTime = DateTime.UtcNow
        });

        _mockRecurringTaskService
            .Setup(s => s.CompleteRecurringTaskAsync(TestUserId, It.IsAny<string>(), It.IsAny<DateTime>()))
            .ThrowsAsync(new RecurringTaskConfigNotFoundException(configId));

        var result = await _controller.ExecuteCommand(request);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region SkipRecurringTask Command Tests

    [Fact]
    public async Task SkipRecurringTask_Success_Returns200()
    {
        var configId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("SkipRecurringTask", new
        {
            recurringTaskConfigId = configId,
            recurrenceDateAndTime = DateTime.UtcNow
        });

        _mockRecurringTaskService
            .Setup(s => s.SkipRecurringTaskAsync(TestUserId, configId.ToLowerInvariant(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.ExecuteCommand(request);

        result.Should().BeOfType<OkResult>();
    }

    [Fact]
    public async Task SkipRecurringTask_InvalidTransition_Returns400()
    {
        var configId = Guid.NewGuid().ToString();
        var request = CreateCommandRequest("SkipRecurringTask", new
        {
            recurringTaskConfigId = configId,
            recurrenceDateAndTime = DateTime.UtcNow
        });

        _mockRecurringTaskService
            .Setup(s => s.SkipRecurringTaskAsync(TestUserId, It.IsAny<string>(), It.IsAny<DateTime>()))
            .ThrowsAsync(new InvalidStateTransitionException("id", "Scheduled", "SkipRecurringTask"));

        var result = await _controller.ExecuteCommand(request);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = badRequest.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        error.Error.Code.Should().Be("INVALID_STATE_TRANSITION");
    }

    [Fact]
    public async Task SkipRecurringTask_MissingConfigId_Returns400()
    {
        var request = CreateCommandRequest("SkipRecurringTask", new
        {
            recurrenceDateAndTime = DateTime.UtcNow
        });

        var result = await _controller.ExecuteCommand(request);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = badRequest.Value.Should().BeOfType<ErrorResponseDto>().Subject;
        error.Error.Code.Should().Be("VALIDATION_ERROR");
    }

    #endregion
}
