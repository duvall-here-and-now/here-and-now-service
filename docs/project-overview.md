# Here and Now Service - Project Overview

**Date:** 2025-12-30
**Type:** Backend API Service
**Architecture:** Clean Architecture (2-Layer)

## Executive Summary

Here and Now Service is an ASP.NET Core 8.0 REST API that demonstrates Auth0 JWT authentication with different access levels. The service provides three simple endpoints (public, protected, admin) to showcase authentication and authorization patterns.

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

1. **Message API**: Three endpoints demonstrating access levels:
   - `/api/messages/public` - No authentication required
   - `/api/messages/protected` - JWT required
   - `/api/messages/admin` - JWT required

2. **JWT Authentication**: Auth0-based token validation
3. **CORS Support**: Configurable cross-origin resource sharing
4. **Security Headers**: Comprehensive security headers middleware
5. **Swagger UI**: Interactive API documentation at `/swagger`

## Architecture Highlights

The solution follows Clean Architecture principles with two main assemblies:

1. **HereAndNow.Message** (Business Logic Layer)
   - Simple domain model (`Message`)
   - Service interface (`IMessageService`)
   - Service implementation with static messages

2. **HereAndNow.Web** (Web API Layer)
   - ASP.NET Core controller (`MessagesController`)
   - Custom middlewares for error handling and security
   - Empty DTOs/Mappers folders for future expansion

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
├── Message/                    # Business logic assembly
│   └── HereAndNow.Message/
│       ├── Models/Message.cs
│       └── Services/
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
_Last Updated: 2025-12-30_
