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
    private readonly Mock<ITaskReminderRepository> _mockReminderRepository;
    private readonly Mock<ILogger<TaskService>> _mockLogger;
    private readonly TaskService _taskService;
    private const string TestUserId = "auth0|test-user-123";

    public TaskServiceTests()
    {
        _mockRepository = new Mock<ITaskRepository>();
        _mockReminderRepository = new Mock<ITaskReminderRepository>();
        _mockLogger = new Mock<ILogger<TaskService>>();
        _taskService = new TaskService(_mockRepository.Object, _mockReminderRepository.Object, _mockLogger.Object);
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
    public async Task CreateTaskAsync_WithValidInput_SetsLastModifiedAtEqualToCreatedAt()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.CreateAsync(It.IsAny<TaskDocument>()))
            .ReturnsAsync((TaskDocument t) => t);

        // Act
        var result = await _taskService.CreateTaskAsync("Test Task", TestUserId);

        // Assert
        result.LastModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.LastModifiedAt.Should().Be(result.CreatedAt);
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
            .Setup(r => r.GetByIdAsync(TestUserId, "task-123"))
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
            .Setup(r => r.GetByIdAsync(TestUserId, "nonexistent"))
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

    #region UpdateTaskAsync Tests

    [Fact]
    public async Task UpdateTaskAsync_WithValidName_UpdatesName()
    {
        // Arrange
        var existingTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "Original Name",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "task-123"))
            .ReturnsAsync(existingTask);
        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<TaskDocument>()))
            .ReturnsAsync((TaskDocument t) => t);

        // Act
        var result = await _taskService.UpdateTaskAsync("task-123", TestUserId, "Updated Name", null);

        // Assert
        result.Name.Should().Be("Updated Name");
        result.State.Should().Be(TaskState.OnDeck); // unchanged
    }

    [Fact]
    public async Task UpdateTaskAsync_WithValidState_UpdatesState()
    {
        // Arrange
        var existingTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "My Task",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "task-123"))
            .ReturnsAsync(existingTask);
        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<TaskDocument>()))
            .ReturnsAsync((TaskDocument t) => t);

        // Act
        var result = await _taskService.UpdateTaskAsync("task-123", TestUserId, null, TaskState.InProgress);

        // Assert
        result.State.Should().Be(TaskState.InProgress);
        result.Name.Should().Be("My Task"); // unchanged
    }

    [Fact]
    public async Task UpdateTaskAsync_TransitionToCompleted_SetsCompletedAt()
    {
        // Arrange
        var existingTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "My Task",
            State = TaskState.InProgress,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            CompletedAt = null
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "task-123"))
            .ReturnsAsync(existingTask);
        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<TaskDocument>()))
            .ReturnsAsync((TaskDocument t) => t);

        // Act
        var result = await _taskService.UpdateTaskAsync("task-123", TestUserId, null, TaskState.Completed);

        // Assert
        result.State.Should().Be(TaskState.Completed);
        result.CompletedAt.Should().NotBeNull();
        result.CompletedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UpdateTaskAsync_TransitionFromCompletedToOnDeck_ClearsCompletedAt()
    {
        // Arrange
        var existingTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "My Task",
            State = TaskState.Completed,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            CompletedAt = DateTime.UtcNow.AddHours(-1)
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "task-123"))
            .ReturnsAsync(existingTask);
        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<TaskDocument>()))
            .ReturnsAsync((TaskDocument t) => t);

        // Act
        var result = await _taskService.UpdateTaskAsync("task-123", TestUserId, null, TaskState.OnDeck);

        // Assert
        result.State.Should().Be(TaskState.OnDeck);
        result.CompletedAt.Should().BeNull();
    }

    [Fact]
    public async Task UpdateTaskAsync_TransitionFromCompletedToInProgress_ClearsCompletedAt()
    {
        // Arrange
        var existingTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "My Task",
            State = TaskState.Completed,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            CompletedAt = DateTime.UtcNow.AddHours(-1)
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "task-123"))
            .ReturnsAsync(existingTask);
        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<TaskDocument>()))
            .ReturnsAsync((TaskDocument t) => t);

        // Act
        var result = await _taskService.UpdateTaskAsync("task-123", TestUserId, null, TaskState.InProgress);

        // Assert
        result.State.Should().Be(TaskState.InProgress);
        result.CompletedAt.Should().BeNull();
    }

    [Fact]
    public async Task UpdateTaskAsync_OnDeckToInProgress_PreservesNullCompletedAt()
    {
        // Arrange
        var existingTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "My Task",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            CompletedAt = null
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "task-123"))
            .ReturnsAsync(existingTask);
        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<TaskDocument>()))
            .ReturnsAsync((TaskDocument t) => t);

        // Act
        var result = await _taskService.UpdateTaskAsync("task-123", TestUserId, null, TaskState.InProgress);

        // Assert
        result.State.Should().Be(TaskState.InProgress);
        result.CompletedAt.Should().BeNull(); // still null
    }

    [Fact]
    public async Task UpdateTaskAsync_WithBothNameAndState_UpdatesBoth()
    {
        // Arrange
        var existingTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "Original Name",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "task-123"))
            .ReturnsAsync(existingTask);
        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<TaskDocument>()))
            .ReturnsAsync((TaskDocument t) => t);

        // Act
        var result = await _taskService.UpdateTaskAsync("task-123", TestUserId, "New Name", TaskState.InProgress);

        // Assert
        result.Name.Should().Be("New Name");
        result.State.Should().Be(TaskState.InProgress);
    }

    [Fact]
    public async Task UpdateTaskAsync_TrimsName()
    {
        // Arrange
        var existingTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "Original Name",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "task-123"))
            .ReturnsAsync(existingTask);
        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<TaskDocument>()))
            .ReturnsAsync((TaskDocument t) => t);

        // Act
        var result = await _taskService.UpdateTaskAsync("task-123", TestUserId, "  Trimmed Name  ", null);

        // Assert
        result.Name.Should().Be("Trimmed Name");
    }

    [Fact]
    public async Task UpdateTaskAsync_WithNonExistentTask_ThrowsTaskNotFoundException()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "nonexistent"))
            .ReturnsAsync((TaskDocument?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TaskNotFoundException>(
            () => _taskService.UpdateTaskAsync("nonexistent", TestUserId, "New Name", null));

        exception.TaskId.Should().Be("nonexistent");
    }

    [Theory]
    [InlineData("", TestUserId)]
    [InlineData("   ", TestUserId)]
    [InlineData("task-123", "")]
    [InlineData("task-123", "   ")]
    public async Task UpdateTaskAsync_WithInvalidInput_ThrowsArgumentException(string taskId, string userId)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _taskService.UpdateTaskAsync(taskId, userId, "New Name", null));
    }

    [Fact]
    public async Task UpdateTaskAsync_WithInvalidState_ThrowsArgumentException()
    {
        // Arrange
        var existingTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "My Task",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "task-123"))
            .ReturnsAsync(existingTask);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _taskService.UpdateTaskAsync("task-123", TestUserId, null, "InvalidState"));

        exception.Message.Should().Contain("Invalid task state");
        exception.ParamName.Should().Be("state");

        // Verify repository.UpdateAsync was never called (validation failed before persistence)
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<TaskDocument>()), Times.Never);
    }

    [Fact]
    public async Task UpdateTaskAsync_WithNameChange_UpdatesLastModifiedAt()
    {
        // Arrange
        var originalLastModified = DateTime.UtcNow.AddDays(-1);
        var existingTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "Original Name",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            LastModifiedAt = originalLastModified
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "task-123"))
            .ReturnsAsync(existingTask);
        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<TaskDocument>()))
            .ReturnsAsync((TaskDocument t) => t);

        // Act
        var result = await _taskService.UpdateTaskAsync("task-123", TestUserId, "Updated Name", null);

        // Assert
        result.LastModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.LastModifiedAt.Should().BeAfter(originalLastModified);
    }

    [Fact]
    public async Task UpdateTaskAsync_WithStateChange_UpdatesLastModifiedAt()
    {
        // Arrange
        var originalLastModified = DateTime.UtcNow.AddDays(-1);
        var existingTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "My Task",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            LastModifiedAt = originalLastModified
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "task-123"))
            .ReturnsAsync(existingTask);
        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<TaskDocument>()))
            .ReturnsAsync((TaskDocument t) => t);

        // Act
        var result = await _taskService.UpdateTaskAsync("task-123", TestUserId, null, TaskState.InProgress);

        // Assert
        result.LastModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.LastModifiedAt.Should().BeAfter(originalLastModified);
    }

    [Fact]
    public async Task UpdateTaskAsync_WithNameChangeAndActiveReminder_SyncsReminderTaskName()
    {
        // Arrange
        var reminderId = "reminder-123";
        var existingTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "Original Name",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            ReminderId = reminderId
        };

        var existingReminder = new TaskReminderDocument
        {
            Id = reminderId,
            UserId = TestUserId,
            TaskId = "task-123",
            TaskName = "Original Name",
            ScheduledTime = DateTime.UtcNow.AddHours(1),
            IsDismissed = false
        };

        TaskDocument? capturedTask = null;
        TaskReminderDocument? capturedReminder = null;

        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "task-123"))
            .ReturnsAsync(existingTask);

        _mockReminderRepository
            .Setup(r => r.GetByIdAsync(TestUserId, reminderId))
            .ReturnsAsync(existingReminder);

        _mockRepository
            .Setup(r => r.UpdateWithReminderSyncAsync(It.IsAny<TaskDocument>(), It.IsAny<TaskReminderDocument>()))
            .Callback<TaskDocument, TaskReminderDocument>((t, r) =>
            {
                capturedTask = t;
                capturedReminder = r;
            })
            .ReturnsAsync((TaskDocument t, TaskReminderDocument r) => t);

        // Act
        var result = await _taskService.UpdateTaskAsync("task-123", TestUserId, "Updated Name", null);

        // Assert - Task name updated
        capturedTask.Should().NotBeNull();
        capturedTask!.Name.Should().Be("Updated Name");

        // Assert - Reminder TaskName synced
        capturedReminder.Should().NotBeNull();
        capturedReminder!.TaskName.Should().Be("Updated Name", "Reminder TaskName should sync with Task Name");
        capturedReminder.LastModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Assert - Sync path taken, not regular update
        _mockRepository.Verify(r => r.UpdateWithReminderSyncAsync(It.IsAny<TaskDocument>(), It.IsAny<TaskReminderDocument>()), Times.Once);
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<TaskDocument>()), Times.Never);
    }

    [Fact]
    public async Task UpdateTaskAsync_WithNameChangeAndDismissedReminder_UsesRegularUpdate()
    {
        // Arrange
        var reminderId = "reminder-123";
        var existingTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "Original Name",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            ReminderId = reminderId
        };

        var dismissedReminder = new TaskReminderDocument
        {
            Id = reminderId,
            UserId = TestUserId,
            TaskId = "task-123",
            TaskName = "Original Name",
            ScheduledTime = DateTime.UtcNow.AddHours(-1),
            IsDismissed = true,
            DismissedAt = DateTime.UtcNow.AddMinutes(-30)
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "task-123"))
            .ReturnsAsync(existingTask);

        _mockReminderRepository
            .Setup(r => r.GetByIdAsync(TestUserId, reminderId))
            .ReturnsAsync(dismissedReminder);

        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<TaskDocument>()))
            .ReturnsAsync((TaskDocument t) => t);

        // Act
        var result = await _taskService.UpdateTaskAsync("task-123", TestUserId, "Updated Name", null);

        // Assert - Name updated
        result.Name.Should().Be("Updated Name");

        // Assert - Regular update path taken (no sync for dismissed reminder)
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<TaskDocument>()), Times.Once);
        _mockRepository.Verify(r => r.UpdateWithReminderSyncAsync(It.IsAny<TaskDocument>(), It.IsAny<TaskReminderDocument>()), Times.Never);
    }

    [Fact]
    public async Task UpdateTaskAsync_WithNameChangeAndStaleReminderId_ClearsReferenceAndUpdates()
    {
        // Arrange - Task has ReminderId but reminder doesn't exist (stale reference)
        var existingTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "Original Name",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            ReminderId = "deleted-reminder"
        };

        TaskDocument? capturedTask = null;

        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "task-123"))
            .ReturnsAsync(existingTask);

        _mockReminderRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "deleted-reminder"))
            .ReturnsAsync((TaskReminderDocument?)null);

        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<TaskDocument>()))
            .Callback<TaskDocument>(t => capturedTask = t)
            .ReturnsAsync((TaskDocument t) => t);

        // Act
        var result = await _taskService.UpdateTaskAsync("task-123", TestUserId, "Updated Name", null);

        // Assert - Name updated
        result.Name.Should().Be("Updated Name");

        // Assert - Stale ReminderId cleared
        capturedTask.Should().NotBeNull();
        capturedTask!.ReminderId.Should().BeNull("Stale ReminderId should be cleared");

        // Assert - Regular update path taken
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<TaskDocument>()), Times.Once);
        _mockRepository.Verify(r => r.UpdateWithReminderSyncAsync(It.IsAny<TaskDocument>(), It.IsAny<TaskReminderDocument>()), Times.Never);
    }

    [Fact]
    public async Task UpdateTaskAsync_WithStateChangeOnlyAndActiveReminder_DoesNotSyncReminder()
    {
        // Arrange
        var reminderId = "reminder-123";
        var existingTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "My Task",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            ReminderId = reminderId
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "task-123"))
            .ReturnsAsync(existingTask);

        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<TaskDocument>()))
            .ReturnsAsync((TaskDocument t) => t);

        // Act - State change only, no name change
        var result = await _taskService.UpdateTaskAsync("task-123", TestUserId, null, TaskState.InProgress);

        // Assert - State updated
        result.State.Should().Be(TaskState.InProgress);

        // Assert - No reminder lookup (name didn't change)
        _mockReminderRepository.Verify(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);

        // Assert - Regular update path taken
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<TaskDocument>()), Times.Once);
        _mockRepository.Verify(r => r.UpdateWithReminderSyncAsync(It.IsAny<TaskDocument>(), It.IsAny<TaskReminderDocument>()), Times.Never);
    }

    [Fact]
    public async Task UpdateTaskAsync_WithNameChangeAndNoReminder_UsesRegularUpdate()
    {
        // Arrange - Task has no reminder
        var existingTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "Original Name",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            ReminderId = null
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "task-123"))
            .ReturnsAsync(existingTask);

        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<TaskDocument>()))
            .ReturnsAsync((TaskDocument t) => t);

        // Act
        var result = await _taskService.UpdateTaskAsync("task-123", TestUserId, "Updated Name", null);

        // Assert - Name updated
        result.Name.Should().Be("Updated Name");

        // Assert - No reminder lookup (no ReminderId)
        _mockReminderRepository.Verify(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);

        // Assert - Regular update path taken
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<TaskDocument>()), Times.Once);
        _mockRepository.Verify(r => r.UpdateWithReminderSyncAsync(It.IsAny<TaskDocument>(), It.IsAny<TaskReminderDocument>()), Times.Never);
    }

    #endregion

    #region CreateTaskWithOptionalReminderAsync Tests

    [Fact]
    public async Task CreateTaskWithOptionalReminderAsync_WithoutReminder_SetsLastModifiedAtEqualToCreatedAt()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.CreateAsync(It.IsAny<TaskDocument>()))
            .ReturnsAsync((TaskDocument t) => t);

        // Act
        var result = await _taskService.CreateTaskWithOptionalReminderAsync("Test Task", TestUserId, null);

        // Assert
        result.LastModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.LastModifiedAt.Should().Be(result.CreatedAt);
        result.ReminderId.Should().BeNull();
    }

    [Fact]
    public async Task CreateTaskWithOptionalReminderAsync_WithReminder_SetsLastModifiedAtOnBothTaskAndReminder()
    {
        // Arrange
        var scheduledTime = DateTime.UtcNow.AddHours(2);
        TaskReminderDocument? capturedReminder = null;

        _mockRepository
            .Setup(r => r.CreateAsync(It.IsAny<TaskDocument>()))
            .ReturnsAsync((TaskDocument t) => t);

        _mockReminderRepository
            .Setup(r => r.CreateWithTaskLinkAsync(It.IsAny<TaskReminderDocument>(), It.IsAny<string>()))
            .Callback<TaskReminderDocument, string>((r, _) => capturedReminder = r)
            .ReturnsAsync((TaskReminderDocument r, string _) => r);

        // Act
        var result = await _taskService.CreateTaskWithOptionalReminderAsync("Test Task", TestUserId, scheduledTime);

        // Assert - Task LastModifiedAt
        result.LastModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.LastModifiedAt.Should().Be(result.CreatedAt);

        // Assert - Reminder LastModifiedAt
        capturedReminder.Should().NotBeNull();
        capturedReminder!.LastModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        capturedReminder.LastModifiedAt.Should().Be(capturedReminder.CreatedAt);
    }

    #endregion

    #region CompleteTaskWithUnityAsync Tests

    [Fact]
    public async Task CompleteTaskWithUnityAsync_WithReminder_ClearsReminderId()
    {
        // Arrange
        var reminderId = "reminder-123";
        var existingTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "Task with Reminder",
            State = TaskState.InProgress,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            ReminderId = reminderId
        };

        var existingReminder = new TaskReminderDocument
        {
            Id = reminderId,
            UserId = TestUserId,
            TaskId = "task-123",
            TaskName = "Task with Reminder",
            ScheduledTime = DateTime.UtcNow.AddHours(1),
            IsDismissed = false
        };

        TaskDocument? capturedTask = null;
        TaskReminderDocument? capturedReminder = null;

        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "task-123"))
            .ReturnsAsync(existingTask);

        _mockReminderRepository
            .Setup(r => r.GetByIdAsync(TestUserId, reminderId))
            .ReturnsAsync(existingReminder);

        _mockRepository
            .Setup(r => r.CompleteWithUnityAsync(It.IsAny<TaskDocument>(), It.IsAny<TaskReminderDocument?>()))
            .Callback<TaskDocument, TaskReminderDocument?>((t, r) =>
            {
                capturedTask = t;
                capturedReminder = r;
            })
            .ReturnsAsync((TaskDocument t, TaskReminderDocument? r) => t);

        // Act
        var result = await _taskService.CompleteTaskWithUnityAsync(TestUserId, "task-123");

        // Assert - Task ReminderId should be cleared
        capturedTask.Should().NotBeNull();
        capturedTask!.ReminderId.Should().BeNull("ReminderId should be cleared when reminder is dismissed");
        capturedTask.State.Should().Be(TaskState.Completed);
        capturedTask.CompletedAt.Should().NotBeNull();

        // Assert - Reminder should be dismissed
        capturedReminder.Should().NotBeNull();
        capturedReminder!.IsDismissed.Should().BeTrue();
        capturedReminder.DismissedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CompleteTaskWithUnityAsync_WithoutReminder_CompletesTask()
    {
        // Arrange
        var existingTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "Task without Reminder",
            State = TaskState.InProgress,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            ReminderId = null
        };

        TaskDocument? capturedTask = null;

        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "task-123"))
            .ReturnsAsync(existingTask);

        _mockRepository
            .Setup(r => r.CompleteWithUnityAsync(It.IsAny<TaskDocument>(), null))
            .Callback<TaskDocument, TaskReminderDocument?>((t, r) => capturedTask = t)
            .ReturnsAsync((TaskDocument t, TaskReminderDocument? r) => t);

        // Act
        var result = await _taskService.CompleteTaskWithUnityAsync(TestUserId, "task-123");

        // Assert
        capturedTask.Should().NotBeNull();
        capturedTask!.State.Should().Be(TaskState.Completed);
        capturedTask.CompletedAt.Should().NotBeNull();
        capturedTask.ReminderId.Should().BeNull();

        // Verify reminder repository was never called
        _mockReminderRepository.Verify(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CompleteTaskWithUnityAsync_WithStaleReminderId_ClearsReference()
    {
        // Arrange - Task has ReminderId but reminder doesn't exist (stale reference)
        var existingTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "Task with Stale Reminder",
            State = TaskState.InProgress,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            ReminderId = "deleted-reminder"
        };

        TaskDocument? capturedTask = null;

        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "task-123"))
            .ReturnsAsync(existingTask);

        _mockReminderRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "deleted-reminder"))
            .ReturnsAsync((TaskReminderDocument?)null);

        _mockRepository
            .Setup(r => r.CompleteWithUnityAsync(It.IsAny<TaskDocument>(), null))
            .Callback<TaskDocument, TaskReminderDocument?>((t, r) => capturedTask = t)
            .ReturnsAsync((TaskDocument t, TaskReminderDocument? r) => t);

        // Act
        var result = await _taskService.CompleteTaskWithUnityAsync(TestUserId, "task-123");

        // Assert - Stale ReminderId should be cleared
        capturedTask.Should().NotBeNull();
        capturedTask!.ReminderId.Should().BeNull("Stale ReminderId should be cleared");
        capturedTask.State.Should().Be(TaskState.Completed);
    }

    [Fact]
    public async Task CompleteTaskWithUnityAsync_TaskNotFound_ThrowsTaskNotFoundException()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "nonexistent"))
            .ReturnsAsync((TaskDocument?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TaskNotFoundException>(
            () => _taskService.CompleteTaskWithUnityAsync(TestUserId, "nonexistent"));

        exception.TaskId.Should().Be("nonexistent");
    }

    [Fact]
    public async Task CompleteTaskWithUnityAsync_WithReminder_UpdatesLastModifiedAtOnBoth()
    {
        // Arrange
        var originalTaskLastModified = DateTime.UtcNow.AddDays(-1);
        var originalReminderLastModified = DateTime.UtcNow.AddDays(-1);
        var reminderId = "reminder-123";

        var existingTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "Task with Reminder",
            State = TaskState.InProgress,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            LastModifiedAt = originalTaskLastModified,
            ReminderId = reminderId
        };

        var existingReminder = new TaskReminderDocument
        {
            Id = reminderId,
            UserId = TestUserId,
            TaskId = "task-123",
            TaskName = "Task with Reminder",
            ScheduledTime = DateTime.UtcNow.AddHours(1),
            IsDismissed = false,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            LastModifiedAt = originalReminderLastModified
        };

        TaskDocument? capturedTask = null;
        TaskReminderDocument? capturedReminder = null;

        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "task-123"))
            .ReturnsAsync(existingTask);

        _mockReminderRepository
            .Setup(r => r.GetByIdAsync(TestUserId, reminderId))
            .ReturnsAsync(existingReminder);

        _mockRepository
            .Setup(r => r.CompleteWithUnityAsync(It.IsAny<TaskDocument>(), It.IsAny<TaskReminderDocument?>()))
            .Callback<TaskDocument, TaskReminderDocument?>((t, r) =>
            {
                capturedTask = t;
                capturedReminder = r;
            })
            .ReturnsAsync((TaskDocument t, TaskReminderDocument? r) => t);

        // Act
        var result = await _taskService.CompleteTaskWithUnityAsync(TestUserId, "task-123");

        // Assert - Task LastModifiedAt
        capturedTask.Should().NotBeNull();
        capturedTask!.LastModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        capturedTask.LastModifiedAt.Should().BeAfter(originalTaskLastModified);

        // Assert - Reminder LastModifiedAt
        capturedReminder.Should().NotBeNull();
        capturedReminder!.LastModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        capturedReminder.LastModifiedAt.Should().BeAfter(originalReminderLastModified);
    }

    [Fact]
    public async Task CompleteTaskWithUnityAsync_WithoutReminder_UpdatesTaskLastModifiedAt()
    {
        // Arrange
        var originalLastModified = DateTime.UtcNow.AddDays(-1);

        var existingTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "Task without Reminder",
            State = TaskState.InProgress,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            LastModifiedAt = originalLastModified,
            ReminderId = null
        };

        TaskDocument? capturedTask = null;

        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "task-123"))
            .ReturnsAsync(existingTask);

        _mockRepository
            .Setup(r => r.CompleteWithUnityAsync(It.IsAny<TaskDocument>(), null))
            .Callback<TaskDocument, TaskReminderDocument?>((t, r) => capturedTask = t)
            .ReturnsAsync((TaskDocument t, TaskReminderDocument? r) => t);

        // Act
        var result = await _taskService.CompleteTaskWithUnityAsync(TestUserId, "task-123");

        // Assert
        capturedTask.Should().NotBeNull();
        capturedTask!.LastModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        capturedTask.LastModifiedAt.Should().BeAfter(originalLastModified);
    }

    #endregion

    #region UpdateStateAsync Tests (Story 6-5)

    [Fact]
    public async Task UpdateStateAsync_OnDeckToInProgress_UpdatesState()
    {
        // Arrange
        var existingTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "My Task",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "task-123"))
            .ReturnsAsync(existingTask);
        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<TaskDocument>()))
            .ReturnsAsync((TaskDocument t) => t);

        // Act
        var result = await _taskService.UpdateStateAsync(TestUserId, "task-123", TaskState.InProgress);

        // Assert
        result.State.Should().Be(TaskState.InProgress);
        result.CompletedAt.Should().BeNull();
    }

    [Fact]
    public async Task UpdateStateAsync_InProgressToCompleted_SetsCompletedAt()
    {
        // Arrange
        var existingTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "My Task",
            State = TaskState.InProgress,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            CompletedAt = null
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "task-123"))
            .ReturnsAsync(existingTask);
        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<TaskDocument>()))
            .ReturnsAsync((TaskDocument t) => t);

        // Act
        var result = await _taskService.UpdateStateAsync(TestUserId, "task-123", TaskState.Completed);

        // Assert
        result.State.Should().Be(TaskState.Completed);
        result.CompletedAt.Should().NotBeNull();
        result.CompletedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UpdateStateAsync_CompletedToOnDeck_ClearsCompletedAt()
    {
        // Arrange
        var existingTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "My Task",
            State = TaskState.Completed,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            CompletedAt = DateTime.UtcNow.AddHours(-1)
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "task-123"))
            .ReturnsAsync(existingTask);
        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<TaskDocument>()))
            .ReturnsAsync((TaskDocument t) => t);

        // Act
        var result = await _taskService.UpdateStateAsync(TestUserId, "task-123", TaskState.OnDeck);

        // Assert
        result.State.Should().Be(TaskState.OnDeck);
        result.CompletedAt.Should().BeNull();
    }

    [Fact]
    public async Task UpdateStateAsync_SameState_ReturnsWithoutUpdate_Idempotent()
    {
        // Arrange
        var existingTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "My Task",
            State = TaskState.InProgress,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "task-123"))
            .ReturnsAsync(existingTask);

        // Act
        var result = await _taskService.UpdateStateAsync(TestUserId, "task-123", TaskState.InProgress);

        // Assert
        result.State.Should().Be(TaskState.InProgress);
        // Verify UpdateAsync was never called (idempotent - no change needed)
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<TaskDocument>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStateAsync_FromDeleted_ThrowsInvalidStateTransitionException()
    {
        // Arrange
        var existingTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "Deleted Task",
            State = TaskState.Deleted,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "task-123"))
            .ReturnsAsync(existingTask);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidStateTransitionException>(
            () => _taskService.UpdateStateAsync(TestUserId, "task-123", TaskState.OnDeck));

        exception.TaskId.Should().Be("task-123");
        exception.CurrentState.Should().Be(TaskState.Deleted);
        exception.AttemptedAction.Should().Contain("OnDeck");
    }

    [Fact]
    public async Task UpdateStateAsync_ToDeletedFromDeleted_IsIdempotent()
    {
        // Arrange
        var existingTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "Already Deleted",
            State = TaskState.Deleted,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "task-123"))
            .ReturnsAsync(existingTask);

        // Act
        var result = await _taskService.UpdateStateAsync(TestUserId, "task-123", TaskState.Deleted);

        // Assert - No exception, returns task as-is
        result.State.Should().Be(TaskState.Deleted);
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<TaskDocument>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStateAsync_TaskNotFound_ThrowsTaskNotFoundException()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "nonexistent"))
            .ReturnsAsync((TaskDocument?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TaskNotFoundException>(
            () => _taskService.UpdateStateAsync(TestUserId, "nonexistent", TaskState.InProgress));

        exception.TaskId.Should().Be("nonexistent");
    }

    [Fact]
    public async Task UpdateStateAsync_WithInvalidState_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _taskService.UpdateStateAsync(TestUserId, "task-123", "InvalidState"));

        exception.Message.Should().Contain("OnDeck");
    }

    [Fact]
    public async Task UpdateStateAsync_ToCompletedWithReminder_UsesUnityToCompleteAndDismiss()
    {
        // Arrange
        var reminderId = "reminder-123";
        var existingTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "Task with Reminder",
            State = TaskState.InProgress,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            ReminderId = reminderId
        };

        var existingReminder = new TaskReminderDocument
        {
            Id = reminderId,
            UserId = TestUserId,
            TaskId = "task-123",
            TaskName = "Task with Reminder",
            ScheduledTime = DateTime.UtcNow.AddHours(1),
            IsDismissed = false
        };

        TaskDocument? capturedTask = null;
        TaskReminderDocument? capturedReminder = null;

        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "task-123"))
            .ReturnsAsync(existingTask);

        _mockReminderRepository
            .Setup(r => r.GetByIdAsync(TestUserId, reminderId))
            .ReturnsAsync(existingReminder);

        _mockRepository
            .Setup(r => r.CompleteWithUnityAsync(It.IsAny<TaskDocument>(), It.IsAny<TaskReminderDocument?>()))
            .Callback<TaskDocument, TaskReminderDocument?>((t, r) =>
            {
                capturedTask = t;
                capturedReminder = r;
            })
            .ReturnsAsync((TaskDocument t, TaskReminderDocument? r) => t);

        // Act
        var result = await _taskService.UpdateStateAsync(TestUserId, "task-123", TaskState.Completed);

        // Assert - Task completed
        capturedTask.Should().NotBeNull();
        capturedTask!.State.Should().Be(TaskState.Completed);
        capturedTask.CompletedAt.Should().NotBeNull();

        // Assert - Reminder dismissed
        capturedReminder.Should().NotBeNull();
        capturedReminder!.IsDismissed.Should().BeTrue();
        capturedReminder.DismissedAt.Should().NotBeNull();

        // Assert - Unity path used
        _mockRepository.Verify(r => r.CompleteWithUnityAsync(It.IsAny<TaskDocument>(), It.IsAny<TaskReminderDocument>()), Times.Once);
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<TaskDocument>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStateAsync_ToDeletedWithReminder_UsesUnityToDeleteAndDismiss()
    {
        // Arrange
        var reminderId = "reminder-123";
        var existingTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "Task with Reminder",
            State = TaskState.InProgress,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            ReminderId = reminderId
        };

        var existingReminder = new TaskReminderDocument
        {
            Id = reminderId,
            UserId = TestUserId,
            TaskId = "task-123",
            TaskName = "Task with Reminder",
            ScheduledTime = DateTime.UtcNow.AddHours(1),
            IsDismissed = false
        };

        TaskDocument? capturedTask = null;
        TaskReminderDocument? capturedReminder = null;

        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "task-123"))
            .ReturnsAsync(existingTask);

        _mockReminderRepository
            .Setup(r => r.GetByIdAsync(TestUserId, reminderId))
            .ReturnsAsync(existingReminder);

        _mockRepository
            .Setup(r => r.DeleteWithUnityAsync(It.IsAny<TaskDocument>(), It.IsAny<TaskReminderDocument?>()))
            .Callback<TaskDocument, TaskReminderDocument?>((t, r) =>
            {
                capturedTask = t;
                capturedReminder = r;
            })
            .Returns(Task.CompletedTask);

        // Act
        var result = await _taskService.UpdateStateAsync(TestUserId, "task-123", TaskState.Deleted);

        // Assert - Task deleted
        capturedTask.Should().NotBeNull();
        capturedTask!.State.Should().Be(TaskState.Deleted);

        // Assert - Reminder dismissed
        capturedReminder.Should().NotBeNull();
        capturedReminder!.IsDismissed.Should().BeTrue();
        capturedReminder.DismissedAt.Should().NotBeNull();

        // Assert - Unity path used
        _mockRepository.Verify(r => r.DeleteWithUnityAsync(It.IsAny<TaskDocument>(), It.IsAny<TaskReminderDocument>()), Times.Once);
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<TaskDocument>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStateAsync_ToCompletedWithAlreadyDismissedReminder_UsesRegularUpdate()
    {
        // Arrange
        var reminderId = "reminder-123";
        var existingTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "Task with Dismissed Reminder",
            State = TaskState.InProgress,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            ReminderId = reminderId
        };

        var dismissedReminder = new TaskReminderDocument
        {
            Id = reminderId,
            UserId = TestUserId,
            TaskId = "task-123",
            TaskName = "Task with Dismissed Reminder",
            ScheduledTime = DateTime.UtcNow.AddHours(-1),
            IsDismissed = true,
            DismissedAt = DateTime.UtcNow.AddMinutes(-30)
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "task-123"))
            .ReturnsAsync(existingTask);

        _mockReminderRepository
            .Setup(r => r.GetByIdAsync(TestUserId, reminderId))
            .ReturnsAsync(dismissedReminder);

        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<TaskDocument>()))
            .ReturnsAsync((TaskDocument t) => t);

        // Act
        var result = await _taskService.UpdateStateAsync(TestUserId, "task-123", TaskState.Completed);

        // Assert - Regular update path used (reminder already dismissed)
        result.State.Should().Be(TaskState.Completed);
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<TaskDocument>()), Times.Once);
        _mockRepository.Verify(r => r.CompleteWithUnityAsync(It.IsAny<TaskDocument>(), It.IsAny<TaskReminderDocument?>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStateAsync_ToCompletedWithoutReminder_UsesRegularUpdate()
    {
        // Arrange
        var existingTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "Task without Reminder",
            State = TaskState.InProgress,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            ReminderId = null
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "task-123"))
            .ReturnsAsync(existingTask);

        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<TaskDocument>()))
            .ReturnsAsync((TaskDocument t) => t);

        // Act
        var result = await _taskService.UpdateStateAsync(TestUserId, "task-123", TaskState.Completed);

        // Assert - Regular update path used (no reminder)
        result.State.Should().Be(TaskState.Completed);
        result.CompletedAt.Should().NotBeNull();
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<TaskDocument>()), Times.Once);
        _mockRepository.Verify(r => r.CompleteWithUnityAsync(It.IsAny<TaskDocument>(), It.IsAny<TaskReminderDocument?>()), Times.Never);
        _mockReminderRepository.Verify(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStateAsync_UpdatesLastModifiedAt()
    {
        // Arrange
        var originalLastModified = DateTime.UtcNow.AddDays(-1);
        var existingTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "My Task",
            State = TaskState.OnDeck,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            LastModifiedAt = originalLastModified
        };

        TaskDocument? capturedTask = null;

        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "task-123"))
            .ReturnsAsync(existingTask);

        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<TaskDocument>()))
            .Callback<TaskDocument>(t => capturedTask = t)
            .ReturnsAsync((TaskDocument t) => t);

        // Act
        var result = await _taskService.UpdateStateAsync(TestUserId, "task-123", TaskState.InProgress);

        // Assert
        capturedTask.Should().NotBeNull();
        capturedTask!.LastModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        capturedTask.LastModifiedAt.Should().BeAfter(originalLastModified);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task UpdateStateAsync_WithInvalidUserId_ThrowsArgumentException(string? userId)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _taskService.UpdateStateAsync(userId!, "task-123", TaskState.InProgress));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task UpdateStateAsync_WithInvalidTaskId_ThrowsArgumentException(string? taskId)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _taskService.UpdateStateAsync(TestUserId, taskId!, TaskState.InProgress));
    }

    #endregion

    #region DeleteTaskWithUnityAsync Tests

    [Fact]
    public async Task DeleteTaskWithUnityAsync_WithReminder_UpdatesLastModifiedAtOnBoth()
    {
        // Arrange
        var originalTaskLastModified = DateTime.UtcNow.AddDays(-1);
        var originalReminderLastModified = DateTime.UtcNow.AddDays(-1);
        var reminderId = "reminder-123";

        var existingTask = new TaskDocument
        {
            Id = "task-123",
            UserId = TestUserId,
            Name = "Task with Reminder",
            State = TaskState.InProgress,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            LastModifiedAt = originalTaskLastModified,
            ReminderId = reminderId
        };

        var existingReminder = new TaskReminderDocument
        {
            Id = reminderId,
            UserId = TestUserId,
            TaskId = "task-123",
            TaskName = "Task with Reminder",
            ScheduledTime = DateTime.UtcNow.AddHours(1),
            IsDismissed = false,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            LastModifiedAt = originalReminderLastModified
        };

        TaskDocument? capturedTask = null;
        TaskReminderDocument? capturedReminder = null;

        _mockRepository
            .Setup(r => r.GetByIdAsync(TestUserId, "task-123"))
            .ReturnsAsync(existingTask);

        _mockReminderRepository
            .Setup(r => r.GetByIdAsync(TestUserId, reminderId))
            .ReturnsAsync(existingReminder);

        _mockRepository
            .Setup(r => r.DeleteWithUnityAsync(It.IsAny<TaskDocument>(), It.IsAny<TaskReminderDocument?>()))
            .Callback<TaskDocument, TaskReminderDocument?>((t, r) =>
            {
                capturedTask = t;
                capturedReminder = r;
            })
            .Returns(Task.CompletedTask);

        // Act
        await _taskService.DeleteTaskWithUnityAsync(TestUserId, "task-123");

        // Assert - Task LastModifiedAt
        capturedTask.Should().NotBeNull();
        capturedTask!.LastModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        capturedTask.LastModifiedAt.Should().BeAfter(originalTaskLastModified);
        capturedTask.State.Should().Be(TaskState.Deleted);

        // Assert - Reminder LastModifiedAt
        capturedReminder.Should().NotBeNull();
        capturedReminder!.LastModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        capturedReminder.LastModifiedAt.Should().BeAfter(originalReminderLastModified);
        capturedReminder.IsDismissed.Should().BeTrue();
    }

    #endregion
}
