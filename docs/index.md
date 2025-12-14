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
| **Deployment** | Azure App Service |

---

## Quick Reference

| Aspect | Details |
|--------|---------|
| **Tech Stack** | ASP.NET Core 8, Auth0, Swagger/OpenAPI, xUnit |
| **Entry Point** | `Web/HereAndNow.Web/Program.cs` |
| **API Endpoints** | 8 endpoints (3 messages, 5 reminder CRUD) |
| **Data Storage** | In-memory (ConcurrentDictionary) |
| **CI/CD** | GitHub Actions → Azure App Service |

---

## Generated Documentation

### Architecture & Design

- [Project Overview](./project-overview.md) - Executive summary and quick facts
- [Architecture](./architecture.md) - System architecture, design decisions, and patterns
- [Source Tree Analysis](./source-tree-analysis.md) - Project structure and critical folders

### API & Data

- [API Contracts](./api-contracts.md) - REST API endpoint documentation with request/response schemas
- [Data Models](./data-models.md) - Domain models, enums, and data access patterns

### Development & Operations

- [Development Guide](./development-guide.md) - Prerequisites, setup, building, testing, troubleshooting
- [Deployment Guide](./deployment-guide.md) - CI/CD pipeline, Azure configuration, deployment procedures

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
| **Services** | `Reminders/HereAndNow.Reminders/Services/` | Add business logic |
| **Models** | `Reminders/HereAndNow.Reminders/Models/` | Add domain entities |
| **Middleware** | `Web/HereAndNow.Web/Middlewares/` | Add cross-cutting concerns |
| **Tests** | `Web/HereAndNow.Web.Tests/` | Add unit/integration tests |

### Code Conventions

- File-scoped namespaces
- Nullable reference types enabled
- XML documentation on public APIs
- Service registration in `Program.cs`
- xUnit for testing with Moq and FluentAssertions

---

## Documentation Metadata

| Field | Value |
|-------|-------|
| **Generated** | 2025-12-12 |
| **Scan Level** | Deep |
| **Workflow** | document-project v1.2.0 |
| **Files Generated** | 8 |
