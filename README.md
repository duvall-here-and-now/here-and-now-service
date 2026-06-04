# Here and Now Service

An ASP.NET Core 8 REST API for personal task and reminder management with recurring task support. Deployed to Azure Web Apps with Auth0 JWT authentication and Azure Cosmos DB persistence.

## Features

- **Tasks** — Create, update, complete, and soft-delete tasks
- **Reminders** — Schedule, snooze, and dismiss reminders linked to tasks
- **Recurring Tasks** — RRULE-based recurrence patterns (RFC 5545 / iCal) with computed instances, state overrides, and a one-active-at-a-time constraint
- **Auth0 authentication** — All task and reminder endpoints require a valid JWT
- **Atomic operations** — Unity Pattern uses Cosmos DB transactional batches to keep task and reminder documents in sync

## Architecture

The solution follows Clean Architecture with three assemblies:

| Assembly | Responsibility |
|----------|----------------|
| `HereAndNow.Message` | Demo business logic (Auth0 sample endpoints) |
| `HereAndNow.Task` | Core domain: tasks, reminders, recurring tasks, Cosmos DB repositories |
| `HereAndNow.Web` | ASP.NET Core Web API: controllers, DTOs, middleware |

**Command Pattern** — All mutations are routed through a single endpoint: `POST /api/v1/commands`. Queries remain on their own REST endpoints.

**Unity Pattern** — Task + reminder documents are updated atomically using Cosmos DB transactional batches, preventing partial-update inconsistencies.

**Computed Instance Model** — Recurring task instances are computed on-the-fly from an RRULE config and stored state overrides rather than being persisted individually.

## Tech Stack

- .NET 8 / ASP.NET Core
- Azure Cosmos DB (partition key: `/userId`)
- Auth0 JWT Bearer authentication
- Ical.Net 5.2 (RRULE parsing)
- Swagger / OpenAPI
- GitHub Actions CI/CD → Azure Web Apps

## Getting Started

### Prerequisites

- .NET 8 SDK
- An Auth0 tenant with an API configured
- An Azure Cosmos DB account (or the [Cosmos DB emulator](https://docs.microsoft.com/en-us/azure/cosmos-db/local-emulator) for local development)

### Environment Setup

Create a `.env` file in `Web/HereAndNow.Web/` with the following variables:

```env
PORT=5000
CLIENT_ORIGIN_URL=http://localhost:4040
AUTH0_DOMAIN=your-tenant.auth0.com
AUTH0_AUDIENCE=your-api-identifier
COSMOS_CONNECTION_STRING=AccountEndpoint=https://...;AccountKey=...
COSMOS_DATABASE_NAME=HereAndNow
COSMOS_CONTAINER_NAME=Tasks
```

The `.env` file is gitignored and must never be committed.

### Build and Run

```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build HereAndNow.sln

# Run the API (available at http://localhost:5000)
dotnet run --project Web/HereAndNow.Web/HereAndNow.Web.csproj

# Run all tests
dotnet test
```

Swagger UI is available at `http://localhost:5000/swagger` when running.

## API Overview

| Category | Entry point |
|----------|-------------|
| Mutations (tasks, reminders, recurring tasks) | `POST /api/v1/commands` |
| Task queries | `GET /api/v1/tasks`, `GET /api/v1/tasks/{id}` |
| Reminder queries | `GET /api/v1/reminders`, `GET /api/v1/reminders/{id}` |
| Recurring task queries | `GET /api/v1/recurring-tasks` and computed instances endpoint |
| Demo endpoints | `GET /api/messages/public\|protected\|admin` |

See [`docs/api-contracts.md`](docs/api-contracts.md) for the full API reference.

## Deployment

Deployments are automated via GitHub Actions on push to `main`:

1. Build and test the solution
2. Publish to the deployment artifact
3. Deploy to Azure Web App using a publish profile stored in GitHub Secrets

## Documentation

| Document | Contents |
|----------|----------|
| [`docs/index.md`](docs/index.md) | Master documentation index |
| [`docs/architecture.md`](docs/architecture.md) | Architecture, Unity Pattern, Cosmos DB design |
| [`docs/api-contracts.md`](docs/api-contracts.md) | Complete API reference |
| [`docs/data-models.md`](docs/data-models.md) | Domain models, DTOs, exceptions |
| [`docs/development-guide.md`](docs/development-guide.md) | Development workflow |
| [`docs/deployment-guide.md`](docs/deployment-guide.md) | Azure deployment guide |
