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
    private readonly Mock<IReminderInstanceService> _mockService;
    private readonly Mock<ILogger<ReminderInstancesController>> _mockLogger;
    private readonly ReminderInstancesController _controller;
    private const string TestUserId = "auth0|test-user-123";

    public ReminderInstancesControllerTests()
    {
        _mockService = new Mock<IReminderInstanceService>();
        _mockLogger = new Mock<ILogger<ReminderInstancesController>>();
        _controller = new ReminderInstancesController(_mockService.Object, _mockLogger.Object);

        // Set up user context with claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, TestUserId),
            new Claim("sub", TestUserId)
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
        var reminderId = Guid.NewGuid();
        var request = new CreateReminderRequest
        {
            Id = reminderId,
            Text = "New Reminder",
            ScheduledDateAndTime = DateTime.UtcNow.AddHours(1),
            ShouldPlaySound = true,
            ShouldDoVibration = false
        };
        var createdReminder = new ReminderInstance
        {
            Id = reminderId,
            UserId = TestUserId,
            Text = request.Text,
            ScheduledDateAndTime = request.ScheduledDateAndTime,
            ShouldPlaySound = request.ShouldPlaySound,
            ShouldDoVibration = request.ShouldDoVibration,
            IsCompleted = false,
            IsDeleted = false,
            CreatedDateAndTime = DateTime.UtcNow
        };
        _mockService.Setup(s => s.Create(
            reminderId,
            TestUserId,
            request.Text,
            request.ScheduledDateAndTime,
            request.ShouldPlaySound,
            request.ShouldDoVibration))
            .Returns(createdReminder);

        // Act
        var result = _controller.Create(request);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(ReminderInstancesController.GetById));
        var returnedReminder = createdResult.Value.Should().BeOfType<ReminderInstanceDto>().Subject;
        returnedReminder.Id.Should().Be(reminderId);
        returnedReminder.Text.Should().Be(request.Text);
    }

    [Fact]
    public void Update_WithValidId_ShouldReturnOk()
    {
        // Arrange
        var id = Guid.NewGuid();
        var request = new UpdateReminderRequest
        {
            Text = "Updated Text",
            ScheduledDateAndTime = DateTime.UtcNow.AddHours(2)
        };
        var updatedReminder = new ReminderInstance
        {
            Id = id,
            UserId = TestUserId,
            Text = "Updated Text",
            ScheduledDateAndTime = request.ScheduledDateAndTime!.Value,
            CreatedDateAndTime = DateTime.UtcNow.AddDays(-1)
        };
        _mockService.Setup(s => s.Update(
            id,
            TestUserId,
            request.Text,
            request.ScheduledDateAndTime,
            request.ShouldPlaySound,
            request.ShouldDoVibration))
            .Returns(updatedReminder);

        // Act
        var result = _controller.Update(id, request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedReminder = okResult.Value.Should().BeOfType<ReminderInstanceDto>().Subject;
        returnedReminder.Text.Should().Be("Updated Text");
    }

    [Fact]
    public void Update_WithNonExistentId_ShouldReturnNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        var request = new UpdateReminderRequest { Text = "Test" };
        _mockService.Setup(s => s.Update(
            id,
            TestUserId,
            request.Text,
            request.ScheduledDateAndTime,
            request.ShouldPlaySound,
            request.ShouldDoVibration))
            .Returns((ReminderInstance?)null);

        // Act
        var result = _controller.Update(id, request);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void Complete_WithValidId_ShouldReturnOk()
    {
        // Arrange
        var id = Guid.NewGuid();
        var completedReminder = new ReminderInstance
        {
            Id = id,
            UserId = TestUserId,
            Text = "Test Reminder",
            ScheduledDateAndTime = DateTime.UtcNow.AddHours(-1),
            IsCompleted = true,
            IsDeleted = false,
            CreatedDateAndTime = DateTime.UtcNow.AddDays(-1),
            CompletedDateAndTime = DateTime.UtcNow
        };
        _mockService.Setup(s => s.Complete(id, TestUserId)).Returns(completedReminder);

        // Act
        var result = _controller.Complete(id);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedReminder = okResult.Value.Should().BeOfType<ReminderInstanceDto>().Subject;
        returnedReminder.IsCompleted.Should().BeTrue();
        returnedReminder.CompletedDateAndTime.Should().NotBeNull();
    }

    [Fact]
    public void Complete_WithNonExistentId_ShouldReturnNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        _mockService.Setup(s => s.Complete(id, TestUserId)).Returns((ReminderInstance?)null);

        // Act
        var result = _controller.Complete(id);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void Complete_WithDeletedReminder_ShouldReturnConflict()
    {
        // Arrange
        var id = Guid.NewGuid();
        _mockService.Setup(s => s.Complete(id, TestUserId))
            .Throws(new InvalidOperationException("Cannot complete a deleted reminder."));

        // Act
        var result = _controller.Complete(id);

        // Assert
        result.Result.Should().BeOfType<ConflictObjectResult>();
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
            IsDeleted = false,
            CompletedDateAndTime = DateTime.UtcNow
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
            IsDeleted = true,
            DeletedDateAndTime = DateTime.UtcNow
        };
        _mockService.Setup(s => s.GetById(id, TestUserId)).Returns(reminder);

        // Act
        var result = _controller.GetById(id);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedReminder = okResult.Value.Should().BeOfType<ReminderInstanceDto>().Subject;
        returnedReminder.State.Should().Be(ReminderState.Deleted);
    }

    [Fact]
    public void Create_ShouldSetServerControlledFields()
    {
        // Arrange
        var reminderId = Guid.NewGuid();
        var request = new CreateReminderRequest
        {
            Id = reminderId,
            Text = "Test",
            ScheduledDateAndTime = DateTime.UtcNow.AddHours(1),
            ShouldPlaySound = false,
            ShouldDoVibration = true
        };
        var createdReminder = new ReminderInstance
        {
            Id = reminderId,
            UserId = TestUserId,
            Text = request.Text,
            ScheduledDateAndTime = request.ScheduledDateAndTime,
            ShouldPlaySound = request.ShouldPlaySound,
            ShouldDoVibration = request.ShouldDoVibration,
            IsCompleted = false,
            IsDeleted = false,
            CreatedDateAndTime = DateTime.UtcNow,
            CompletedDateAndTime = null,
            DeletedDateAndTime = null
        };
        _mockService.Setup(s => s.Create(
            reminderId,
            TestUserId,
            request.Text,
            request.ScheduledDateAndTime,
            request.ShouldPlaySound,
            request.ShouldDoVibration))
            .Returns(createdReminder);

        // Act
        var result = _controller.Create(request);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var returnedReminder = createdResult.Value.Should().BeOfType<ReminderInstanceDto>().Subject;

        // Client-provided ID should be used
        returnedReminder.Id.Should().Be(reminderId);
        // Server-controlled fields should be set correctly
        returnedReminder.IsCompleted.Should().BeFalse();
        returnedReminder.IsDeleted.Should().BeFalse();
        returnedReminder.CreatedDateAndTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        returnedReminder.CompletedDateAndTime.Should().BeNull();
        returnedReminder.DeletedDateAndTime.Should().BeNull();
    }

    [Fact]
    public void Create_WithDuplicateId_ShouldReturn409Conflict()
    {
        // Arrange
        var existingId = Guid.NewGuid();
        var request = new CreateReminderRequest
        {
            Id = existingId,
            Text = "Duplicate Reminder",
            ScheduledDateAndTime = DateTime.UtcNow.AddHours(1),
            ShouldPlaySound = true,
            ShouldDoVibration = false
        };
        _mockService.Setup(s => s.Create(
            existingId,
            TestUserId,
            request.Text,
            request.ScheduledDateAndTime,
            request.ShouldPlaySound,
            request.ShouldDoVibration))
            .Throws(new InvalidOperationException($"A reminder with ID {existingId} already exists."));

        // Act
        var result = _controller.Create(request);

        // Assert
        var conflictResult = result.Result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflictResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public void Create_WithClientProvidedId_ShouldReturnSameIdInResponse()
    {
        // Arrange
        var clientProvidedId = Guid.NewGuid();
        var request = new CreateReminderRequest
        {
            Id = clientProvidedId,
            Text = "Test Reminder",
            ScheduledDateAndTime = DateTime.UtcNow.AddHours(1),
            ShouldPlaySound = true,
            ShouldDoVibration = false
        };
        var createdReminder = new ReminderInstance
        {
            Id = clientProvidedId,
            UserId = TestUserId,
            Text = request.Text,
            ScheduledDateAndTime = request.ScheduledDateAndTime,
            ShouldPlaySound = request.ShouldPlaySound,
            ShouldDoVibration = request.ShouldDoVibration,
            IsCompleted = false,
            IsDeleted = false,
            CreatedDateAndTime = DateTime.UtcNow
        };
        _mockService.Setup(s => s.Create(
            clientProvidedId,
            TestUserId,
            request.Text,
            request.ScheduledDateAndTime,
            request.ShouldPlaySound,
            request.ShouldDoVibration))
            .Returns(createdReminder);

        // Act
        var result = _controller.Create(request);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var returnedReminder = createdResult.Value.Should().BeOfType<ReminderInstanceDto>().Subject;
        returnedReminder.Id.Should().Be(clientProvidedId, "the returned ID should match the client-provided ID");
    }

    [Fact]
    public void Update_WithPartialData_ShouldOnlyUpdateProvidedFields()
    {
        // Arrange
        var id = Guid.NewGuid();
        var originalScheduledTime = DateTime.UtcNow.AddHours(1);
        var request = new UpdateReminderRequest
        {
            Text = "Only text updated"
            // Other fields are null - should not be updated
        };
        var updatedReminder = new ReminderInstance
        {
            Id = id,
            UserId = TestUserId,
            Text = "Only text updated",
            ScheduledDateAndTime = originalScheduledTime, // Unchanged
            ShouldPlaySound = true, // Original value preserved
            ShouldDoVibration = false, // Original value preserved
            CreatedDateAndTime = DateTime.UtcNow.AddDays(-1)
        };
        _mockService.Setup(s => s.Update(
            id,
            TestUserId,
            request.Text,
            null, // scheduledDateAndTime not provided
            null, // shouldPlaySound not provided
            null)) // shouldDoVibration not provided
            .Returns(updatedReminder);

        // Act
        var result = _controller.Update(id, request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedReminder = okResult.Value.Should().BeOfType<ReminderInstanceDto>().Subject;
        returnedReminder.Text.Should().Be("Only text updated");
        returnedReminder.ScheduledDateAndTime.Should().Be(originalScheduledTime);
    }

    #region State Validation Tests (Story 1.2)

    [Fact]
    public void Update_ScheduledTimeOnScheduledReminder_Returns200Ok()
    {
        // Arrange - Reminder is in Scheduled state (future time, not completed, not deleted)
        var id = Guid.NewGuid();
        var futureTime = DateTime.UtcNow.AddHours(2);
        var newScheduledTime = DateTime.UtcNow.AddHours(3);
        var request = new UpdateReminderRequest
        {
            ScheduledDateAndTime = newScheduledTime
        };
        var updatedReminder = new ReminderInstance
        {
            Id = id,
            UserId = TestUserId,
            Text = "Test",
            ScheduledDateAndTime = newScheduledTime,
            IsCompleted = false,
            IsDeleted = false,
            CreatedDateAndTime = DateTime.UtcNow.AddDays(-1)
        };
        _mockService.Setup(s => s.Update(
            id,
            TestUserId,
            null,
            newScheduledTime,
            null,
            null))
            .Returns(updatedReminder);

        // Act
        var result = _controller.Update(id, request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedReminder = okResult.Value.Should().BeOfType<ReminderInstanceDto>().Subject;
        returnedReminder.ScheduledDateAndTime.Should().Be(newScheduledTime);
    }

    [Fact]
    public void Update_ScheduledTimeOnActiveReminder_Returns400BadRequest()
    {
        // Arrange - Reminder is in Active state (past time, not completed, not deleted)
        var id = Guid.NewGuid();
        var newScheduledTime = DateTime.UtcNow.AddHours(1);
        var request = new UpdateReminderRequest
        {
            ScheduledDateAndTime = newScheduledTime
        };
        _mockService.Setup(s => s.Update(
            id,
            TestUserId,
            null,
            newScheduledTime,
            null,
            null))
            .Throws(new InvalidOperationException("Cannot update scheduled time. Reminder is in 'Active' state."));

        // Act
        var result = _controller.Update(id, request);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void Update_ScheduledTimeOnCompletedReminder_Returns400BadRequest()
    {
        // Arrange - Reminder is in Completed state
        var id = Guid.NewGuid();
        var newScheduledTime = DateTime.UtcNow.AddHours(1);
        var request = new UpdateReminderRequest
        {
            ScheduledDateAndTime = newScheduledTime
        };
        _mockService.Setup(s => s.Update(
            id,
            TestUserId,
            null,
            newScheduledTime,
            null,
            null))
            .Throws(new InvalidOperationException("Cannot update scheduled time. Reminder is in 'Completed' state."));

        // Act
        var result = _controller.Update(id, request);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void Update_TextOnActiveReminder_Returns200Ok()
    {
        // Arrange - Reminder is in Active state but updating text (not scheduledDateAndTime)
        var id = Guid.NewGuid();
        var pastTime = DateTime.UtcNow.AddHours(-1);
        var request = new UpdateReminderRequest
        {
            Text = "Updated text on active reminder"
        };
        var updatedReminder = new ReminderInstance
        {
            Id = id,
            UserId = TestUserId,
            Text = "Updated text on active reminder",
            ScheduledDateAndTime = pastTime,
            IsCompleted = false,
            IsDeleted = false,
            CreatedDateAndTime = DateTime.UtcNow.AddDays(-1)
        };
        _mockService.Setup(s => s.Update(
            id,
            TestUserId,
            request.Text,
            null,
            null,
            null))
            .Returns(updatedReminder);

        // Act
        var result = _controller.Update(id, request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedReminder = okResult.Value.Should().BeOfType<ReminderInstanceDto>().Subject;
        returnedReminder.Text.Should().Be("Updated text on active reminder");
    }

    [Fact]
    public void Update_TextOnCompletedReminder_Returns200Ok()
    {
        // Arrange - Reminder is in Completed state but updating text (not scheduledDateAndTime)
        var id = Guid.NewGuid();
        var request = new UpdateReminderRequest
        {
            Text = "Updated text on completed reminder"
        };
        var updatedReminder = new ReminderInstance
        {
            Id = id,
            UserId = TestUserId,
            Text = "Updated text on completed reminder",
            ScheduledDateAndTime = DateTime.UtcNow.AddHours(-1),
            IsCompleted = true,
            IsDeleted = false,
            CreatedDateAndTime = DateTime.UtcNow.AddDays(-1),
            CompletedDateAndTime = DateTime.UtcNow
        };
        _mockService.Setup(s => s.Update(
            id,
            TestUserId,
            request.Text,
            null,
            null,
            null))
            .Returns(updatedReminder);

        // Act
        var result = _controller.Update(id, request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedReminder = okResult.Value.Should().BeOfType<ReminderInstanceDto>().Subject;
        returnedReminder.Text.Should().Be("Updated text on completed reminder");
    }

    #endregion
}
