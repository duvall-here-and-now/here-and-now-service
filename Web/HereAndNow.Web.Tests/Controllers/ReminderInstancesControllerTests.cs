using FluentAssertions;
using HereAndNowService.Controllers;
using HereAndNowService.Models;
using HereAndNowService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace HereAndNow.Web.Tests.Controllers;

public class ReminderInstancesControllerTests
{
    private readonly Mock<IReminderInstanceService> _mockService;
    private readonly Mock<ILogger<ReminderInstancesController>> _mockLogger;
    private readonly ReminderInstancesController _controller;

    public ReminderInstancesControllerTests()
    {
        _mockService = new Mock<IReminderInstanceService>();
        _mockLogger = new Mock<ILogger<ReminderInstancesController>>();
        _controller = new ReminderInstancesController(_mockService.Object, _mockLogger.Object);
    }

    [Fact]
    public void GetAll_ShouldReturnOkWithReminders()
    {
        // Arrange
        var reminders = new List<ReminderInstance>
        {
            new ReminderInstance { Id = Guid.NewGuid(), Text = "Test 1", ScheduledDateAndTime = DateTime.UtcNow },
            new ReminderInstance { Id = Guid.NewGuid(), Text = "Test 2", ScheduledDateAndTime = DateTime.UtcNow }
        };
        _mockService.Setup(s => s.GetAll()).Returns(reminders);

        // Act
        var result = _controller.GetAll();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedReminders = okResult.Value.Should().BeAssignableTo<IEnumerable<ReminderInstance>>().Subject;
        returnedReminders.Should().HaveCount(2);
    }

    [Fact]
    public void GetById_WithValidId_ShouldReturnOkWithReminder()
    {
        // Arrange
        var id = Guid.NewGuid();
        var reminder = new ReminderInstance { Id = id, Text = "Test", ScheduledDateAndTime = DateTime.UtcNow };
        _mockService.Setup(s => s.GetById(id)).Returns(reminder);

        // Act
        var result = _controller.GetById(id);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedReminder = okResult.Value.Should().BeOfType<ReminderInstance>().Subject;
        returnedReminder.Id.Should().Be(id);
    }

    [Fact]
    public void GetById_WithInvalidId_ShouldReturnNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        _mockService.Setup(s => s.GetById(id)).Returns((ReminderInstance?)null);

        // Act
        var result = _controller.GetById(id);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void Create_ShouldReturnCreatedAtAction()
    {
        // Arrange
        var reminder = new ReminderInstance { Text = "New Reminder", ScheduledDateAndTime = DateTime.UtcNow };
        var createdReminder = new ReminderInstance { Id = Guid.NewGuid(), Text = reminder.Text, ScheduledDateAndTime = reminder.ScheduledDateAndTime };
        _mockService.Setup(s => s.Create(reminder)).Returns(createdReminder);

        // Act
        var result = _controller.Create(reminder);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(ReminderInstancesController.GetById));
        var returnedReminder = createdResult.Value.Should().BeOfType<ReminderInstance>().Subject;
        returnedReminder.Id.Should().Be(createdReminder.Id);
    }

    [Fact]
    public void Update_WithValidId_ShouldReturnOk()
    {
        // Arrange
        var id = Guid.NewGuid();
        var reminder = new ReminderInstance { Id = id, Text = "Updated", ScheduledDateAndTime = DateTime.UtcNow };
        _mockService.Setup(s => s.Update(id, reminder)).Returns(reminder);

        // Act
        var result = _controller.Update(id, reminder);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedReminder = okResult.Value.Should().BeOfType<ReminderInstance>().Subject;
        returnedReminder.Text.Should().Be("Updated");
    }

    [Fact]
    public void Update_WithMismatchedIds_ShouldReturnBadRequest()
    {
        // Arrange
        var urlId = Guid.NewGuid();
        var bodyId = Guid.NewGuid();
        var reminder = new ReminderInstance { Id = bodyId, Text = "Test", ScheduledDateAndTime = DateTime.UtcNow };

        // Act
        var result = _controller.Update(urlId, reminder);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void Update_WithNonExistentId_ShouldReturnNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        var reminder = new ReminderInstance { Id = id, Text = "Test", ScheduledDateAndTime = DateTime.UtcNow };
        _mockService.Setup(s => s.Update(id, reminder)).Returns((ReminderInstance?)null);

        // Act
        var result = _controller.Update(id, reminder);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void Delete_WithValidId_ShouldReturnNoContent()
    {
        // Arrange
        var id = Guid.NewGuid();
        _mockService.Setup(s => s.Delete(id)).Returns(true);

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
        _mockService.Setup(s => s.Delete(id)).Returns(false);

        // Act
        var result = _controller.Delete(id);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
