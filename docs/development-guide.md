# Here and Now Service - Development Guide

**Date:** 2026-05-01

---

## Prerequisites

- **.NET 8 SDK** — `dotnet --version` should show `8.x.x`
- **Azure Cosmos DB** — local emulator or Azure account (optional; only needed for task features)
- **Auth0 account** — for JWT validation configuration

---

## Environment Setup

1. Copy `.env.example` to `.env` (or create `.env` from scratch):

```env
PORT=6060
CLIENT_ORIGIN_URL=http://localhost:3000
AUTH0_DOMAIN=<your-auth0-domain>.auth0.com
AUTH0_AUDIENCE=https://<your-api-identifier>

# Optional — enables Cosmos DB features
COSMOS_CONNECTION_STRING=AccountEndpoint=https://localhost:8081/;AccountKey=...
COSMOS_DATABASE_NAME=HereAndNow
COSMOS_CONTAINER_NAME=Tasks
```

2. The service reads `.env` via `dotenv.net` at startup. Without `COSMOS_CONNECTION_STRING`, only the `/api/messages` endpoints will work.

---

## Build and Run

```bash
# Restore packages
dotnet restore

# Build entire solution
dotnet build HereAndNow.sln

# Build Release configuration
dotnet build HereAndNow.sln --configuration Release

# Run the web API (port from .env, default 6060)
dotnet run --project Web/HereAndNow.Web/HereAndNow.Web.csproj

# Watch mode (auto-restart on file changes)
dotnet watch run --project Web/HereAndNow.Web/HereAndNow.Web.csproj
```

After starting, Swagger UI is at `http://localhost:6060/swagger`.

---

## Testing

```bash
# Run all tests
dotnet test

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"

# Run specific test project
dotnet test Web/HereAndNow.Web.Tests/HereAndNow.Web.Tests.csproj
dotnet test Task/HereAndNow.Task.Tests/HereAndNow.Task.Tests.csproj

# Run tests matching a name filter
dotnet test --filter "FullyQualifiedName~CreateTask"

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage"

# Watch mode for continuous testing
dotnet watch test --project Web/HereAndNow.Web.Tests/HereAndNow.Web.Tests.csproj
```

Integration tests use `TestWebApplicationFactory` with mocked Cosmos services — **no real Cosmos connection required**.

---

## Adding a New Command

1. **Create command class** in `Web/HereAndNow.Web/Commands/MyNewCommand.cs`:

```csharp
public class MyNewCommand
{
    [Required]
    [JsonPropertyName("taskId")]
    public string TaskId { get; set; } = string.Empty;

    // ... other fields
}
```

2. **Add service method** to `ITaskService` and implement in `TaskService`.

3. **Add handler** in `CommandsController`:

```csharp
private async Task<IActionResult> HandleMyNewCommandAsync(CommandRequest request, string userId)
{
    MyNewCommand? command;
    try { command = JsonSerializer.Deserialize<MyNewCommand>(...); }
    catch (JsonException) { return BadRequest(...); }

    // validate fields...

    try
    {
        var result = await _taskService.MyNewOperationAsync(userId, ...);
        return Ok(TaskMapper.ToDto(result));
    }
    catch (TaskNotFoundException) { return NotFound(...); }
}
```

4. **Wire the switch** in `ExecuteCommand`:

```csharp
"MyNewCommand" => await HandleMyNewCommandAsync(request, userId),
```

5. **Add tests** in `CommandsControllerTests.cs` and `CommandsApiTests.cs`.

---

## Adding a Recurring Task Feature

1. **Read** `docs/compute-instances-algorithm.md` first — mandatory.
2. Add service method to `IRecurringTaskService`.
3. Implement in `RecurringTaskService`.
4. Add state command handling in `CommandsController` (follow the pattern in existing handlers like `HandleStartRecurringTaskAsync`).
5. Add tests covering: valid transitions, invalid transitions (Scheduled rejection), idempotency.

---

## Coding Conventions

### C# Style

- **File-scoped namespaces** — `namespace HereAndNowService.Controllers;`
- **Nullable enabled** — all types explicitly nullable where needed
- **Implicit usings** — no need to add `using System;` etc.
- **XML docs on public APIs** — Swagger includes them
- **`[JsonPropertyName]`** on all public API-facing properties

### GUID IDs

Always normalize client-supplied GUIDs to lowercase:

```csharp
if (!Guid.TryParse(command.TaskId, out var parsed))
    return BadRequest(CreateErrorResponse("VALIDATION_ERROR", "taskId must be a valid GUID format"));
var taskId = parsed.ToString().ToLowerInvariant();
```

### Timestamps

```csharp
// Always use UTC
var now = DateTime.UtcNow;  // correct
var now = DateTime.Now;     // WRONG — never use this
```

### State Constants

```csharp
task.State = TaskState.Completed;  // correct
task.State = "Completed";           // wrong — use the constant
```

### Structured Logging

```csharp
_logger.LogInformation("Creating task {TaskId} for user {UserId}", taskId, userId);
_logger.LogWarning("Task {TaskId} not found for user {UserId}", taskId, userId);
```

---

## Test Conventions

- **Naming:** `MethodName_Condition_ExpectedResult`
- **Structure:** Arrange / Act / Assert
- **Standard test user:** `auth0|test-user-123`
- **Mocking:** Use Moq for services and repositories
- **Assertions:** Use FluentAssertions (`result.Should().Be(...)`)

```csharp
[Fact]
public async Task CreateTask_WithValidName_Returns201()
{
    // Arrange
    var command = new CreateTaskCommand { TaskId = Guid.NewGuid().ToString(), Name = "Test" };

    // Act
    var result = await _controller.ExecuteCommand(new CommandRequest { Command = "CreateTask", ... });

    // Assert
    result.Should().BeOfType<CreatedAtActionResult>();
}
```

---

## curl Examples

### Create a Task

```bash
curl -X POST http://localhost:6060/api/v1/commands \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "command": "CreateTask",
    "payload": {
      "taskId": "550e8400-e29b-41d4-a716-446655440000",
      "name": "Buy groceries"
    }
  }'
```

### Create Task + Reminder

```bash
curl -X POST http://localhost:6060/api/v1/commands \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "command": "CreateTaskAndTaskReminder",
    "payload": {
      "taskId": "550e8400-e29b-41d4-a716-446655440000",
      "taskReminderId": "660e8400-e29b-41d4-a716-446655440001",
      "name": "Call dentist",
      "scheduledTime": "2026-06-15T09:00:00Z"
    }
  }'
```

### Update Task State

```bash
curl -X POST http://localhost:6060/api/v1/commands \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "command": "UpdateTaskState",
    "payload": {
      "taskId": "550e8400-e29b-41d4-a716-446655440000",
      "state": "InProgress"
    }
  }'
```

### Create Recurring Task Config

```bash
curl -X POST http://localhost:6060/api/v1/commands \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "command": "CreateRecurringTaskConfig",
    "payload": {
      "id": "770e8400-e29b-41d4-a716-446655440010",
      "text": "Morning standup",
      "recurrenceRule": "FREQ=DAILY;BYHOUR=9;BYMINUTE=0;BYSECOND=0",
      "startDateAndTime": "2026-06-01T09:00:00Z"
    }
  }'
```

### Get Recurring Task Instances

```bash
curl "http://localhost:6060/api/v1/recurring-tasks?from=2026-06-01T00:00:00Z&to=2026-06-30T23:59:59Z" \
  -H "Authorization: Bearer $TOKEN"
```

### Get Tasks (filtered)

```bash
curl "http://localhost:6060/api/v1/tasks?state=OnDeck&orderBy=createdAt&direction=asc&skip=0&take=20" \
  -H "Authorization: Bearer $TOKEN"
```

---

## Publishing

```bash
dotnet publish Web/HereAndNow.Web/HereAndNow.Web.csproj -c Release -o ./publish
```

---

## Code Review Requirements

For large changes (>100 lines or multiple files), invoke the .NET code reviewer at `.github/agents/dotnet-code-reviewer.md`. Required for:

- New feature implementations spanning multiple files
- Changes to core business logic or service patterns
- Adding new API endpoints or commands
- Changes to authentication/authorization
- CosmosDB query or transaction changes
