# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is an ASP.NET Core 8 API service implementing a reminder system with Auth0 authentication and authorization. The project is structured as a multi-project solution with separation between business logic and API layers.

## Solution Structure

The solution follows a clean architecture pattern with two main assemblies:

- **HereAndNow.Reminders** (`/Reminders/HereAndNow.Reminders/`)
  - Business logic assembly containing domain models and service interfaces
  - Models: `ReminderInstance`, `Message`, `ReminderStatus`
  - Service interfaces: `IReminderInstanceService`, `IMessageService`
  - Pure business logic with no web dependencies
  - References: Microsoft.Extensions.Logging.Abstractions

- **HereAndNow.Web** (`/Web/HereAndNow.Web/`)
  - ASP.NET Core Web API project containing controllers and middleware
  - Handles HTTP concerns, authentication, CORS, and Swagger configuration
  - Controllers: `MessagesController`, `ReminderInstancesController`, `ErrorController`
  - Custom middlewares: `ErrorHandlerMiddleware`, `SecureHeadersMiddleware`
  - References the Reminders assembly for business logic
  - Uses dotenv.net for environment variable management

- **HereAndNow.Web.Tests** (`/Web/HereAndNow.Web.Tests/`)
  - Test project using xUnit, Moq, FluentAssertions
  - Integration testing with `Microsoft.AspNetCore.Mvc.Testing`

## Common Development Commands

### Build and Run
```bash
# Build entire solution
dotnet build HereAndNow.sln

# Build specific configuration
dotnet build HereAndNow.sln --configuration Release

# Run the web API
dotnet run --project Web/HereAndNow.Web/HereAndNow.Web.csproj

# Restore packages
dotnet restore
```

### Testing
```bash
# Run all tests
dotnet test

# Run tests with verbose output
dotnet test --logger "console;verbosity=detailed"

# Run specific test by name filter
dotnet test --filter "FullyQualifiedName~TestMethodName"

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test project
dotnet test Web/HereAndNow.Web.Tests/HereAndNow.Web.Tests.csproj

# Watch mode for continuous testing during development
dotnet watch test --project Web/HereAndNow.Web.Tests/HereAndNow.Web.Tests.csproj
```

### Publishing
```bash
# Publish for deployment
dotnet publish Web/HereAndNow.Web/HereAndNow.Web.csproj -c Release -o ./publish
```

## Configuration and Environment

The application uses environment variables loaded via dotenv.net. Required variables:
- `PORT` - Port number for the web server
- `CLIENT_ORIGIN_URL` - CORS origin URL for the frontend
- `AUTH0_DOMAIN` - Auth0 domain for JWT validation
- `AUTH0_AUDIENCE` - Auth0 API audience identifier

Configuration files:
- `.env` - Local environment variables (not committed to git)


## Authentication & Authorization Architecture

The application uses Auth0 for JWT-based authentication:
- JWT Bearer tokens validated against Auth0 authority
- Configured in `Program.cs` with `AddJwtBearer`
- Swagger UI includes Bearer token authentication
- All API endpoints require authentication by default
- CORS configured to accept requests from configured client origin

## Dependency Injection Pattern

Services are registered in `Program.cs` with appropriate lifetimes:
- `IMessageService` → `MessageService` (Scoped)
- `IReminderInstanceService` → `ReminderInstanceService` (Singleton)

The Reminders assembly defines service interfaces, while Web project provides implementations. This allows the business layer to remain independent of infrastructure concerns.

## .NET 8 Standards

This project follows .NET 8 best practices:
- **File-scoped namespaces** - Used throughout the codebase
- **Nullable reference types enabled** - All projects have `<Nullable>enable</Nullable>`
- **Implicit usings enabled** - Common namespaces imported automatically
- **XML documentation generation** - Enabled for the Web project with warning suppression for missing docs
- **Modern C# patterns** - Pattern matching, record types, init-only properties

## Code Review Requirements

For large code changes (>100 lines or multiple files), invoke the .NET code review agent located at `.github/agents/dotnet-code-reviewer.md`. This is required for:
- Major refactoring efforts
- New feature implementations spanning multiple files
- Changes to core business logic or service patterns
- Adding new API endpoints
- Changes impacting authentication or authorization
- Performance-critical code modifications

The code reviewer enforces .NET 8 best practices including proper async/await patterns, dependency injection, security considerations, and SOLID principles.

## Deployment

The project uses GitHub Actions for CI/CD to Azure Web Apps:
- Workflow file: `.github/workflows/main_here-and-now-service.yml`
- Triggers on push to `main` branch
- Pipeline: Build → Test → Publish → Deploy to Azure App Service
- **Quality Gate**: Tests must pass before deployment proceeds
- Test results published to GitHub Actions UI with detailed reports
- Code coverage reports uploaded as workflow artifacts
- Uses publish profile stored in GitHub secrets
- The `clean: true` flag ensures old artifacts are removed during deployment

## Swagger/OpenAPI

Swagger UI is available at `/swagger` when the application is running. It includes:
- Auto-generated API documentation from XML comments
- JWT Bearer token authentication support
- Request/response schema documentation
- Interactive API testing interface
