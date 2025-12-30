# Here and Now Service - Project Overview

**Date:** 2025-12-29
**Type:** Backend API Service
**Architecture:** Clean Architecture with DTO Pattern

## Executive Summary

Here and Now Service is an ASP.NET Core 8.0 REST API that provides a reminder management system with Auth0-based authentication. The service allows authenticated users to create, read, update, and delete reminder instances, with features for scheduling, completion tracking, and soft-deletion.

## Project Classification

- **Repository Type:** Monolith (.NET Solution)
- **Project Type:** Backend API
- **Primary Language:** C# (.NET 8.0)
- **Architecture Pattern:** Clean Architecture with separation between business logic and web layers
- **Authentication:** Auth0 JWT Bearer tokens
- **Deployment:** Azure Web Apps via GitHub Actions

## Technology Stack Summary

| Category | Technology | Version | Purpose |
|----------|------------|---------|---------|
| Framework | ASP.NET Core | 8.0 | Web API framework |
| Language | C# | 12 | Primary language |
| Authentication | Auth0 JWT | - | Identity & access management |
| API Docs | Swashbuckle | 6.9.0 | Swagger/OpenAPI generation |
| Environment | dotenv.net | 3.2.1 | Environment variable loading |
| Testing | xUnit | 2.9.2 | Unit testing framework |
| Testing | Moq | 4.20.72 | Mocking framework |
| Testing | FluentAssertions | 6.12.0 | Assertion library |
| Testing | Mvc.Testing | 8.0.11 | Integration testing |
| CI/CD | GitHub Actions | - | Build, test, deploy pipeline |
| Hosting | Azure Web Apps | - | Production hosting |

## Key Features

1. **Reminder Management**: Full CRUD operations for reminder instances
2. **JWT Authentication**: All reminder endpoints require Auth0 authentication
3. **Soft Delete**: Reminders are soft-deleted (marked as deleted, not removed)
4. **State Machine**: Computed reminder states (Scheduled, Active, Completed, Deleted)
5. **CORS Support**: Configurable cross-origin resource sharing
6. **Security Headers**: Comprehensive security headers middleware
7. **Swagger UI**: Interactive API documentation at `/swagger`

## Architecture Highlights

The solution follows Clean Architecture principles with two main assemblies:

1. **HereAndNow.Reminders** (Business Logic Layer)
   - Pure domain models with no web dependencies
   - Service interfaces defining business operations
   - In-memory service implementations

2. **HereAndNow.Web** (Web API Layer)
   - ASP.NET Core controllers
   - DTOs for API contracts (separated from domain models)
   - Mappers for domain ↔ DTO conversion
   - Custom middlewares for error handling and security

## Development Overview

### Prerequisites

- .NET 8.0 SDK
- Auth0 account (for authentication configuration)

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
├── Reminders/                  # Business logic assembly
│   └── HereAndNow.Reminders/
├── Web/                        # Web layer assemblies
│   ├── HereAndNow.Web/         # API project
│   └── HereAndNow.Web.Tests/   # Test project
├── .github/workflows/          # CI/CD pipeline
└── docs/                       # Project documentation
```

## Documentation Map

For detailed information, see:

- [index.md](./index.md) - Master documentation index
- [architecture.md](./architecture.md) - Detailed architecture
- [source-tree-analysis.md](./source-tree-analysis.md) - Directory structure
- [api-contracts.md](./api-contracts.md) - API endpoint documentation
- [data-models.md](./data-models.md) - Domain model documentation
- [development-guide.md](./development-guide.md) - Development workflow
- [deployment-guide.md](./deployment-guide.md) - Deployment process

---

_Generated using BMAD Method `document-project` workflow_
