# Project Overview

## Here and Now Service

A **REST API service** for managing reminders with Auth0 authentication, built on ASP.NET Core 8 following Clean Architecture principles.

---

## Purpose

The Here and Now Service provides a backend API for a reminder management application. It demonstrates:

- Auth0 JWT authentication integration
- Clean Architecture with separation of concerns
- RESTful API design patterns
- Swagger/OpenAPI documentation
- Azure App Service deployment

---

## Quick Facts

| Attribute | Value |
|-----------|-------|
| **Project Type** | Backend API Service |
| **Framework** | ASP.NET Core 8.0 |
| **Language** | C# 12 |
| **Architecture** | Clean Architecture |
| **Authentication** | Auth0 JWT Bearer |
| **Storage** | In-memory (ConcurrentDictionary) |
| **Deployment** | Azure App Service |
| **CI/CD** | GitHub Actions |

---

## Technology Stack

| Category | Technology |
|----------|-----------|
| Framework | ASP.NET Core 8.0 |
| Language | C# 12 |
| Authentication | Auth0 + JWT Bearer |
| API Documentation | Swagger/OpenAPI |
| Testing | xUnit, Moq, FluentAssertions |
| CI/CD | GitHub Actions |
| Hosting | Azure App Service |

---

## Solution Structure

```
here-and-now-service/
├── HereAndNow.sln                    # Solution file
├── Reminders/
│   └── HereAndNow.Reminders/         # Domain layer (business logic)
├── Web/
│   ├── HereAndNow.Web/               # API layer (controllers, middleware)
│   └── HereAndNow.Web.Tests/         # Test project
└── docs/                             # Generated documentation
```

### Assembly Purpose

| Assembly | Purpose |
|----------|---------|
| `HereAndNow.Reminders` | Domain models and business logic services |
| `HereAndNow.Web` | ASP.NET Core Web API with Auth0 integration |
| `HereAndNow.Web.Tests` | Unit and integration tests |

---

## API Endpoints

### Messages API

| Endpoint | Auth | Description |
|----------|------|-------------|
| `GET /api/messages/public` | No | Public message |
| `GET /api/messages/protected` | Yes | Protected message |
| `GET /api/messages/admin` | Yes | Admin message |

### Reminder Instances API

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/reminder-instances` | GET | List all reminders |
| `/api/reminder-instances/{id}` | GET | Get specific reminder |
| `/api/reminder-instances` | POST | Create reminder |
| `/api/reminder-instances/{id}` | PUT | Update reminder |
| `/api/reminder-instances/{id}` | DELETE | Delete reminder |

---

## Getting Started

### Prerequisites

- .NET SDK 8.0+
- Auth0 account

### Quick Start

```bash
# Clone repository
git clone <repository-url>
cd here-and-now-service

# Create .env file with Auth0 credentials
echo "PORT=3001
CLIENT_ORIGIN_URL=http://localhost:4040
AUTH0_DOMAIN=your-domain.auth0.com
AUTH0_AUDIENCE=your-api-identifier" > .env

# Run the API
dotnet run --project Web/HereAndNow.Web/HereAndNow.Web.csproj

# Open Swagger UI
open http://localhost:3001/swagger
```

---

## Documentation Index

| Document | Description |
|----------|-------------|
| [Architecture](./architecture.md) | System architecture and design decisions |
| [API Contracts](./api-contracts.md) | Detailed API endpoint documentation |
| [Data Models](./data-models.md) | Domain model documentation |
| [Source Tree](./source-tree-analysis.md) | Project structure analysis |
| [Development Guide](./development-guide.md) | Local development setup |
| [Deployment Guide](./deployment-guide.md) | CI/CD and Azure deployment |

---

## Key Features

- **Auth0 Integration** - JWT Bearer authentication with automatic token validation
- **Clean Architecture** - Domain layer isolated from infrastructure concerns
- **OpenAPI Documentation** - Interactive Swagger UI for API exploration
- **Security Headers** - HSTS, CSP, X-Frame-Options, and more
- **Automated Testing** - Unit and integration tests with coverage
- **CI/CD Pipeline** - Automated build, test, and deploy to Azure

---

## Related Resources

- [Auth0 Developer Resources](https://developer.auth0.com/resources)
- [ASP.NET Core Documentation](https://docs.microsoft.com/aspnet/core)
- [Azure App Service Documentation](https://docs.microsoft.com/azure/app-service)
