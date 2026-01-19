# Here and Now Service - Development Guide

**Date:** 2026-01-18

## Prerequisites

### Required Software

| Software | Version | Purpose |
|----------|---------|---------|
| .NET SDK | 8.0+ | Build and run the application |
| Git | 2.0+ | Version control |
| IDE | VS 2022 / Rider / VS Code | Development environment |

### Optional Tools

| Tool | Purpose |
|------|---------|
| Postman / Insomnia | API testing |
| Azure CLI | Deployment and CosmosDB operations |
| Azure Cosmos DB Emulator | Local database development |

### Auth0 Account

You need an Auth0 account to test authenticated endpoints:
1. Create an account at [auth0.com](https://auth0.com)
2. Create an API in Auth0 Dashboard
3. Note your domain and audience values

### Azure Cosmos DB (Optional for Full Features)

For Task and Reminder functionality, you need Azure Cosmos DB:
1. Create an Azure Cosmos DB account (or use the emulator)
2. Create a database named `HereAndNow`
3. Create a container named `Tasks` with partition key `/userId`

## Environment Setup

### 1. Clone the Repository

```bash
git clone <repository-url>
cd here-and-now-service
```

### 2. Configure Environment Variables

Create a `.env` file in the project root:

```env
# Server
PORT=6060

# CORS
CLIENT_ORIGIN_URL=http://localhost:3000

# Auth0
AUTH0_DOMAIN=your-tenant.auth0.com
AUTH0_AUDIENCE=https://your-api-identifier

# Cosmos DB (optional - enables Task/Reminder features)
COSMOS_CONNECTION_STRING=AccountEndpoint=https://...
COSMOS_DATABASE_NAME=HereAndNow
COSMOS_CONTAINER_NAME=Tasks
```

**Variable Descriptions:**

| Variable | Required | Example | Description |
|----------|----------|---------|-------------|
| PORT | Yes | 6060 | Port the API listens on |
| CLIENT_ORIGIN_URL | Yes | http://localhost:3000 | Allowed CORS origin(s), comma-separated |
| AUTH0_DOMAIN | Yes | dev-abc123.auth0.com | Your Auth0 tenant domain |
| AUTH0_AUDIENCE | Yes | https://api.example.com | Your Auth0 API identifier |
| COSMOS_CONNECTION_STRING | No | AccountEndpoint=... | CosmosDB connection string |
| COSMOS_DATABASE_NAME | No | HereAndNow | CosmosDB database name (default: HereAndNow) |
| COSMOS_CONTAINER_NAME | No | Tasks | CosmosDB container name (default: Tasks) |

### 3. Restore Dependencies

```bash
dotnet restore
```

## Building the Application

### Build Entire Solution

```bash
dotnet build HereAndNow.sln
```

### Build Release Configuration

```bash
dotnet build HereAndNow.sln --configuration Release
```

### Build Specific Project

```bash
dotnet build Web/HereAndNow.Web/HereAndNow.Web.csproj
```

## Running the Application

### Development Mode

```bash
dotnet run --project Web/HereAndNow.Web/HereAndNow.Web.csproj
```

The API will be available at: `http://localhost:{PORT}`

### Watch Mode (Auto-Reload)

```bash
dotnet watch run --project Web/HereAndNow.Web/HereAndNow.Web.csproj
```

### Accessing Swagger UI

Once running, open your browser to:
```
http://localhost:{PORT}/swagger
```

## Testing

### Run All Tests

```bash
dotnet test
```

### Run Tests with Detailed Output

```bash
dotnet test --logger "console;verbosity=detailed"
```

### Run Tests with Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

Coverage reports are generated in `TestResults/` directories.

### Watch Mode for Testing

```bash
dotnet watch test --project Web/HereAndNow.Web.Tests/HereAndNow.Web.Tests.csproj
```

### Test Categories

| Category | Location | Description |
|----------|----------|-------------|
| Controller Unit Tests | `Controllers/` | Test controller logic with mocked services |
| Service Unit Tests | `Services/` | Test service logic with mocked repositories |
| Integration Tests | `Integration/` | Test full HTTP pipeline |

## Project Structure

```
here-and-now-service/
├── Message/HereAndNow.Message/       # Demo business logic (Auth0 sample)
│   ├── Models/                       # Domain models
│   └── Services/                     # Service interfaces + implementations
├── Task/HereAndNow.Task/             # Core business logic (Tasks + Reminders)
│   ├── Models/                       # Domain models + Exceptions
│   ├── Repositories/                 # CosmosDB data access
│   └── Services/                     # Business logic + State machine
├── Web/HereAndNow.Web/               # API layer
│   ├── Controllers/                  # REST endpoints
│   ├── Commands/                     # ★ Command Pattern (NEW)
│   ├── DTOs/                         # Request/Response objects
│   ├── Mappers/                      # Document ↔ DTO conversion
│   ├── Validation/                   # Custom validation attributes
│   └── Middlewares/                  # Custom middleware
└── Web/HereAndNow.Web.Tests/         # Tests
    ├── Controllers/                  # Controller unit tests
    ├── Services/                     # Service unit tests
    └── Integration/                  # API integration tests
```

## Common Development Tasks

### Adding a New Command (Preferred Pattern)

The Command Pattern is the primary way to add mutations. Follow these steps:

#### 1. Create the Command Class

```csharp
// Web/HereAndNow.Web/Commands/NewFeatureCommand.cs
namespace HereAndNowService.Commands;

public class NewFeatureCommand
{
    [Required]
    [JsonPropertyName("taskId")]
    public string TaskId { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("someField")]
    public string SomeField { get; set; } = string.Empty;
}
```

#### 2. Add Service Method (if needed)

```csharp
// Task/HereAndNow.Task/Services/ITaskService.cs
Task<TaskDocument> NewFeatureAsync(string userId, string taskId, string someField);

// Task/HereAndNow.Task/Services/TaskService.cs
public async Task<TaskDocument> NewFeatureAsync(string userId, string taskId, string someField)
{
    var task = await _taskRepository.GetByIdAsync(userId, taskId)
        ?? throw new TaskNotFoundException(taskId);

    // Business logic here
    task.SomeField = someField;
    task.LastModifiedAt = DateTime.UtcNow;

    return await _taskRepository.UpdateAsync(task);
}
```

#### 3. Add Handler in CommandsController

```csharp
// Web/HereAndNow.Web/Controllers/CommandsController.cs
// Add case in ExecuteCommand method:

case "NewFeature":
    var newFeatureCmd = JsonSerializer.Deserialize<NewFeatureCommand>(
        request.Payload.GetRawText(), _jsonOptions);

    if (newFeatureCmd == null || !TryValidateModel(newFeatureCmd))
        return BadRequest(new ErrorResponseDto("VALIDATION_ERROR", "Invalid payload"));

    var result = await _taskService.NewFeatureAsync(userId, newFeatureCmd.TaskId, newFeatureCmd.SomeField);
    return Ok(TaskMapper.ToDto(result));
```

#### 4. Add Exception Handling (in ErrorHandlerMiddleware if needed)

```csharp
// Web/HereAndNow.Web/Middlewares/ErrorHandlerMiddleware.cs
NewFeatureException ex => (StatusCodes.Status400BadRequest, "NEW_FEATURE_ERROR", ex.Message),
```

#### 5. Add Unit Tests

```csharp
// Web/HereAndNow.Web.Tests/Controllers/CommandsControllerTests.cs
[Fact]
public async Task ExecuteCommand_NewFeature_ReturnsUpdatedTask()
{
    // Arrange
    var command = new CommandRequest
    {
        Command = "NewFeature",
        Payload = JsonSerializer.SerializeToElement(new { taskId = "test-id", someField = "value" })
    };

    _mockTaskService.Setup(s => s.NewFeatureAsync(It.IsAny<string>(), "test-id", "value"))
        .ReturnsAsync(new TaskDocument { Id = "test-id" });

    // Act
    var result = await _controller.ExecuteCommand(command);

    // Assert
    result.Should().BeOfType<OkObjectResult>();
}
```

#### 6. Add Integration Tests

```csharp
// Web/HereAndNow.Web.Tests/Integration/CommandsApiTests.cs
[Fact]
public async Task NewFeature_WithValidPayload_ReturnsOk()
{
    var request = new
    {
        command = "NewFeature",
        payload = new { taskId = "test-id", someField = "value" }
    };

    var response = await _client.PostAsJsonAsync("/api/v1/commands", request);

    response.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

### Adding a Unity Operation

Unity operations atomically update Task and Reminder together:

```csharp
// In TaskRepository
public async Task<TaskDocument> NewUnityOperationAsync(
    TaskDocument task,
    TaskReminderDocument? reminder)
{
    if (reminder == null)
        return await UpdateAsync(task);

    var batch = _container.CreateTransactionalBatch(new PartitionKey(task.UserId));
    batch.ReplaceItem(task.Id, task);
    batch.ReplaceItem(reminder.Id, reminder);

    using var response = await batch.ExecuteAsync();
    if (!response.IsSuccessStatusCode)
        throw new UnityTransactionFailedException(...);

    return response.GetOperationResultAtIndex<TaskDocument>(0).Resource;
}
```

### Adding a Legacy REST Endpoint (Deprecated)

> **Note:** Prefer adding Commands instead. Only add REST endpoints for queries.

1. **Add domain model** (if needed):
   ```
   Task/HereAndNow.Task/Models/NewModel.cs
   ```

2. **Add exception** (if needed):
   ```
   Task/HereAndNow.Task/Models/Exceptions/NewException.cs
   ```

3. **Add repository interface + implementation**
4. **Add service interface + implementation**
5. **Add DTOs**
6. **Add mapper**
7. **Add controller endpoints**
8. **Register in DI** (Program.cs)
9. **Add tests**

### Adding a Custom Validation Attribute

```csharp
// Web/HereAndNow.Web/Validation/NewValidationAttribute.cs
[AttributeUsage(AttributeTargets.Property)]
public class NewValidationAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        // Validation logic
        return ValidationResult.Success;
    }
}
```

## Code Style and Conventions

### File-Scoped Namespaces

Use file-scoped namespaces (C# 10+):

```csharp
namespace HereAndNowService.Controllers;  // Note: semicolon, not braces

public class MyController : ControllerBase
{
    // ...
}
```

### Nullable Reference Types

Nullable reference types are enabled. Use `?` for nullable types:

```csharp
public string? ReminderId { get; set; }  // Nullable string
```

### XML Documentation

Document public APIs with XML comments:

```csharp
/// <summary>
/// Executes a command to modify system state.
/// </summary>
/// <param name="request">The command request containing command type and payload</param>
/// <returns>Command-specific response</returns>
[HttpPost]
public async Task<IActionResult> ExecuteCommand([FromBody] CommandRequest request)
```

### JSON Property Names

Use `[JsonPropertyName]` for explicit JSON property names:

```csharp
[JsonPropertyName("scheduledTime")]
public DateTime ScheduledTime { get; set; }
```

### Command Naming Convention

Commands follow `{Action}{Entity}Command` pattern:

| Command | Pattern |
|---------|---------|
| `CreateTaskCommand` | Create + Task |
| `UpdateTaskNameCommand` | Update + TaskName |
| `UpdateTaskStateCommand` | Update + TaskState |
| `DismissTaskReminderCommand` | Dismiss + TaskReminder |

## Testing API Endpoints

### Using curl (Commands API)

```bash
# Create a task with client-generated ID
curl -X POST http://localhost:6060/api/v1/commands \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "command": "CreateTask",
    "payload": {
      "taskId": "550e8400-e29b-41d4-a716-446655440000",
      "name": "Buy groceries"
    }
  }'

# Create task with reminder atomically
curl -X POST http://localhost:6060/api/v1/commands \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "command": "CreateTaskAndTaskReminder",
    "payload": {
      "taskId": "550e8400-e29b-41d4-a716-446655440000",
      "taskReminderId": "660e8400-e29b-41d4-a716-446655440001",
      "name": "Call dentist",
      "scheduledTime": "2026-01-20T09:00:00Z"
    }
  }'

# Update task name
curl -X POST http://localhost:6060/api/v1/commands \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "command": "UpdateTaskName",
    "payload": {
      "taskId": "550e8400-e29b-41d4-a716-446655440000",
      "name": "Buy groceries and milk"
    }
  }'

# Update task state (complete)
curl -X POST http://localhost:6060/api/v1/commands \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "command": "UpdateTaskState",
    "payload": {
      "taskId": "550e8400-e29b-41d4-a716-446655440000",
      "state": "Completed"
    }
  }'

# Snooze reminder
curl -X POST http://localhost:6060/api/v1/commands \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "command": "UpdateTaskReminderScheduledTime",
    "payload": {
      "taskReminderId": "660e8400-e29b-41d4-a716-446655440001",
      "scheduledTime": "2026-01-25T14:00:00Z"
    }
  }'

# Dismiss reminder
curl -X POST http://localhost:6060/api/v1/commands \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "command": "DismissTaskReminder",
    "payload": {
      "taskReminderId": "660e8400-e29b-41d4-a716-446655440001"
    }
  }'
```

### Using curl (Query APIs)

```bash
# Get all tasks (paginated)
curl "http://localhost:6060/api/v1/tasks?orderBy=createdAt&direction=desc&take=20" \
  -H "Authorization: Bearer YOUR_TOKEN"

# Get task by ID
curl "http://localhost:6060/api/v1/tasks/550e8400-e29b-41d4-a716-446655440000" \
  -H "Authorization: Bearer YOUR_TOKEN"

# Get all reminders
curl "http://localhost:6060/api/v1/reminders" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

### Using Swagger UI

1. Navigate to `http://localhost:6060/swagger`
2. Click "Authorize" button
3. Enter your JWT token
4. Execute endpoints

## Debugging

### Visual Studio / Rider

1. Set `HereAndNow.Web` as startup project
2. Press F5 to start debugging
3. Breakpoints work as expected

### VS Code

1. Install C# extension
2. Create `.vscode/launch.json`:
   ```json
   {
     "version": "0.2.0",
     "configurations": [
       {
         "name": ".NET Core Launch (web)",
         "type": "coreclr",
         "request": "launch",
         "program": "${workspaceFolder}/Web/HereAndNow.Web/bin/Debug/net8.0/HereAndNow.Web.dll",
         "cwd": "${workspaceFolder}/Web/HereAndNow.Web"
       }
     ]
   }
   ```

## Troubleshooting

### Common Issues

| Issue | Solution |
|-------|----------|
| "Config variable missing" | Check `.env` file exists with all required variables |
| 401 on all requests | Verify AUTH0_DOMAIN and AUTH0_AUDIENCE |
| CORS errors | Check CLIENT_ORIGIN_URL matches your frontend |
| Port already in use | Change PORT in `.env` |
| Task endpoints 500 | Check COSMOS_CONNECTION_STRING is set correctly |
| "Container not found" | Create the Tasks container in CosmosDB |
| "UNKNOWN_COMMAND" error | Check command name spelling (case-sensitive) |

### Running Without CosmosDB

If `COSMOS_CONNECTION_STRING` is not set:
- Task and Reminder endpoints will not be available
- Message endpoints will work (static data)
- This is useful for testing Auth0 integration only

### Viewing Logs

Application logs are written to console. For more detailed logs:

```csharp
// In Program.cs or appsettings.json
builder.Logging.SetMinimumLevel(LogLevel.Debug);
```

## Command Pattern Quick Reference

| Command | Payload | Response | Notes |
|---------|---------|----------|-------|
| `CreateTask` | `{ taskId, name }` | TaskDto | Client-generated ID |
| `CreateTaskAndTaskReminder` | `{ taskId, taskReminderId, name, scheduledTime }` | TaskAndReminderDto | Atomic creation |
| `UpdateTaskName` | `{ taskId, name }` | TaskDto | Unity: syncs to reminder |
| `UpdateTaskState` | `{ taskId, state }` | TaskDto | Unity: dismisses reminder on Complete/Delete |
| `UpdateTaskReminderScheduledTime` | `{ taskReminderId, scheduledTime }` | TaskReminderDto | Snooze |
| `DismissTaskReminder` | `{ taskReminderId }` | 204 | Idempotent |

---

_Generated using BMAD Method `document-project` workflow_
_Last Updated: 2026-01-18_
