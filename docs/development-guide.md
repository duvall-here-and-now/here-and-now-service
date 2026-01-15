# Here and Now Service - Development Guide

**Date:** 2026-01-15

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
│   └── Services/                     # Business logic
├── Web/HereAndNow.Web/               # API layer
│   ├── Controllers/                  # REST endpoints
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

### Adding a New Task Feature

1. **Add domain model** (if needed):
   ```
   Task/HereAndNow.Task/Models/NewModel.cs
   ```

2. **Add exception** (if needed):
   ```
   Task/HereAndNow.Task/Models/Exceptions/NewException.cs
   ```

3. **Add repository interface**:
   ```
   Task/HereAndNow.Task/Repositories/INewRepository.cs
   ```

4. **Add repository implementation**:
   ```
   Task/HereAndNow.Task/Repositories/NewRepository.cs
   ```

5. **Add service interface**:
   ```
   Task/HereAndNow.Task/Services/INewService.cs
   ```

6. **Add service implementation**:
   ```
   Task/HereAndNow.Task/Services/NewService.cs
   ```

7. **Add DTOs**:
   ```
   Web/HereAndNow.Web/DTOs/CreateNewDto.cs
   Web/HereAndNow.Web/DTOs/NewDto.cs
   ```

8. **Add mapper**:
   ```
   Web/HereAndNow.Web/Mappers/NewMapper.cs
   ```

9. **Add controller endpoints**:
   ```
   Web/HereAndNow.Web/Controllers/NewController.cs
   ```

10. **Register in DI** (Program.cs):
    ```csharp
    builder.Services.AddSingleton<INewRepository, NewRepository>();
    builder.Services.AddScoped<INewService, NewService>();
    ```

11. **Add tests**:
    ```
    Web/HereAndNow.Web.Tests/Controllers/NewControllerTests.cs
    Web/HereAndNow.Web.Tests/Services/NewServiceTests.cs
    ```

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
/// Creates a new task, optionally with an associated reminder.
/// </summary>
/// <param name="dto">The task creation request</param>
/// <returns>The created task</returns>
[HttpPost]
public async Task<ActionResult<TaskDto>> CreateTask([FromBody] CreateTaskDto dto)
```

### JSON Property Names

Use `[JsonPropertyName]` for explicit JSON property names:

```csharp
[JsonPropertyName("scheduledTime")]
public DateTime ScheduledTime { get; set; }
```

## Testing API Endpoints

### Using curl

```bash
# Public endpoint (no auth)
curl http://localhost:6060/api/messages/public

# Protected endpoint (requires token)
curl -H "Authorization: Bearer YOUR_TOKEN" \
  http://localhost:6060/api/messages/protected

# Create a task
curl -X POST http://localhost:6060/api/v1/tasks \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name": "Buy groceries", "scheduledTime": "2026-01-20T10:00:00Z"}'

# Get all tasks
curl "http://localhost:6060/api/v1/tasks?orderBy=createdAt&direction=desc" \
  -H "Authorization: Bearer YOUR_TOKEN"

# Complete a task (Unity operation)
curl -X PUT http://localhost:6060/api/v1/tasks/{taskId}/complete \
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

---

_Generated using BMAD Method `document-project` workflow_
_Last Updated: 2026-01-15_
