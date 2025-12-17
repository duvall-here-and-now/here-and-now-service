# Here and Now Service - Documentation Index

> **Primary AI Retrieval Source** - This index serves as the entry point for AI-assisted development workflows.

---

## Project Overview

| Attribute | Value |
|-----------|-------|
| **Type** | Backend API Service (Monolith) |
| **Primary Language** | C# 12 |
| **Framework** | ASP.NET Core 8.0 |
| **Architecture Pattern** | Clean Architecture |
| **Authentication** | Auth0 JWT Bearer |
| **Data Storage** | Azure Cosmos DB (Primary) / In-memory (Fallback) |
| **Deployment** | Azure App Service |

---

## Quick Reference

| Aspect | Details |
|--------|---------|
| **Tech Stack** | ASP.NET Core 8, Auth0, Azure Cosmos DB, Swagger/OpenAPI, xUnit |
| **Entry Point** | `Web/HereAndNow.Web/Program.cs` |
| **API Endpoints** | 8 endpoints (3 messages, 5 reminder CRUD) |
| **Data Storage** | Azure Cosmos DB with `/userId` partition key |
| **CI/CD** | GitHub Actions → Azure App Service |
| **Test Coverage** | 18 tests (13 unit, 5 integration) |

---

## Generated Documentation

### Architecture & Design

| Document | Description |
|----------|-------------|
| [Project Overview](./project-overview.md) | Executive summary and quick facts |
| [Architecture](./architecture.md) | System architecture, design decisions, data flow |
| [Source Tree Analysis](./source-tree-analysis.md) | Project structure, namespaces, dependency flow |

### API & Data

| Document | Description |
|----------|-------------|
| [API Contracts](./api-contracts.md) | REST API endpoints, request/response schemas, error codes |
| [Data Models](./data-models.md) | Domain models, DTOs, persistence layer, service interfaces |

### Development & Operations

| Document | Description |
|----------|-------------|
| [Development Guide](./development-guide.md) | Prerequisites, setup, building, testing, troubleshooting |
| [Deployment Guide](./deployment-guide.md) | CI/CD pipeline, Azure Cosmos DB, deployment procedures |

---

## Existing Documentation

These documents were found in the repository:

| Document | Location | Description |
|----------|----------|-------------|
| [README.md](../README.md) | Project root | Auth0 integration overview |
| [CLAUDE.md](../CLAUDE.md) | Project root | Claude Code development instructions |
| [SWAGGER_SETUP.md](../Web/HereAndNow.Web/SWAGGER_SETUP.md) | Web project | Swagger UI configuration and Azure IP restrictions |
| [dotnet-code-reviewer.md](../.github/agents/dotnet-code-reviewer.md) | .github/agents | AI code review agent guidelines |
| [copilot-instructions.md](../.github/copilot-instructions.md) | .github | GitHub Copilot configuration |

---

## Getting Started

### For New Developers

1. Read [Development Guide](./development-guide.md) for setup instructions
2. Review [Architecture](./architecture.md) for system understanding
3. Explore [API Contracts](./api-contracts.md) for endpoint documentation
4. Access Swagger UI at `/swagger` when running locally

### For AI-Assisted Development

When planning new features, reference:

1. **For API changes:** [API Contracts](./api-contracts.md) + [Data Models](./data-models.md)
2. **For architectural decisions:** [Architecture](./architecture.md)
3. **For code patterns:** [Source Tree Analysis](./source-tree-analysis.md)

---

## Brownfield PRD Reference

When creating a PRD for new features on this codebase, provide this index as context:

```
Context: docs/index.md
Architecture: docs/architecture.md
API: docs/api-contracts.md
Data: docs/data-models.md
```

### Key Integration Points

| Component | Location | Purpose |
|-----------|----------|---------|
| **Controllers** | `Web/HereAndNow.Web/Controllers/` | Add new API endpoints |
| **DTOs** | `Web/HereAndNow.Web/DTOs/` | Add API response models |
| **Mappers** | `Web/HereAndNow.Web/Mappers/` | Add Domain ↔ DTO mappings |
| **Services** | `Reminders/HereAndNow.Reminders/Services/` | Add business logic |
| **Models** | `Reminders/HereAndNow.Reminders/Models/` | Add domain entities |
| **Persistence** | `Reminders/HereAndNow.Reminders/Persistence/` | Add Cosmos documents |
| **Middleware** | `Web/HereAndNow.Web/Middlewares/` | Add cross-cutting concerns |
| **Tests** | `Web/HereAndNow.Web.Tests/` | Add unit/integration tests |

### Code Conventions

- File-scoped namespaces
- Nullable reference types enabled
- XML documentation on public APIs
- Service registration in `Program.cs`
- xUnit for testing with Moq and FluentAssertions
- Soft delete pattern (IsDeleted flag)
- Multi-tenant isolation via partition key

### Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `PORT` | Yes | Server port |
| `CLIENT_ORIGIN_URL` | Yes | CORS allowed origins (comma-separated) |
| `AUTH0_DOMAIN` | Yes | Auth0 tenant domain |
| `AUTH0_AUDIENCE` | Yes | Auth0 API identifier |
| `COSMOS_ENDPOINT` | No* | Cosmos DB endpoint |
| `COSMOS_PRIMARY_KEY` | No* | Cosmos DB key |
| `COSMOS_DATABASE_NAME` | No* | Database name |
| `COSMOS_CONTAINER_NAME` | No* | Container name |

*If Cosmos variables are not set, service uses in-memory storage

---

## Recent Changes

### 2025-12-17 (This Scan)

- **Added:** Azure Cosmos DB persistence with multi-tenant user isolation
- **Added:** DTO layer with computed State property
- **Added:** ReminderInstanceMapper for Domain ↔ DTO conversion
- **Added:** ServiceUnavailableException for Cosmos transient errors
- **Added:** SDK-level retry policy for 429 throttling
- **Added:** Soft delete pattern with IsDeleted flag

### 2025-12-12 (Initial Scan)

- Initial documentation generation
- API contracts, data models, architecture documented

---

## Documentation Metadata

| Field | Value |
|-------|-------|
| **Generated** | 2025-12-17 |
| **Scan Level** | Exhaustive |
| **Workflow** | document-project v1.2.0 |
| **Files Updated** | 8 |
| **Source Files Scanned** | 20 |
