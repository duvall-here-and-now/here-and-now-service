# Here and Now Service - Documentation Index

**Type:** Monolith Backend API
**Primary Language:** C# (.NET 8.0)
**Architecture:** Clean Architecture (3-Layer) + Command Pattern
**Last Updated:** 2026-01-21

## Project Overview

Here and Now Service is an ASP.NET Core 8.0 REST API for task management with reminders. The service provides task and reminder management through a **Command Pattern** API with Auth0 JWT authentication and Azure Cosmos DB persistence.

> **v1.3.0 Update:** The API has evolved to use a **Command Pattern** for all mutations. Commands provide explicit intent, client-generated IDs for optimistic UI, and atomic operations.
>
> **v1.3.1 Update:** `UpdateTaskReminderScheduledTime` no longer validates future time, supporting mobile offline sync scenarios.

## Quick Reference

- **Tech Stack:** ASP.NET Core 8.0, Azure Cosmos DB, Auth0 JWT, Swagger
- **Entry Point:** `Web/HereAndNow.Web/Program.cs`
- **Architecture Pattern:** Clean Architecture + Command Pattern
- **Data Storage:** Azure Cosmos DB (NoSQL with `/userId` partition key)
- **Deployment:** Azure Web Apps via GitHub Actions

### API Overview

| Controller | Endpoints | Purpose |
|------------|-----------|---------|
| **Commands** | 1 (6 commands) | All mutations - CreateTask, UpdateTaskState, etc. |
| Messages | 3 | Public/protected/admin demo endpoints |
| Tasks | 4 | Task queries + legacy complete |
| Reminders | 4 | Reminder queries + legacy endpoints |

### Available Commands

| Command | Description |
|---------|-------------|
| `CreateTask` | Create task with client-generated ID |
| `CreateTaskAndTaskReminder` | Atomic task+reminder creation |
| `UpdateTaskName` | Update name (syncs to reminder) |
| `UpdateTaskState` | State machine with Unity |
| `UpdateTaskReminderScheduledTime` | Reschedule reminder |
| `DismissTaskReminder` | Dismiss reminder (idempotent) |

### Key Commands

| Command | Description |
|---------|-------------|
| `dotnet build HereAndNow.sln` | Build the solution |
| `dotnet test` | Run all tests |
| `dotnet run --project Web/HereAndNow.Web/HereAndNow.Web.csproj` | Start the API |

### Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| PORT | Yes | Server port |
| CLIENT_ORIGIN_URL | Yes | CORS origins (comma-separated) |
| AUTH0_DOMAIN | Yes | Auth0 tenant domain |
| AUTH0_AUDIENCE | Yes | Auth0 API identifier |
| COSMOS_CONNECTION_STRING | No* | CosmosDB connection (required for Task features) |
| COSMOS_DATABASE_NAME | No | Database name (default: HereAndNow) |
| COSMOS_CONTAINER_NAME | No | Container name (default: Tasks) |

## Generated Documentation

### Core Documentation

- [Project Overview](./project-overview.md) - Executive summary and high-level architecture
- [Source Tree Analysis](./source-tree-analysis.md) - Annotated directory structure

### Technical Documentation

- [Architecture](./architecture.md) - Detailed technical architecture, Unity pattern, CosmosDB design
- [API Contracts](./api-contracts.md) - Complete API documentation with Commands and legacy endpoints
- [Data Models](./data-models.md) - Domain models, Commands, DTOs, exceptions

### Operational Documentation

- [Development Guide](./development-guide.md) - Local setup, CosmosDB emulator, testing, coding conventions
- [Deployment Guide](./deployment-guide.md) - CI/CD pipeline, Azure deployment, CosmosDB configuration

## Existing Documentation (Outside docs/)

| Document | Status | Description |
|----------|--------|-------------|
| [README.md](../README.md) | Current | Project readme |
| [CLAUDE.md](../CLAUDE.md) | Current | Claude Code instructions |
| [SWAGGER_SETUP.md](../Web/HereAndNow.Web/SWAGGER_SETUP.md) | Current | Swagger configuration details |
| [copilot-instructions.md](../.github/copilot-instructions.md) | Current | Copilot instructions |

## Getting Started

### Prerequisites

- .NET 8.0 SDK
- Auth0 account
- Azure Cosmos DB account (or emulator for local development)

### Quick Start

```bash
# 1. Clone and navigate
cd here-and-now-service

# 2. Create .env file with required variables
cat > .env << 'EOF'
PORT=6060
CLIENT_ORIGIN_URL=http://localhost:3000
AUTH0_DOMAIN=your-domain.auth0.com
AUTH0_AUDIENCE=https://your-api
COSMOS_CONNECTION_STRING=AccountEndpoint=https://localhost:8081/;AccountKey=...
COSMOS_DATABASE_NAME=HereAndNow
COSMOS_CONTAINER_NAME=Tasks
EOF

# 3. Build and run
dotnet build
dotnet run --project Web/HereAndNow.Web/HereAndNow.Web.csproj

# 4. Access Swagger UI
# Open http://localhost:6060/swagger
```

### Run Tests

```bash
dotnet test
```

### Example: Create a Task with Reminder

```bash
curl -X POST http://localhost:6060/api/v1/commands \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
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
```

## For AI-Assisted Development

This documentation was generated specifically to enable AI agents to understand and extend this codebase.

### When Planning New Features:

**Task/Reminder features:**
→ Reference: `architecture.md` (Unity pattern), `data-models.md`, `api-contracts.md`

**Adding new commands:**
→ Follow patterns in `api-contracts.md` → "Commands Controller"
→ Add command class in `Web/HereAndNow.Web/Commands/`
→ Add handler in `CommandsController.ExecuteCommand()`

**CosmosDB operations:**
→ Reference: `architecture.md` → "Unity Pattern", `data-models.md` → "Repository Layer"

**Deployment changes:**
→ Reference: `deployment-guide.md` → "CosmosDB Setup"

**Understanding codebase structure:**
→ Reference: `source-tree-analysis.md`

### Key Patterns to Follow

**Task Module (Core Business Logic):**
1. **Domain models** go in `Task/HereAndNow.Task/Models/`
2. **Exceptions** go in `Task/HereAndNow.Task/Models/Exceptions/`
3. **Repository interfaces** go in `Task/HereAndNow.Task/Repositories/`
4. **Repository implementations** go in `Task/HereAndNow.Task/Repositories/`
5. **Service interfaces** go in `Task/HereAndNow.Task/Services/`
6. **Service implementations** go in `Task/HereAndNow.Task/Services/`

**Web Module (API Layer):**
7. **Commands** go in `Web/HereAndNow.Web/Commands/`
8. **DTOs** go in `Web/HereAndNow.Web/DTOs/`
9. **Mappers** go in `Web/HereAndNow.Web/Mappers/`
10. **Validation attributes** go in `Web/HereAndNow.Web/Validation/`
11. **Controllers** go in `Web/HereAndNow.Web/Controllers/`
12. **Tests** go in `Web/HereAndNow.Web.Tests/`

**Message Module (Demo):**
13. **Message models** go in `Message/HereAndNow.Message/Models/`
14. **Message services** go in `Message/HereAndNow.Message/Services/`

### Code Conventions

- File-scoped namespaces
- Nullable reference types enabled
- XML documentation for public APIs
- Structured logging with ILogger
- `[JsonPropertyName]` for explicit JSON serialization
- CosmosDB documents use type discriminator pattern (`"type": "Task"` or `"type": "TaskReminder"`)

### Command Pattern

All mutations use the Command Pattern via `POST /api/v1/commands`:

```csharp
// CommandRequest structure
{
  "command": "CommandName",
  "payload": { /* command-specific */ }
}
```

Benefits:
- **Explicit intent** - Commands describe what to do, not HTTP verbs
- **Client-generated IDs** - Enables optimistic UI patterns
- **Single endpoint** - All mutations through one POST endpoint
- **Atomic operations** - CosmosDB transactional batches for consistency

### Unity Pattern (Atomic Operations)

For operations that must update Task and Reminder together atomically:

```csharp
// Use transactional batch
var batch = _container.CreateTransactionalBatch(new PartitionKey(task.UserId));
batch.ReplaceItem(task.Id, task);
batch.ReplaceItem(reminder.Id, reminder);
await batch.ExecuteAsync();
```

Current Unity operations:
- **CreateTaskWithReminderBatchAsync** - Create task + reminder atomically
- **CompleteWithUnityAsync** - Complete task, dismiss reminder
- **DeleteWithUnityAsync** - Soft-delete task, dismiss reminder
- **UpdateWithReminderSyncAsync** - Update task name, sync to reminder

---

_Documentation generated by BMAD Method `document-project` workflow_
_Scan Level: Exhaustive | Mode: Full Rescan_
_Last Updated: 2026-01-21_
