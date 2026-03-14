# Copilot Instructions

## Project Overview
This is an ASP.NET Core 8 API service for task management with reminders, featuring Auth0 authentication and Azure Cosmos DB persistence.

## Technology Stack
- **Framework**: ASP.NET Core 8.0 (C#)
- **Database**: Azure Cosmos DB (NoSQL)
- **Authentication**: Auth0 JWT Bearer
- **Solution Structure**: Multi-project solution with `Message`, `Task`, and `Web` projects

## Coding Standards

### C# Guidelines
- Follow C# naming conventions (PascalCase for classes/methods, camelCase for local variables)
- Use modern C# features and patterns where appropriate
- Prefer async/await for I/O operations
- Use dependency injection for service management
- Make sure all IDisposable objects are properly disposed of
- Use `[JsonPropertyName]` for explicit JSON property names

### Code Style
- Keep methods focused and single-purpose
- Add XML documentation comments for public APIs
- Use meaningful variable and method names
- Handle exceptions appropriately with proper error messages
- Follow Service-Repository pattern for data access

## Project Structure

### Message Module (Demo)
- `/Message/HereAndNow.Message/Models/` - Domain models
- `/Message/HereAndNow.Message/Services/` - Service interfaces and implementations

### Task Module (Core Business Logic)
- `/Task/HereAndNow.Task/Models/` - Domain models (TaskDocument, TaskReminderDocument)
- `/Task/HereAndNow.Task/Models/Exceptions/` - Custom business exceptions
- `/Task/HereAndNow.Task/Repositories/` - CosmosDB repository interfaces and implementations
- `/Task/HereAndNow.Task/Services/` - Service interfaces and implementations

### Web Module (API Layer)
- `/Web/HereAndNow.Web/Controllers/` - REST API endpoints
- `/Web/HereAndNow.Web/DTOs/` - Request/response data transfer objects
- `/Web/HereAndNow.Web/Mappers/` - Document to DTO mappers
- `/Web/HereAndNow.Web/Validation/` - Custom validation attributes
- `/Web/HereAndNow.Web/Middlewares/` - Custom middleware

### Tests
- `/Web/HereAndNow.Web.Tests/Controllers/` - Controller unit tests
- `/Web/HereAndNow.Web.Tests/Services/` - Service unit tests
- `/Web/HereAndNow.Web.Tests/Integration/` - API integration tests

## API Endpoints

### Messages (Demo)
- `GET /api/messages/public` - No authentication required
- `GET /api/messages/protected` - JWT authentication required
- `GET /api/messages/admin` - JWT authentication required

### Tasks
- `GET /api/v1/tasks` - List tasks (paginated, filterable, sortable)
- `POST /api/v1/tasks` - Create task (optionally with reminder)
- `GET /api/v1/tasks/{taskId}` - Get task by ID
- `PUT /api/v1/tasks/{taskId}` - Update task
- `PUT /api/v1/tasks/{taskId}/complete` - Complete task (Unity operation)
- `DELETE /api/v1/tasks/{taskId}` - Soft-delete task (Unity operation)

### Reminders
- `GET /api/v1/reminders` - List active reminders
- `POST /api/v1/tasks/{taskId}/reminder` - Create reminder for task
- `GET /api/v1/reminders/{reminderId}` - Get reminder by ID
- `PUT /api/v1/reminders/{reminderId}/snooze` - Snooze reminder
- `PUT /api/v1/reminders/{reminderId}/dismiss` - Dismiss reminder

## Key Patterns

### Unity Pattern (Atomic Operations)
When Task and Reminder must be updated together atomically, use CosmosDB transactional batches:

```csharp
var batch = _container.CreateTransactionalBatch(new PartitionKey(task.UserId));
batch.ReplaceItem(task.Id, task);
batch.ReplaceItem(reminder.Id, reminder);
await batch.ExecuteAsync();
```

Unity operations:
- `CompleteWithUnityAsync` - Complete task, dismiss reminder
- `DeleteWithUnityAsync` - Soft-delete task, dismiss reminder
- `UpdateWithReminderSyncAsync` - Update task name, sync to reminder

### CosmosDB Design
- Database: `HereAndNow`
- Container: `Tasks`
- Partition Key: `/userId`
- Document types: `Task`, `TaskReminder` (type discriminator)

## Authentication & Authorization
- This project uses Auth0 for authentication and authorization
- All Task and Reminder endpoints require JWT authentication
- Maintain security best practices when working with authentication code
- Never commit secrets or API keys to version control

## Development Workflow
1. Build the solution before running tests
2. Ensure all changes maintain backward compatibility
3. Test API endpoints after making changes
4. Update relevant documentation when adding new features
5. Follow existing architectural patterns (Service-Repository, Unity)

## Common Commands
- Build: `dotnet build HereAndNow.sln`
- Run: `dotnet run --project Web/HereAndNow.Web/HereAndNow.Web.csproj`
- Test: `dotnet test`
- Restore packages: `dotnet restore`

## Environment Variables
- `PORT` - Server port
- `CLIENT_ORIGIN_URL` - CORS origins (comma-separated)
- `AUTH0_DOMAIN` - Auth0 tenant domain
- `AUTH0_AUDIENCE` - Auth0 API identifier
- `COSMOS_CONNECTION_STRING` - CosmosDB connection (required for Task features)
- `COSMOS_DATABASE_NAME` - Database name (default: HereAndNow)
- `COSMOS_CONTAINER_NAME` - Container name (default: Tasks)

## Code Review Standards

Before submitting pull requests:
1. Follow .NET 8 best practices (see `.github/agents/dotnet-code-reviewer.md`)
2. Use file-scoped namespaces
3. Enable and respect nullable reference types
4. Include proper async/await patterns with CancellationToken
5. Add XML documentation for public APIs
6. Write unit tests for new functionality
7. Follow existing architectural patterns (Service-Repository, Unity)
8. Use transactional batches for atomic Task-Reminder operations

### Code Review Agent

**For large code changes (>100 lines or multiple files), always invoke the .NET code review agent before finalizing:**

The specialized code review agent is located at `.github/agents/dotnet-code-reviewer.md`. This agent enforces .NET 8 best practices and should be consulted for:

- Major refactoring efforts
- New feature implementations spanning multiple files
- Changes to core business logic or data access patterns
- Adding new API endpoints
- Changes that impact authentication or authorization
- Performance-critical code modifications
- CosmosDB query or transaction changes

**How to use the code review agent:**
1. Make your code changes
2. Invoke the `dotnet-code-reviewer` agent with the files you've changed
3. Address any Critical or High severity issues identified
4. Consider Medium severity suggestions for code quality improvements
5. Document any intentional deviations from best practices

This ensures consistency with .NET 8 standards and catches potential issues early in the development process.
