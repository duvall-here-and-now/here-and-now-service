# Copilot Instructions

## Project Overview
This is an ASP.NET Core API service demonstrating Auth0 authentication with different access levels (public, protected, admin).

## Technology Stack
- **Framework**: ASP.NET Core 8.0 (C#)
- **Authentication**: Auth0 JWT Bearer
- **Solution Structure**: Multi-project solution with `Message` and `Web` projects

## Coding Standards

### C# Guidelines
- Follow C# naming conventions (PascalCase for classes/methods, camelCase for local variables)
- Use modern C# features and patterns where appropriate
- Prefer async/await for I/O operations
- Use dependency injection for service management
- Make sure all IDisposable objects are properly disposed of

### Code Style
- Keep methods focused and single-purpose
- Add XML documentation comments for public APIs
- Use meaningful variable and method names
- Handle exceptions appropriately with proper error messages


## Project Structure
- `/Message` - Business logic and domain models
  - `/Message/HereAndNow.Message/Models/` - Domain models
  - `/Message/HereAndNow.Message/Services/` - Service interfaces and implementations
- `/Web` - API controllers and web configuration
  - `/Web/HereAndNow.Web/Controllers/` - REST API endpoints
  - `/Web/HereAndNow.Web/Middlewares/` - Custom middleware
- `HereAndNow.sln` - Main solution file

## API Endpoints
- `GET /api/messages/public` - No authentication required
- `GET /api/messages/protected` - JWT authentication required
- `GET /api/messages/admin` - JWT authentication required

## Authentication & Authorization
- This project uses Auth0 for authentication and authorization
- Maintain security best practices when working with authentication code
- Never commit secrets or API keys to version control

## Development Workflow
1. Build the solution before running tests
2. Ensure all changes maintain backward compatibility
3. Test API endpoints after making changes
4. Update relevant documentation when adding new features

## Common Commands
- Build: `dotnet build HereAndNow.sln`
- Run: `dotnet run --project Web/HereAndNow.Web/HereAndNow.Web.csproj`
- Test: `dotnet test`
- Restore packages: `dotnet restore`


## Code Review Standards

Before submitting pull requests:
1. Follow .NET 8 best practices (see `.github/agents/dotnet-code-reviewer.md`)
2. Use file-scoped namespaces
3. Enable and respect nullable reference types
4. Include proper async/await patterns with CancellationToken
5. Add XML documentation for public APIs
6. Write unit tests for new functionality
7. Follow existing architectural patterns

### Code Review Agent

**For large code changes (>100 lines or multiple files), always invoke the .NET code review agent before finalizing:**

The specialized code review agent is located at `.github/agents/dotnet-code-reviewer.md`. This agent enforces .NET 8 best practices and should be consulted for:

- Major refactoring efforts
- New feature implementations spanning multiple files
- Changes to core business logic or data access patterns
- Adding new API endpoints
- Changes that impact authentication or authorization
- Performance-critical code modifications

**How to use the code review agent:**
1. Make your code changes
2. Invoke the `dotnet-code-reviewer` agent with the files you've changed
3. Address any Critical or High severity issues identified
4. Consider Medium severity suggestions for code quality improvements
5. Document any intentional deviations from best practices

This ensures consistency with .NET 8 standards and catches potential issues early in the development process.
