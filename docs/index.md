# Here and Now Service - Documentation Index

**Type:** Monolith Backend API
**Primary Language:** C# (.NET 8.0)
**Architecture:** Clean Architecture (3-Layer)
**Last Updated:** 2026-01-15

## Project Overview

Here and Now Service is an ASP.NET Core 8.0 REST API for task management with reminders. The service provides CRUD operations for tasks and reminders, with Auth0 JWT authentication and Azure Cosmos DB persistence.

## Quick Reference

- **Tech Stack:** ASP.NET Core 8.0, Azure Cosmos DB, Auth0 JWT, Swagger
- **Entry Point:** `Web/HereAndNow.Web/Program.cs`
- **Architecture Pattern:** Clean Architecture (Message → Task → Web layers)
- **Data Storage:** Azure Cosmos DB (NoSQL with `/userId` partition key)
- **Deployment:** Azure Web Apps via GitHub Actions

### API Endpoints Summary

| Controller | Endpoints | Key Features |
|------------|-----------|--------------|
| Messages | 3 | Public/protected/admin demo endpoints |
| Tasks | 6 | CRUD, complete, delete (Unity operations) |
| Reminders | 5 | CRUD, snooze, dismiss |
| **Total** | **14** | |

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
- [API Contracts](./api-contracts.md) - Complete REST API documentation with all 14 endpoints
- [Data Models](./data-models.md) - Domain models, DTOs, exceptions, service/repository patterns

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

## For AI-Assisted Development

This documentation was generated specifically to enable AI agents to understand and extend this codebase.

### When Planning New Features:

**Task/Reminder features:**
→ Reference: `architecture.md` (Unity pattern), `data-models.md`, `api-contracts.md`

**Adding new endpoints:**
→ Follow patterns in `development-guide.md` → "Adding a New Task Feature"

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
7. **DTOs** go in `Web/HereAndNow.Web/DTOs/`
8. **Mappers** go in `Web/HereAndNow.Web/Mappers/`
9. **Validation attributes** go in `Web/HereAndNow.Web/Validation/`
10. **Controllers** go in `Web/HereAndNow.Web/Controllers/`
11. **Tests** go in `Web/HereAndNow.Web.Tests/`

**Message Module (Demo):**
12. **Message models** go in `Message/HereAndNow.Message/Models/`
13. **Message services** go in `Message/HereAndNow.Message/Services/`

### Code Conventions

- File-scoped namespaces
- Nullable reference types enabled
- XML documentation for public APIs
- Structured logging with ILogger
- `[JsonPropertyName]` for explicit JSON serialization
- CosmosDB documents use type discriminator pattern (`"type": "Task"` or `"type": "TaskReminder"`)

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
- **CompleteWithUnityAsync** - Complete task, dismiss reminder
- **DeleteWithUnityAsync** - Soft-delete task, dismiss reminder
- **UpdateWithReminderSyncAsync** - Update task name, sync to reminder

---

_Documentation generated by BMAD Method `document-project` workflow_
_Scan Level: Exhaustive | Mode: Full Rescan_
_Last Updated: 2026-01-15_
