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

public class ReminderInstancesControllerTests
{
    private const string TestUserId = "auth0|test-user-123";

    private readonly Mock<IReminderInstanceService> _mockService;
    private readonly Mock<ILogger<ReminderInstancesController>> _mockLogger;
    private readonly ReminderInstancesController _controller;

    public ReminderInstancesControllerTests()
    {
        _mockService = new Mock<IReminderInstanceService>();
        _mockLogger = new Mock<ILogger<ReminderInstancesController>>();
        _controller = new ReminderInstancesController(_mockService.Object, _mockLogger.Object);

        // Set up the controller's User with a test user ID claim
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

    [Fact]
    public void GetAll_ShouldReturnOkWithReminders()
    {
        // Arrange
        var reminders = new List<ReminderInstance>
        {
            new ReminderInstance { Id = Guid.NewGuid(), UserId = TestUserId, Text = "Test 1", ScheduledDateAndTime = DateTime.UtcNow },
            new ReminderInstance { Id = Guid.NewGuid(), UserId = TestUserId, Text = "Test 2", ScheduledDateAndTime = DateTime.UtcNow }
        };
        _mockService.Setup(s => s.GetAll(TestUserId)).Returns(reminders);

        // Act
        var result = _controller.GetAll();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedReminders = okResult.Value.Should().BeAssignableTo<IEnumerable<ReminderInstanceDto>>().Subject;
        returnedReminders.Should().HaveCount(2);
    }

    [Fact]
    public void GetById_WithValidId_ShouldReturnOkWithReminder()
    {
        // Arrange
        var id = Guid.NewGuid();
        var reminder = new ReminderInstance { Id = id, UserId = TestUserId, Text = "Test", ScheduledDateAndTime = DateTime.UtcNow };
        _mockService.Setup(s => s.GetById(id, TestUserId)).Returns(reminder);

        // Act
        var result = _controller.GetById(id);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedReminder = okResult.Value.Should().BeOfType<ReminderInstanceDto>().Subject;
        returnedReminder.Id.Should().Be(id);
    }

    [Fact]
    public void GetById_WithInvalidId_ShouldReturnNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        _mockService.Setup(s => s.GetById(id, TestUserId)).Returns((ReminderInstance?)null);

        // Act
        var result = _controller.GetById(id);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void Create_ShouldReturnCreatedAtAction()
    {
        // Arrange
        var reminderDto = new ReminderInstanceDto { Text = "New Reminder", ScheduledDateAndTime = DateTime.UtcNow };
        var createdReminder = new ReminderInstance
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            Text = reminderDto.Text,
            ScheduledDateAndTime = reminderDto.ScheduledDateAndTime
        };
        _mockService.Setup(s => s.Create(It.IsAny<ReminderInstance>())).Returns(createdReminder);

        // Act
        var result = _controller.Create(reminderDto);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(ReminderInstancesController.GetById));
        var returnedReminder = createdResult.Value.Should().BeOfType<ReminderInstanceDto>().Subject;
        returnedReminder.Id.Should().Be(createdReminder.Id);
    }

    [Fact]
    public void Update_WithValidId_ShouldReturnOk()
    {
        // Arrange
        var id = Guid.NewGuid();
        var reminderDto = new ReminderInstanceDto { Id = id, Text = "Updated", ScheduledDateAndTime = DateTime.UtcNow };
        var updatedReminder = new ReminderInstance
        {
            Id = id,
            UserId = TestUserId,
            Text = "Updated",
            ScheduledDateAndTime = reminderDto.ScheduledDateAndTime
        };
        _mockService.Setup(s => s.Update(id, It.IsAny<ReminderInstance>())).Returns(updatedReminder);

        // Act
        var result = _controller.Update(id, reminderDto);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedReminder = okResult.Value.Should().BeOfType<ReminderInstanceDto>().Subject;
        returnedReminder.Text.Should().Be("Updated");
    }

    [Fact]
    public void Update_WithMismatchedIds_ShouldReturnBadRequest()
    {
        // Arrange
        var urlId = Guid.NewGuid();
        var bodyId = Guid.NewGuid();
        var reminderDto = new ReminderInstanceDto { Id = bodyId, Text = "Test", ScheduledDateAndTime = DateTime.UtcNow };

        // Act
        var result = _controller.Update(urlId, reminderDto);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void Update_WithNonExistentId_ShouldReturnNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        var reminderDto = new ReminderInstanceDto { Id = id, Text = "Test", ScheduledDateAndTime = DateTime.UtcNow };
        _mockService.Setup(s => s.Update(id, It.IsAny<ReminderInstance>())).Returns((ReminderInstance?)null);

        // Act
        var result = _controller.Update(id, reminderDto);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void Delete_WithValidId_ShouldReturnNoContent()
    {
        // Arrange
        var id = Guid.NewGuid();
        _mockService.Setup(s => s.Delete(id, TestUserId)).Returns(true);

        // Act
        var result = _controller.Delete(id);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public void Delete_WithNonExistentId_ShouldReturnNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        _mockService.Setup(s => s.Delete(id, TestUserId)).Returns(false);

        // Act
        var result = _controller.Delete(id);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void GetById_ShouldReturnCorrectState_WhenScheduled()
    {
        // Arrange
        var id = Guid.NewGuid();
        var futureTime = DateTime.UtcNow.AddHours(1);
        var reminder = new ReminderInstance
        {
            Id = id,
            UserId = TestUserId,
            Text = "Future Reminder",
            ScheduledDateAndTime = futureTime,
            IsCompleted = false,
            IsDeleted = false
        };
        _mockService.Setup(s => s.GetById(id, TestUserId)).Returns(reminder);

        // Act
        var result = _controller.GetById(id);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedReminder = okResult.Value.Should().BeOfType<ReminderInstanceDto>().Subject;
        returnedReminder.State.Should().Be(ReminderState.Scheduled);
    }

    [Fact]
    public void GetById_ShouldReturnCorrectState_WhenActive()
    {
        // Arrange
        var id = Guid.NewGuid();
        var pastTime = DateTime.UtcNow.AddHours(-1);
        var reminder = new ReminderInstance
        {
            Id = id,
            UserId = TestUserId,
            Text = "Past Reminder",
            ScheduledDateAndTime = pastTime,
            IsCompleted = false,
            IsDeleted = false
        };
        _mockService.Setup(s => s.GetById(id, TestUserId)).Returns(reminder);

        // Act
        var result = _controller.GetById(id);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedReminder = okResult.Value.Should().BeOfType<ReminderInstanceDto>().Subject;
        returnedReminder.State.Should().Be(ReminderState.Active);
    }

    [Fact]
    public void GetById_ShouldReturnCorrectState_WhenCompleted()
    {
        // Arrange
        var id = Guid.NewGuid();
        var reminder = new ReminderInstance
        {
            Id = id,
            UserId = TestUserId,
            Text = "Completed Reminder",
            ScheduledDateAndTime = DateTime.UtcNow,
            IsCompleted = true,
            IsDeleted = false
        };
        _mockService.Setup(s => s.GetById(id, TestUserId)).Returns(reminder);

        // Act
        var result = _controller.GetById(id);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedReminder = okResult.Value.Should().BeOfType<ReminderInstanceDto>().Subject;
        returnedReminder.State.Should().Be(ReminderState.Completed);
    }

    [Fact]
    public void GetById_ShouldReturnCorrectState_WhenDeleted()
    {
        // Arrange
        var id = Guid.NewGuid();
        var reminder = new ReminderInstance
        {
            Id = id,
            UserId = TestUserId,
            Text = "Deleted Reminder",
            ScheduledDateAndTime = DateTime.UtcNow,
            IsCompleted = false,
            IsDeleted = true
        };
        _mockService.Setup(s => s.GetById(id, TestUserId)).Returns(reminder);

        // Act
        var result = _controller.GetById(id);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedReminder = okResult.Value.Should().BeOfType<ReminderInstanceDto>().Subject;
        returnedReminder.State.Should().Be(ReminderState.Deleted);
    }
}
