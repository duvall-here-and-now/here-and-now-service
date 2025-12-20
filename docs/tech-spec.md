# here-and-now-service - Technical Specification

**Author:** Mike
**Date:** 2025-12-19
**Project Level:** Quick Flow (Brownfield)
**Change Type:** API Behavior Modification + Code Cleanup
**Development Context:** Brownfield - Existing ASP.NET Core 8 API

---

## Context

### Available Documents

| Document | Status | Source |
|----------|--------|--------|
| Product Brief | Not available | - |
| Research | Not available | - |
| Brownfield Documentation | ✅ Loaded | `docs/index.md` (INDEX_GUIDED) |
| API Contracts | ✅ Loaded | `docs/api-contracts.md` |
| Data Models | ✅ Loaded | `docs/data-models.md` |
| Architecture | ✅ Loaded | `docs/architecture.md` |
| Source Tree | ✅ Loaded | `docs/source-tree-analysis.md` |

### Project Stack

| Category | Technology | Version |
|----------|-----------|---------|
| **Framework** | ASP.NET Core | 8.0 |
| **Language** | C# | 12 |
| **Authentication** | Auth0 + JWT Bearer | - |
| **Database** | Azure Cosmos DB | SDK 3.46.0 |
| **API Documentation** | Swashbuckle/Swagger | 6.9.0 |
| **Testing** | xUnit | 2.9.2 |
| **Mocking** | Moq | 4.20.72 |
| **Assertions** | FluentAssertions | 6.12.0 |
| **Integration Tests** | WebApplicationFactory | 8.0.11 |

### Existing Codebase Structure

```
here-and-now-service/
├── Reminders/HereAndNow.Reminders/          # Domain layer
│   ├── Models/ReminderInstance.cs           # Core domain entity (13 properties)
│   ├── Services/
│   │   ├── IReminderInstanceService.cs      # Service interface
│   │   ├── ReminderInstanceService.cs       # In-memory implementation (TO BE DELETED)
│   │   └── CosmosReminderInstanceService.cs # Cosmos DB implementation (ONLY impl after changes)
│   ├── Persistence/ReminderDocument.cs      # Cosmos document mapping
│   └── Exceptions/ServiceUnavailableException.cs
│
├── Web/HereAndNow.Web/                      # API layer
│   ├── Controllers/
│   │   └── ReminderInstancesController.cs   # 6 endpoints (GET, GET/{id}, POST, PATCH, POST/{id}/complete, DELETE)
│   ├── DTOs/
│   │   ├── CreateReminderRequest.cs         # POST request body (ADD Id field)
│   │   ├── UpdateReminderRequest.cs         # PATCH request body
│   │   ├── ReminderInstanceDto.cs           # Response DTO with computed State
│   │   └── ReminderState.cs                 # Enum: Scheduled, Active, Completed, Deleted
│   ├── Mappers/ReminderInstanceMapper.cs    # Domain ↔ DTO conversion
│   ├── Program.cs                           # DI setup (REMOVE conditional logic)
│   └── Middlewares/                         # Error handling, security headers
│
└── Web/HereAndNow.Web.Tests/                # Test project
    ├── Controllers/ReminderInstancesControllerTests.cs  # 13 unit tests
    ├── Helpers/TestWebApplicationFactory.cs             # (ADD mock service)
    └── Integration/AuthorizationTests.cs                # 5 integration tests
```

---

## The Change

### Problem Statement

**Change #1 - Client-Provided UUID:**
The current API generates reminder IDs server-side (`Guid.NewGuid()`), which prevents clients from:
- Implementing idempotent create operations (retry-safe)
- Pre-generating IDs for offline-first scenarios
- Correlating reminders across distributed systems before server confirmation

**Change #2 - ScheduledDateAndTime State Validation:**
The current PATCH endpoint allows modifying `ScheduledDateAndTime` regardless of the reminder's state. This creates semantic issues:
- Active reminders (time has passed) shouldn't have their trigger time changed retroactively
- Completed reminders have already fired - changing time is meaningless
- Deleted reminders shouldn't be modifiable at all (already enforced)

**Change #3 - Remove In-Memory Service Implementation:**
The codebase contains two implementations of `IReminderInstanceService`:
- `CosmosReminderInstanceService` - Production Cosmos DB implementation
- `ReminderInstanceService` - In-memory fallback for local development

The in-memory implementation:
- Serves no purpose in production (Cosmos DB is always used)
- Creates maintenance burden (must update both implementations)
- Adds confusion about which implementation is active
- Makes the codebase larger than necessary

### Proposed Solution

**Change #1:** Require clients to provide the reminder `Id` (UUID) in the `CreateReminderRequest`. The server will:
- Validate the UUID format
- Check for duplicates within the user's partition
- Return 409 Conflict if the ID already exists

**Change #2:** Add state validation to the PATCH endpoint. When `ScheduledDateAndTime` is included in the update request:
- Compute the reminder's current state
- If state is NOT `Scheduled`, return 400 Bad Request
- Other fields (`Text`, `ShouldPlaySound`, `ShouldDoVibration`) remain updatable in any non-deleted state

**Change #3:** Remove the in-memory `ReminderInstanceService` and require Cosmos DB:
- Delete `ReminderInstanceService.cs`
- Update `Program.cs` to require Cosmos DB configuration (throw on startup if missing)
- Update `TestWebApplicationFactory` to register a mock `IReminderInstanceService` for integration tests

### Scope

**In Scope:**

- Add required `Id` field to `CreateReminderRequest`
- Modify `IReminderInstanceService.Create()` to accept `Guid id` parameter
- Update `CosmosReminderInstanceService` to use provided ID
- Add duplicate ID detection with 409 Conflict response
- Add state validation for `ScheduledDateAndTime` updates in PATCH endpoint
- Return 400 Bad Request when updating time on non-Scheduled reminders
- Delete `ReminderInstanceService.cs` (in-memory implementation)
- Update `Program.cs` to require Cosmos DB and remove conditional logic
- Update `TestWebApplicationFactory` to mock `IReminderInstanceService`
- Update existing unit tests for new behavior
- Add new unit tests for edge cases

**Out of Scope:**

- Changing other API endpoints (Complete, Delete)
- Modifying the State computation logic
- Adding validation for other fields based on state
- Database schema changes (Cosmos DB documents already support client IDs)
- Breaking changes to response format
- Creating a Cosmos DB emulator setup guide (developers should use Azure Cosmos DB)

---

## Implementation Details

### Source Tree Changes

| File Path | Action | Changes |
|-----------|--------|---------|
| `Web/HereAndNow.Web/DTOs/CreateReminderRequest.cs` | MODIFY | Add required `Id` property with `[Required]` attribute |
| `Web/HereAndNow.Web/Controllers/ReminderInstancesController.cs` | MODIFY | Pass `request.Id` to service; handle 409 Conflict for duplicates; handle 400 for state validation |
| `Reminders/HereAndNow.Reminders/Services/IReminderInstanceService.cs` | MODIFY | Add `Guid id` parameter to `Create()` method signature |
| `Reminders/HereAndNow.Reminders/Services/ReminderInstanceService.cs` | **DELETE** | Remove entire file (in-memory implementation no longer needed) |
| `Reminders/HereAndNow.Reminders/Services/CosmosReminderInstanceService.cs` | MODIFY | Use provided ID; handle Cosmos 409 Conflict; add state validation in `Update()` |
| `Web/HereAndNow.Web/Program.cs` | MODIFY | Remove conditional service registration; require Cosmos DB; throw if not configured |
| `Web/HereAndNow.Web.Tests/Helpers/TestWebApplicationFactory.cs` | MODIFY | Register mock `IReminderInstanceService` for integration tests |
| `Reminders/HereAndNow.Reminders/Models/ReminderInstance.cs` | NO CHANGE | Already supports `Id` as `Guid` |
| `Web/HereAndNow.Web.Tests/Controllers/ReminderInstancesControllerTests.cs` | MODIFY | Update existing tests; add tests for duplicate ID and state validation |

### Technical Approach

**Change #1 - Client-Provided UUID:**

1. **DTO Modification** (`CreateReminderRequest.cs`):
   ```csharp
   [Required]
   public Guid Id { get; init; }
   ```

2. **Service Interface Change** (`IReminderInstanceService.cs`):
   ```csharp
   ReminderInstance Create(
       Guid id,           // NEW: Client-provided ID
       string userId,
       string text,
       DateTime scheduledDateAndTime,
       bool shouldPlaySound,
       bool shouldDoVibration);
   ```

3. **Cosmos Service** (`CosmosReminderInstanceService.cs`):
   - Use `CreateItemAsync()` which throws `CosmosException` with `HttpStatusCode.Conflict` (409) on duplicate
   - Catch and rethrow as `InvalidOperationException` for consistent handling

4. **Controller** (`ReminderInstancesController.cs`):
   - Catch `InvalidOperationException` from service
   - Return `Conflict(new { message = "..." })` with 409 status

**Change #2 - State Validation:**

1. **State Computation Helper** (add to service or use inline):
   ```csharp
   private static bool IsScheduledState(ReminderInstance reminder)
   {
       if (reminder.IsDeleted) return false;
       if (reminder.IsCompleted) return false;
       if (DateTime.UtcNow >= reminder.ScheduledDateAndTime) return false; // Active
       return true; // Scheduled
   }
   ```

2. **Update Method Validation** (`CosmosReminderInstanceService.cs`):
   - Before applying `scheduledDateAndTime` update, check `IsScheduledState()`
   - If not Scheduled and `scheduledDateAndTime.HasValue`, throw `InvalidOperationException`

3. **Controller** (`ReminderInstancesController.cs`):
   - Catch `InvalidOperationException` for state validation failures
   - Return `BadRequest(new { message = "..." })` with 400 status

**Change #3 - Remove In-Memory Service:**

1. **Delete File** (`ReminderInstanceService.cs`):
   - Remove `Reminders/HereAndNow.Reminders/Services/ReminderInstanceService.cs` entirely

2. **Update Program.cs** (lines 35-62):
   - Remove the `useCosmosDb` conditional check
   - Always register `CosmosReminderInstanceService`
   - Add Cosmos DB environment variables to required vars check
   - Throw `InvalidOperationException` on startup if Cosmos not configured

   ```csharp
   // BEFORE (conditional)
   if (useCosmosDb) { ... } else { ... }

   // AFTER (required)
   var cosmosSettings = new CosmosDbSettings { ... };
   if (string.IsNullOrEmpty(cosmosSettings.Endpoint) || string.IsNullOrEmpty(cosmosSettings.PrimaryKey))
   {
       throw new InvalidOperationException("Cosmos DB configuration is required. Set COSMOS_ENDPOINT and COSMOS_PRIMARY_KEY.");
   }
   builder.Services.AddSingleton(cosmosSettings);
   builder.Services.AddSingleton<CosmosClient>(...);
   builder.Services.AddScoped<IReminderInstanceService, CosmosReminderInstanceService>(...);
   ```

3. **Update TestWebApplicationFactory.cs**:
   - Register a mock `IReminderInstanceService` to bypass Cosmos DB requirement
   - Use Moq to create a mock that returns appropriate test data

   ```csharp
   builder.ConfigureTestServices(services =>
   {
       // Remove any existing IReminderInstanceService registration
       var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IReminderInstanceService));
       if (descriptor != null) services.Remove(descriptor);

       // Register mock service
       var mockService = new Mock<IReminderInstanceService>();
       // Setup mock behaviors as needed for tests
       services.AddSingleton(mockService.Object);
   });
   ```

### Existing Patterns to Follow

Based on brownfield codebase analysis:

1. **Record DTOs with `init` properties:**
   ```csharp
   public record CreateReminderRequest
   {
       [Required]
       public Guid Id { get; init; }  // Follow existing pattern
   }
   ```

2. **Service method signature pattern:**
   - Required parameters as positional
   - Optional parameters with defaults
   - Return `null` for not found, throw exceptions for business rule violations

3. **Controller error handling pattern:**
   - Catch specific exceptions
   - Return typed HTTP responses: `NotFound()`, `Conflict()`, `BadRequest()`
   - Include `{ message = "..." }` object in error responses

4. **Logging pattern:**
   - Log at entry: "Request received..."
   - Log on success: "Successfully..."
   - Log on failure: "Cannot {action} - {reason}"
   - Use structured logging with named parameters

### Integration Points

| Component | Integration Type | Details |
|-----------|-----------------|---------|
| `CreateReminderRequest` → Controller | Request binding | ASP.NET model binding validates `[Required]` |
| Controller → Service | Method call | Pass `request.Id` as first parameter |
| Service → Cosmos DB | CreateItemAsync | Cosmos returns 409 on duplicate document ID |
| Program.cs → Cosmos DB | Startup validation | Throws if Cosmos not configured |
| TestWebApplicationFactory → Mock | Test DI | Registers mock service for integration tests |

---

## Development Context

### Relevant Existing Code

| Reference | Location | Pattern to Follow |
|-----------|----------|-------------------|
| Existing DTO with Required field | `CreateReminderRequest.cs:14-16` | `[Required]` attribute usage |
| Service Create signature | `IReminderInstanceService.cs:34-39` | Parameter ordering |
| Duplicate handling pattern | `ReminderInstanceService.cs:88-97` | TryAdd + logging |
| Conflict response pattern | `ReminderInstancesController.cs:183-187` | Complete endpoint returns 409 |
| State computation | `ReminderInstanceDto.cs:61-67` | Switch expression pattern |
| Update validation | `ReminderInstanceService.cs:127-131` | Check IsDeleted before update |

### Dependencies

**Framework/Libraries:**

| Dependency | Version | Usage |
|------------|---------|-------|
| ASP.NET Core | 8.0 | Web framework, model binding, validation |
| Microsoft.Azure.Cosmos | 3.46.0 | Cosmos DB SDK with conflict detection |
| System.ComponentModel.DataAnnotations | (built-in) | `[Required]` attribute |

**Internal Modules:**

| Module | Namespace | Purpose |
|--------|-----------|---------|
| `IReminderInstanceService` | `HereAndNowService.Services` | Service interface to modify |
| `ReminderInstanceMapper` | `HereAndNowService.Mappers` | No changes needed |
| `ReminderState` | `HereAndNowService.DTOs` | Used for state comparison |

### Configuration Changes

None required. No new environment variables or config files needed.

### Existing Conventions (Brownfield)

| Aspect | Convention | Source |
|--------|------------|--------|
| **Namespaces** | File-scoped (`namespace X;`) | All files |
| **Nullable** | Enabled (`<Nullable>enable</Nullable>`) | .csproj |
| **DTOs** | Record types with `init` properties | `CreateReminderRequest.cs` |
| **Validation** | Data annotations (`[Required]`, `[StringLength]`) | `CreateReminderRequest.cs` |
| **Error responses** | `{ message: "..." }` anonymous objects | All controllers |
| **Logging** | Structured with `{ParameterName}` placeholders | All services |
| **XML docs** | On all public members | Enabled in .csproj |

### Test Framework & Standards

| Aspect | Standard |
|--------|----------|
| **Framework** | xUnit 2.9.2 |
| **Mocking** | Moq 4.20.72 |
| **Assertions** | FluentAssertions 6.12.0 |
| **Test naming** | `MethodName_Scenario_ExpectedResult` |
| **Test organization** | One test class per controller |
| **Mocking pattern** | `Mock<IService>` with `Setup()` and `Verify()` |

---

## Implementation Stack

| Layer | Technology | Version |
|-------|-----------|---------|
| **Runtime** | .NET | 8.0 |
| **Framework** | ASP.NET Core | 8.0 |
| **Language** | C# | 12 |
| **Database** | Azure Cosmos DB | SDK 3.46.0 |
| **Testing** | xUnit + Moq + FluentAssertions | 2.9.2 / 4.20.72 / 6.12.0 |
| **CI/CD** | GitHub Actions | - |

---

## Technical Details

### Change #1: Client-Provided UUID

**Algorithm:**
1. Client includes `Id` (GUID) in POST request body
2. ASP.NET validates `[Required]` - returns 400 if missing or malformed
3. Controller extracts `request.Id` and passes to service
4. Service attempts to create with provided ID
5. If ID exists in user's partition → throw `InvalidOperationException`
6. Controller catches exception → returns 409 Conflict

**Cosmos DB Behavior:**
- `CreateItemAsync()` with existing document ID returns `CosmosException` with `StatusCode = HttpStatusCode.Conflict` (409)
- This is efficient - single atomic operation, no read-before-write

**In-Memory Behavior:**
- `ConcurrentDictionary.TryAdd()` returns `false` if key exists
- Thread-safe, no race conditions

### Change #2: ScheduledDateAndTime State Validation

**Algorithm:**
1. Client sends PATCH with `scheduledDateAndTime` field
2. Service loads existing reminder
3. If `scheduledDateAndTime.HasValue`:
   - Compute current state: `IsDeleted` → Deleted, `IsCompleted` → Completed, `now >= time` → Active, else → Scheduled
   - If state ≠ Scheduled → throw `InvalidOperationException`
4. Apply other field updates normally
5. Controller catches exception → returns 400 Bad Request

**State Computation (inline):**
```csharp
bool isScheduled = !existing.IsDeleted
                && !existing.IsCompleted
                && DateTime.UtcNow < existing.ScheduledDateAndTime;
```

**Edge Cases:**
- Reminder becomes Active between GET and PATCH: Validation catches this at update time
- Multiple simultaneous PATCH requests: ConcurrentDictionary handles atomicity; Cosmos uses optimistic concurrency

### Error Messages

| Scenario | HTTP Status | Message |
|----------|-------------|---------|
| Missing ID in create | 400 | (ASP.NET default validation) |
| Duplicate ID | 409 | `"A reminder with ID {id} already exists."` |
| Update time on Active reminder | 400 | `"Cannot update scheduled time. Reminder is in 'Active' state."` |
| Update time on Completed reminder | 400 | `"Cannot update scheduled time. Reminder is in 'Completed' state."` |
| Update time on Deleted reminder | 404 | (existing behavior - not found) |

---

## Development Setup

```bash
# 1. Clone repo (if needed)
git clone <repo-url>
cd here-and-now-service

# 2. Restore packages
dotnet restore

# 3. Set up environment (copy template, edit values)
cp .env.example .env
# REQUIRED: Configure COSMOS_ENDPOINT and COSMOS_PRIMARY_KEY
# Options: Azure Cosmos DB account OR Azure Cosmos DB Emulator

# 4. Run the API (requires Cosmos DB)
dotnet run --project Web/HereAndNow.Web/HereAndNow.Web.csproj

# 5. Run tests (uses mocked service - no Cosmos required)
dotnet test

# 6. Access Swagger UI
# Open: http://localhost:{PORT}/swagger
```

**Note:** After Change #3, running the API requires Cosmos DB configuration. Tests use a mocked service and do not require Cosmos DB.

---

## Implementation Guide

### Setup Steps

1. Create feature branch: `git checkout -b feature/client-uuid-and-state-validation`
2. Verify dev environment: `dotnet build && dotnet test`
3. Review existing code references listed above
4. Run API locally to test current behavior

### Implementation Steps

**Story 1: Client-Provided UUID**

1. **Modify DTO** (`CreateReminderRequest.cs`):
   - Add `[Required] public Guid Id { get; init; }` property
   - Update XML documentation

2. **Modify Service Interface** (`IReminderInstanceService.cs`):
   - Add `Guid id` as first parameter to `Create()` method
   - Update XML documentation

3. **Modify Cosmos Service** (`CosmosReminderInstanceService.cs`):
   - Update `Create()` signature
   - Use `id` parameter instead of `Guid.NewGuid()`
   - Catch `CosmosException` with 409 status, rethrow as `InvalidOperationException`

4. **Modify Controller** (`ReminderInstancesController.cs`):
   - Pass `request.Id` to service
   - Wrap service call in try-catch
   - Return `Conflict()` on `InvalidOperationException`

5. **Update Tests** (`ReminderInstancesControllerTests.cs`):
   - Update existing Create tests to provide ID
   - Add test: Create with duplicate ID returns 409

**Story 2: ScheduledDateAndTime State Validation**

1. **Modify Cosmos Service** (`CosmosReminderInstanceService.cs`):
   - In `Update()`, before applying `scheduledDateAndTime`:
   - Check if reminder is in Scheduled state
   - Throw `InvalidOperationException` if not

2. **Modify Controller** (`ReminderInstancesController.cs`):
   - Catch `InvalidOperationException` in Update action
   - Return `BadRequest()` with descriptive message

3. **Add Tests** (`ReminderInstancesControllerTests.cs`):
   - Test: Update time on Scheduled reminder succeeds
   - Test: Update time on Active reminder returns 400
   - Test: Update time on Completed reminder returns 400
   - Test: Update other fields on Active/Completed succeeds

**Story 3: Remove In-Memory Service**

1. **Delete In-Memory Service** (`ReminderInstanceService.cs`):
   - Delete file: `Reminders/HereAndNow.Reminders/Services/ReminderInstanceService.cs`
   - Verify no compilation errors

2. **Update Program.cs**:
   - Remove the `useCosmosDb` conditional logic (lines 35-62)
   - Add `COSMOS_ENDPOINT` and `COSMOS_PRIMARY_KEY` to required vars check
   - Register `CosmosReminderInstanceService` unconditionally
   - Remove fallback `ReminderInstanceService` registration

3. **Update TestWebApplicationFactory.cs**:
   - Add `using Microsoft.Extensions.DependencyInjection;`
   - Add `using Moq;`
   - In `ConfigureWebHost()`, add `ConfigureTestServices()` to register mock
   - Create `Mock<IReminderInstanceService>` and register as singleton

4. **Verify Tests Pass**:
   - Run `dotnet test` to ensure all tests still pass
   - Integration tests now use mock service instead of in-memory

### Testing Strategy

**Unit Tests (Controller):**
- Mock `IReminderInstanceService`
- Test request validation (missing ID)
- Test success path (201 Created with location header)
- Test duplicate ID (409 Conflict)
- Test state validation (400 Bad Request)

**Unit Tests (Service - optional but recommended):**
- Test in-memory duplicate detection
- Test state validation logic

**Integration Tests:**
- Test full HTTP pipeline with WebApplicationFactory
- Verify 409 response format
- Verify 400 response format

### Acceptance Criteria

**Story 1: Client-Provided UUID**
- [ ] `POST /api/reminder-instances` requires `id` field in request body
- [ ] Request without `id` returns 400 Bad Request
- [ ] Request with valid new ID creates reminder with that ID (201 Created)
- [ ] Request with duplicate ID returns 409 Conflict with message
- [ ] Created reminder's ID matches the provided ID

**Story 2: ScheduledDateAndTime State Validation**
- [ ] `PATCH` with `scheduledDateAndTime` on Scheduled reminder succeeds (200 OK)
- [ ] `PATCH` with `scheduledDateAndTime` on Active reminder returns 400 Bad Request
- [ ] `PATCH` with `scheduledDateAndTime` on Completed reminder returns 400 Bad Request
- [ ] `PATCH` with only `text` on Active/Completed reminder succeeds (200 OK)
- [ ] Error message includes current state name

**Story 3: Remove In-Memory Service**
- [ ] `ReminderInstanceService.cs` file is deleted
- [ ] `Program.cs` no longer contains conditional service registration
- [ ] `Program.cs` requires Cosmos DB configuration on startup
- [ ] Application throws `InvalidOperationException` if Cosmos not configured
- [ ] `TestWebApplicationFactory` registers mock `IReminderInstanceService`
- [ ] All existing tests still pass
- [ ] Solution builds without errors

---

## Developer Resources

### File Paths Reference

| Purpose | Path |
|---------|------|
| Create DTO | `Web/HereAndNow.Web/DTOs/CreateReminderRequest.cs` |
| Update DTO | `Web/HereAndNow.Web/DTOs/UpdateReminderRequest.cs` |
| Response DTO | `Web/HereAndNow.Web/DTOs/ReminderInstanceDto.cs` |
| State Enum | `Web/HereAndNow.Web/DTOs/ReminderState.cs` |
| Controller | `Web/HereAndNow.Web/Controllers/ReminderInstancesController.cs` |
| Service Interface | `Reminders/HereAndNow.Reminders/Services/IReminderInstanceService.cs` |
| Cosmos Service | `Reminders/HereAndNow.Reminders/Services/CosmosReminderInstanceService.cs` |
| App Startup | `Web/HereAndNow.Web/Program.cs` |
| Test Factory | `Web/HereAndNow.Web.Tests/Helpers/TestWebApplicationFactory.cs` |
| Controller Tests | `Web/HereAndNow.Web.Tests/Controllers/ReminderInstancesControllerTests.cs` |
| In-Memory Service | `Reminders/HereAndNow.Reminders/Services/ReminderInstanceService.cs` **(TO BE DELETED)** |

### Key Code Locations

| Reference | File:Line | Description |
|-----------|-----------|-------------|
| Create DTO definition | `CreateReminderRequest.cs:9` | Record definition |
| Service Create method | `IReminderInstanceService.cs:34` | Interface method |
| Cosmos Create | `CosmosReminderInstanceService.cs:118` | Implementation |
| Controller Create | `ReminderInstancesController.cs:96` | POST endpoint |
| State computation | `ReminderInstanceDto.cs:61` | Switch expression |
| Cosmos Update | `CosmosReminderInstanceService.cs:164` | Update method |
| Controller Update | `ReminderInstancesController.cs:130` | PATCH endpoint |
| Conflict example | `ReminderInstancesController.cs:186` | 409 response pattern |
| Conditional service registration | `Program.cs:35-62` | To be simplified |
| Test factory | `TestWebApplicationFactory.cs:9` | To add mock registration |

### Testing Locations

| Test Type | Path |
|-----------|------|
| Controller Unit Tests | `Web/HereAndNow.Web.Tests/Controllers/ReminderInstancesControllerTests.cs` |
| Integration Tests | `Web/HereAndNow.Web.Tests/Integration/AuthorizationTests.cs` |
| Test Helper | `Web/HereAndNow.Web.Tests/Helpers/TestWebApplicationFactory.cs` |

### Documentation to Update

| Document | Update Needed |
|----------|---------------|
| `docs/api-contracts.md` | Add `id` to CreateReminderRequest schema; document 409 response; document 400 for state validation |
| `docs/data-models.md` | Update CreateReminderRequest documentation |
| `CHANGELOG.md` | Add entry for API behavior changes (if exists) |

---

## UX/UI Considerations

**No UI/UX impact** - These are backend API behavior changes only.

Client applications will need to:
1. Generate UUIDs before calling POST (e.g., `crypto.randomUUID()` in JavaScript)
2. Handle 409 Conflict response (retry with different ID or treat as success if idempotent)
3. Handle 400 Bad Request on PATCH when trying to reschedule non-Scheduled reminders

---

## Testing Approach

### Conform to Existing Test Standards

| Aspect | Convention |
|--------|------------|
| File naming | `{ClassName}Tests.cs` |
| Test organization | One test class per tested class |
| Assertion style | FluentAssertions (`result.Should().Be...`) |
| Mocking | Moq with `Setup()` and `Verify()` |
| Coverage | Focus on controller actions, service methods |

### Test Coverage Plan

**New Unit Tests:**

```
Create_WithValidNewId_Returns201Created
Create_WithDuplicateId_Returns409Conflict
Create_WithMissingId_Returns400BadRequest

Update_ScheduledTimeOnScheduledReminder_Returns200Ok
Update_ScheduledTimeOnActiveReminder_Returns400BadRequest
Update_ScheduledTimeOnCompletedReminder_Returns400BadRequest
Update_TextOnActiveReminder_Returns200Ok
Update_TextOnCompletedReminder_Returns200Ok
```

**Updated Existing Tests:**
- All existing Create tests need to include `Id` in request

---

## Deployment Strategy

### Deployment Steps

1. Merge feature branch to `main`
2. GitHub Actions CI/CD pipeline triggers automatically
3. Build → Test → Publish → Deploy to Azure App Service
4. Verify in staging/production via Swagger UI

### Rollback Plan

1. Identify the previous working commit hash
2. Create hotfix branch from that commit
3. Push to main to trigger deployment
4. Or: Use Azure App Service deployment slots to swap back

### Monitoring

| Metric | Where to Monitor |
|--------|------------------|
| 409 Conflict rate | Application Insights / Azure logs |
| 400 Bad Request rate | Application Insights / Azure logs |
| API response times | Azure App Service metrics |
| Error logs | Azure App Service log stream |

---

## Summary

This tech-spec covers three changes:

1. **Client-Provided UUID**: Shifts ID generation responsibility to the client, enabling idempotent creates and offline-first scenarios. Returns 409 Conflict on duplicates.

2. **ScheduledDateAndTime State Validation**: Prevents illogical updates by only allowing time changes when the reminder is in "Scheduled" state. Returns 400 Bad Request otherwise.

3. **Remove In-Memory Service**: Deletes the unused `ReminderInstanceService.cs`, requires Cosmos DB configuration, and updates test infrastructure to use mocks. Simplifies the codebase and eliminates maintenance burden.

All changes follow existing codebase patterns. Changes #1 and #2 are backward-compatible for clients that adapt to the new requirements. Change #3 requires developers to have Cosmos DB configured (Azure Cosmos DB or emulator).
