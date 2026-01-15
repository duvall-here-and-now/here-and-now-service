# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is an ASP.NET Core 8 API service for task management with reminders, featuring Auth0 authentication and Azure Cosmos DB persistence. The project is structured as a multi-project solution following Clean Architecture with three layers: Message (demo), Task (core business logic), and Web (API).

## Solution Structure

The solution follows a clean architecture pattern with three main assemblies:

- **HereAndNow.Message** (`/Message/HereAndNow.Message/`)
  - Demo business logic assembly (Auth0 sample)
  - Models: `Message`
  - Service interfaces: `IMessageService`
  - Service implementation: `MessageService` (returns static messages)
  - Pure business logic with no web dependencies

- **HereAndNow.Task** (`/Task/HereAndNow.Task/`)
  - Core business logic assembly with CosmosDB persistence
  - Models: `TaskDocument`, `TaskReminderDocument`, `TaskState`, `PagedResult`
  - Exceptions: `TaskNotFoundException`, `ReminderNotFoundException`, `ReminderAlreadyExistsException`, `ReminderAlreadyDismissedException`, `InvalidScheduledTimeException`, `UnityTransactionFailedException`
  - Repository interfaces: `ITaskRepository`, `ITaskReminderRepository`
  - Repository implementations: `TaskRepository`, `TaskReminderRepository` (CosmosDB)
  - Service interfaces: `ITaskService`, `ITaskReminderService`
  - Service implementations: `TaskService`, `TaskReminderService`
  - References: Microsoft.Azure.Cosmos, Newtonsoft.Json

- **HereAndNow.Web** (`/Web/HereAndNow.Web/`)
  - ASP.NET Core Web API project containing controllers and middleware
  - Controllers: `MessagesController`, `TasksController`, `RemindersController`, `ErrorController`
  - DTOs: `CreateTaskDto`, `UpdateTaskDto`, `TaskDto`, `PagedTasksDto`, `CreateReminderDto`, `SnoozeReminderDto`, `TaskReminderDto`, `ErrorResponseDto`
  - Mappers: `TaskMapper`, `ReminderMapper`
  - Validation: `FutureTimeValidationAttribute`
  - Custom middlewares: `ErrorHandlerMiddleware`, `SecureHeadersMiddleware`
  - Uses dotenv.net for environment variable management

- **HereAndNow.Web.Tests** (`/Web/HereAndNow.Web.Tests/`)
  - Test project using xUnit, Moq, FluentAssertions
  - Unit tests for controllers and services
  - Integration tests with `Microsoft.AspNetCore.Mvc.Testing`

## Common Development Commands

### Build and Run
```bash
# Build entire solution
dotnet build HereAndNow.sln

# Build specific configuration
dotnet build HereAndNow.sln --configuration Release

# Run the web API
dotnet run --project Web/HereAndNow.Web/HereAndNow.Web.csproj

# Restore packages
dotnet restore
```

### Testing
```bash
# Run all tests
dotnet test

# Run tests with verbose output
dotnet test --logger "console;verbosity=detailed"

# Run specific test by name filter
dotnet test --filter "FullyQualifiedName~TestMethodName"

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test project
dotnet test Web/HereAndNow.Web.Tests/HereAndNow.Web.Tests.csproj

# Watch mode for continuous testing during development
dotnet watch test --project Web/HereAndNow.Web.Tests/HereAndNow.Web.Tests.csproj
```

### Publishing
```bash
# Publish for deployment
dotnet publish Web/HereAndNow.Web/HereAndNow.Web.csproj -c Release -o ./publish
```

## Configuration and Environment

The application uses environment variables loaded via dotenv.net.

### Required Variables
- `PORT` - Port number for the web server
- `CLIENT_ORIGIN_URL` - CORS origin URL for the frontend (comma-separated for multiple)
- `AUTH0_DOMAIN` - Auth0 domain for JWT validation
- `AUTH0_AUDIENCE` - Auth0 API audience identifier

### Optional Variables (Required for Task features)
- `COSMOS_CONNECTION_STRING` - Azure Cosmos DB connection string
- `COSMOS_DATABASE_NAME` - Database name (default: HereAndNow)
- `COSMOS_CONTAINER_NAME` - Container name (default: Tasks)

Configuration files:
- `.env` - Local environment variables (not committed to git)

## Authentication & Authorization Architecture

The application uses Auth0 for JWT-based authentication:
- JWT Bearer tokens validated against Auth0 authority
- Configured in `Program.cs` with `AddJwtBearer`
- Swagger UI includes Bearer token authentication
- All Task and Reminder endpoints require authentication (`[Authorize]` attribute)
- Public endpoint (`/api/messages/public`) does not require authentication
- CORS configured to accept requests from configured client origin

## API Endpoints

### Messages (Demo)
| Endpoint | Auth | Description |
|----------|------|-------------|
| GET /api/messages/public | None | Returns public message |
| GET /api/messages/protected | JWT | Returns protected message |
| GET /api/messages/admin | JWT | Returns admin message |

### Tasks
| Endpoint | Auth | Description |
|----------|------|-------------|
| GET /api/v1/tasks | JWT | List tasks (paginated, filterable, sortable) |
| POST /api/v1/tasks | JWT | Create task (optionally with reminder) |
| GET /api/v1/tasks/{taskId} | JWT | Get task by ID |
| PUT /api/v1/tasks/{taskId} | JWT | Update task (Unity: syncs reminder if name changes) |
| PUT /api/v1/tasks/{taskId}/complete | JWT | Complete task (Unity: dismisses reminder) |
| DELETE /api/v1/tasks/{taskId} | JWT | Soft-delete task (Unity: dismisses reminder) |

### Reminders
| Endpoint | Auth | Description |
|----------|------|-------------|
| GET /api/v1/reminders | JWT | List active reminders |
| POST /api/v1/tasks/{taskId}/reminder | JWT | Create reminder for task |
| GET /api/v1/reminders/{reminderId} | JWT | Get reminder by ID |
| PUT /api/v1/reminders/{reminderId}/snooze | JWT | Snooze reminder to new time |
| PUT /api/v1/reminders/{reminderId}/dismiss | JWT | Dismiss reminder |

## Data Storage Architecture

### Azure Cosmos DB Design
- **Database:** HereAndNow
- **Container:** Tasks
- **Partition Key:** `/userId` (enables efficient per-user queries)
- **Document Types:** Task, TaskReminder (type discriminator pattern)

### Unity Pattern (Atomic Operations)

The "Unity" pattern uses CosmosDB transactional batches to atomically update Task and Reminder documents together:

```csharp
var batch = _container.CreateTransactionalBatch(new PartitionKey(task.UserId));
batch.ReplaceItem(task.Id, task);
batch.ReplaceItem(reminder.Id, reminder);
await batch.ExecuteAsync();
```

Unity operations:
- **CompleteWithUnityAsync** - Complete task, dismiss reminder atomically
- **DeleteWithUnityAsync** - Soft-delete task, dismiss reminder atomically
- **UpdateWithReminderSyncAsync** - Update task name, sync to reminder's denormalized `TaskName`

## Dependency Injection Pattern

Services are registered in `Program.cs` with appropriate lifetimes:
- `IMessageService` → `MessageService` (Scoped)
- `ITaskService` → `TaskService` (Scoped)
- `ITaskReminderService` → `TaskReminderService` (Scoped)
- `ITaskRepository` → `TaskRepository` (Singleton)
- `ITaskReminderRepository` → `TaskReminderRepository` (Singleton)
- `CosmosClient` → Singleton (shared across repositories)

## .NET 8 Standards

This project follows .NET 8 best practices:
- **File-scoped namespaces** - Used throughout the codebase
- **Nullable reference types enabled** - All projects have `<Nullable>enable</Nullable>`
- **Implicit usings enabled** - Common namespaces imported automatically
- **XML documentation generation** - Enabled for the Web project
- **Modern C# patterns** - Pattern matching, record types, init-only properties
- **[JsonPropertyName]** - Explicit JSON serialization control

## Code Review Requirements

For large code changes (>100 lines or multiple files), invoke the .NET code review agent located at `.github/agents/dotnet-code-reviewer.md`. This is required for:
- Major refactoring efforts
- New feature implementations spanning multiple files
- Changes to core business logic or service patterns
- Adding new API endpoints
- Changes impacting authentication or authorization
- Performance-critical code modifications
- CosmosDB query or transaction changes

The code reviewer enforces .NET 8 best practices including proper async/await patterns, dependency injection, security considerations, and SOLID principles.

## Deployment

The project uses GitHub Actions for CI/CD to Azure Web Apps:
- Workflow file: `.github/workflows/main_here-and-now-service.yml`
- Triggers on push to `main` branch
- Pipeline: Build → Test → Publish → Deploy to Azure App Service
- **Quality Gate**: Tests must pass before deployment proceeds
- Test results published to GitHub Actions UI with detailed reports
- Code coverage reports uploaded as workflow artifacts
- Uses publish profile stored in GitHub secrets
- The `clean: true` flag ensures old artifacts are removed during deployment

### Azure Resources Required
- Azure Web App (here-and-now-service)
- Azure Cosmos DB account with HereAndNow database and Tasks container

## Swagger/OpenAPI

Swagger UI is available at `/swagger` when the application is running. It includes:
- Auto-generated API documentation from XML comments
- JWT Bearer token authentication support
- Request/response schema documentation
- Interactive API testing interface

## Documentation

For detailed documentation, see the `docs/` folder:
- `docs/index.md` - Master documentation index
- `docs/architecture.md` - Detailed architecture, Unity pattern, CosmosDB design
- `docs/api-contracts.md` - Complete API documentation
- `docs/data-models.md` - Domain models, DTOs, exceptions
- `docs/development-guide.md` - Development workflow
- `docs/deployment-guide.md` - Azure deployment guide
