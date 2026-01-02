using FluentAssertions;
using HereAndNowService.Models;
using HereAndNowService.Models.Exceptions;
using HereAndNowService.Repositories;
using HereAndNowService.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace HereAndNow.Web.Tests.Services;

public class TaskServiceTests
{
    private readonly Mock<ITaskRepository> _mockRepository;
    private readonly Mock<ILogger<TaskService>> _mockLogger;
    private readonly TaskService _taskService;
    private const string TestUserId = "auth0|test-user-123";

    public TaskServiceTests()
    {
        _mockRepository = new Mock<ITaskRepository>();
        _mockLogger = new Mock<ILogger<TaskService>>();
        _taskService = new TaskService(_mockRepository.Object, _mockLogger.Object);
    }

    #region CreateTaskAsync Tests

    [Fact]
    public async Task CreateTaskAsync_WithValidInput_CreatesTask()
    {
        // Arrange
        TaskDocument? capturedTask = null;
        _mockRepository
            .Setup(r => r.CreateAsync(It.IsAny<TaskDocument>()))
            .Callback<TaskDocument>(t => capturedTask = t)
            .ReturnsAsync((TaskDocument t) => t);

        // Act
        var result = await _taskService.CreateTaskAsync("Test Task", TestUserId);

        // Assert
        result.Name.Should().Be("Test Task");
        result.UserId.Should().Be(TestUserId);
        result.State.Should().Be(TaskState.OnDeck);
        result.Id.Should().NotBeNullOrEmpty();
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.CompletedAt.Should().BeNull();
        result.ReminderId.Should().BeNull();
    }

    [Fact]
    public async Task CreateTaskAsync_InitializesStateToOnDeck()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.CreateAsync(It.IsAny<TaskDocument>()))
            .ReturnsAsync((TaskDocument t) => t);

        // Act
        var result = await _taskService.CreateTaskAsync("New Task", TestUserId);

        // Assert
        result.State.Should().Be(TaskState.OnDeck);
    }

    [Fact]
    public async Task CreateTaskAsync_GeneratesUniqueId()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.CreateAsync(It.IsAny<TaskDocument>()))
            .ReturnsAsync((TaskDocument t) => t);

        // Act
        var result1 = await _taskService.CreateTaskAsync("Task 1", TestUserId);
        var result2 = await _taskService.CreateTaskAsync("Task 2", TestUserId);

        // Assert
        result1.Id.Should().NotBe(result2.Id);
        Guid.TryParse(result1.Id, out _).Should().BeTrue();
    }

    [Fact]
    public async Task CreateTaskAsync_TrimsTaskName()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.CreateAsync(It.IsAny<TaskDocument>()))
            .ReturnsAsync((TaskDocument t) => t);

        // Act
        var result = await _taskService.CreateTaskAsync("  Trimmed Task  ", TestUserId);

        // Assert
        result.Name.Should().Be("Trimmed Task");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task CreateTaskAsync_WithInvalidName_ThrowsArgumentException(string? name)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _taskService.CreateTaskAsync(name!, TestUserId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task CreateTaskAsync_WithInvalidUserId_ThrowsArgumentException(string? userId)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _taskService.CreateTaskAsync("Valid Task", userId!));
    }

    #endregion

    #region GetTasksAsync Tests

    [Fact]
    public async Task GetTasksAsync_WithNoFilter_ReturnsAllTasks()
    {
        // Arrange
        var tasks = new List<TaskDocument>
        {
            new TaskDocument { Id = "1", Name = "Task 1", State = TaskState.OnDeck },
            new TaskDocument { Id = "2", Name = "Task 2", State = TaskState.InProgress }
        };

        _mockRepository
            .Setup(r => r.GetByUserIdAsync(TestUserId, null))
            .ReturnsAsync(tasks);

        // Act
        var result = await _taskService.GetTasksAsync(TestUserId);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetTasksAsync_WithValidState_ReturnsFilteredTasks()
    {
        // Arrange
        var tasks = new List<TaskDocument>
        {
            new TaskDocument { Id = "1", Name = "Task 1", State = TaskState.OnDeck }
        };

        _mockRepository
            .Setup(r => r.GetByUserIdAsync(TestUserId, TaskState.OnDeck))
            .ReturnsAsync(tasks);

        // Act
        var result = await _taskService.GetTasksAsync(TestUserId, TaskState.OnDeck);

        // Assert
        result.Should().HaveCount(1);
        _mockRepository.Verify(r => r.GetByUserIdAsync(TestUserId, TaskState.OnDeck), Times.Once);
    }

    [Fact]
    public async Task GetTasksAsync_WithInvalidState_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _taskService.GetTasksAsync(TestUserId, "InvalidState"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task GetTasksAsync_WithInvalidUserId_ThrowsArgumentException(string? userId)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _taskService.GetTasksAsync(userId!));
    }

    #endregion

    #region GetTaskByIdAsync Tests

    [Fact]
    public async Task GetTaskByIdAsync_WithValidId_ReturnsTask()
    {
        // Arrange
        var task = new TaskDocument { Id = "task-123", Name = "Test Task", UserId = TestUserId };

        _mockRepository
            .Setup(r => r.GetByIdAsync("task-123", TestUserId))
            .ReturnsAsync(task);

        // Act
        var result = await _taskService.GetTaskByIdAsync("task-123", TestUserId);

        // Assert
        result.Id.Should().Be("task-123");
        result.Name.Should().Be("Test Task");
    }

    [Fact]
    public async Task GetTaskByIdAsync_WithNonExistentId_ThrowsTaskNotFoundException()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetByIdAsync("nonexistent", TestUserId))
            .ReturnsAsync((TaskDocument?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TaskNotFoundException>(
            () => _taskService.GetTaskByIdAsync("nonexistent", TestUserId));

        exception.TaskId.Should().Be("nonexistent");
    }

    [Theory]
    [InlineData("", TestUserId)]
    [InlineData("   ", TestUserId)]
    [InlineData("task-123", "")]
    [InlineData("task-123", "   ")]
    public async Task GetTaskByIdAsync_WithInvalidInput_ThrowsArgumentException(string taskId, string userId)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _taskService.GetTaskByIdAsync(taskId, userId));
    }

    #endregion
}
