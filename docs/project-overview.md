# Here and Now Service - Project Overview

**Date:** 2026-01-15
**Type:** Backend API Service
**Architecture:** Clean Architecture (3-Layer)

## Executive Summary

Here and Now Service is an ASP.NET Core 8.0 REST API for task management with optional reminders. The service provides a complete CRUD API for tasks and reminders, with Auth0 JWT authentication and Azure Cosmos DB for persistence. It evolved from an Auth0 demo to a production-ready task management backend.

## Project Classification

- **Repository Type:** Monolith (.NET Solution)
- **Project Type:** Backend API
- **Primary Language:** C# (.NET 8.0)
- **Architecture Pattern:** Clean Architecture with 3 layers (Message, Task, Web)
- **Data Storage:** Azure Cosmos DB (NoSQL)
- **Authentication:** Auth0 JWT Bearer tokens
- **Deployment:** Azure Web Apps via GitHub Actions

## Technology Stack Summary

| Category | Technology | Version | Purpose |
|----------|------------|---------|---------|
| Framework | ASP.NET Core | 8.0 | Web API framework |
| Language | C# | 12 | Primary language |
| Database | Azure Cosmos DB | 3.46.1 | NoSQL document storage |
| Authentication | Auth0 JWT | - | Identity & access management |
| API Docs | Swashbuckle | 6.9.0 | Swagger/OpenAPI generation |
| Environment | dotenv.net | 3.2.1 | Environment variable loading |
| Testing | xUnit | 2.9.2 | Unit testing framework |
| Testing | Moq | 4.20.72 | Mocking framework |
| Testing | FluentAssertions | 6.12.2 | Assertion library |
| Testing | Mvc.Testing | 8.0.11 | Integration testing |
| CI/CD | GitHub Actions | - | Build, test, deploy pipeline |
| Hosting | Azure Web Apps | - | Production hosting |

## Key Features

1. **Task Management API**: Full CRUD for tasks with state management
   - Create, read, update, delete tasks
   - State transitions: OnDeck → InProgress → Completed/Deleted
   - Pagination, sorting, and filtering support

2. **Reminder System**: Optional task reminders
   - Associate reminders with tasks
   - Snooze and dismiss functionality
   - Denormalized task names for efficient display

3. **Unity Operations**: Atomic Task-Reminder updates
   - Complete task → auto-dismiss reminder
   - Delete task → auto-dismiss reminder
   - Update task name → sync to reminder

4. **Message API**: Demo endpoints for access levels
   - `/api/messages/public` - No authentication required
   - `/api/messages/protected` - JWT required
   - `/api/messages/admin` - JWT required

5. **JWT Authentication**: Auth0-based token validation
6. **CORS Support**: Configurable cross-origin resource sharing
7. **Security Headers**: Comprehensive security headers middleware
8. **Swagger UI**: Interactive API documentation at `/swagger`

## Architecture Highlights

The solution follows Clean Architecture principles with three main assemblies:

1. **HereAndNow.Message** (Demo Business Logic)
   - Simple domain model (`Message`)
   - Service interface and implementation
   - Static messages for Auth0 demonstration

2. **HereAndNow.Task** (Core Business Logic)
   - Domain models (`TaskDocument`, `TaskReminderDocument`, `TaskState`, `PagedResult`)
   - Custom exceptions for business rule violations
   - Repository interfaces and CosmosDB implementations
   - Service interfaces and implementations
   - Unity pattern for atomic operations

3. **HereAndNow.Web** (Web API Layer)
   - REST controllers (`MessagesController`, `TasksController`, `RemindersController`)
   - DTOs for request/response shaping
   - Mappers for Document↔DTO conversion
   - Custom validation attributes
   - Middlewares for error handling and security

## API Summary

| Controller | Endpoints | Description |
|------------|-----------|-------------|
| Messages | 3 | Demo endpoints (public, protected, admin) |
| Tasks | 6 | Task CRUD + complete/delete Unity operations |
| Reminders | 5 | Reminder CRUD + snooze/dismiss |
| **Total** | **14** | |

## Development Overview

### Prerequisites

- .NET 8.0 SDK
- Auth0 account (for authentication)
- Azure Cosmos DB account or emulator (for Task features)

### Getting Started

```bash
# Clone and navigate to project
cd here-and-now-service

# Restore dependencies
dotnet restore

# Configure environment (.env file)
# PORT=6060
# CLIENT_ORIGIN_URL=http://localhost:3000
# AUTH0_DOMAIN=your-domain.auth0.com
# AUTH0_AUDIENCE=https://your-api
# COSMOS_CONNECTION_STRING=AccountEndpoint=...
# COSMOS_DATABASE_NAME=HereAndNow
# COSMOS_CONTAINER_NAME=Tasks

# Run the API
dotnet run --project Web/HereAndNow.Web/HereAndNow.Web.csproj
```

### Key Commands

- **Build:** `dotnet build HereAndNow.sln`
- **Test:** `dotnet test`
- **Run:** `dotnet run --project Web/HereAndNow.Web/HereAndNow.Web.csproj`
- **Publish:** `dotnet publish -c Release`

## Repository Structure

```
here-and-now-service/
├── HereAndNow.sln              # Solution file
├── Message/                    # Demo business logic assembly
│   └── HereAndNow.Message/
│       ├── Models/
│       └── Services/
├── Task/                       # Core business logic assembly
│   └── HereAndNow.Task/
│       ├── Models/             # TaskDocument, TaskReminderDocument, Exceptions
│       ├── Repositories/       # CosmosDB data access
│       └── Services/           # Business logic
├── Web/                        # Web layer assemblies
│   ├── HereAndNow.Web/         # API project
│   │   ├── Controllers/
│   │   ├── DTOs/
│   │   ├── Mappers/
│   │   └── Validation/
│   └── HereAndNow.Web.Tests/   # Test project
├── .github/workflows/          # CI/CD pipeline
└── docs/                       # Project documentation
```

## Documentation Map

For detailed information, see:

- [index.md](./index.md) - Master documentation index
- [architecture.md](./architecture.md) - Detailed architecture, Unity pattern
- [source-tree-analysis.md](./source-tree-analysis.md) - Directory structure
- [api-contracts.md](./api-contracts.md) - All 14 API endpoints
- [data-models.md](./data-models.md) - Domain models, DTOs, exceptions
- [development-guide.md](./development-guide.md) - Development workflow
- [deployment-guide.md](./deployment-guide.md) - Azure deployment, CosmosDB setup

---

_Generated using BMAD Method `document-project` workflow_
_Last Updated: 2026-01-15_
