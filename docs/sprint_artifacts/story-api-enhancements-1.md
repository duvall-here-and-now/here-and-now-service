# Story 1.1: Client-Provided UUID on Create

**Status:** Review

---

## User Story

As a **client application developer**,
I want **to provide the reminder ID when creating a reminder**,
So that **I can implement idempotent retry logic and offline-first patterns**.

---

## Acceptance Criteria

1. **Given** a valid POST request with a new UUID in the `id` field
   **When** the request is processed
   **Then** the reminder is created with that exact ID (201 Created)

2. **Given** a POST request without an `id` field
   **When** the request is processed
   **Then** the server returns 400 Bad Request with validation error

3. **Given** a POST request with an `id` that already exists for the user
   **When** the request is processed
   **Then** the server returns 409 Conflict with message "A reminder with ID {id} already exists."

4. **Given** a successful create response
   **When** I check the returned reminder
   **Then** the `id` matches the ID I provided in the request

---

## Implementation Details

### Tasks / Subtasks

- [x] **Task 1: Modify CreateReminderRequest DTO** (AC: #1, #2)
  - Add `[Required] public Guid Id { get; init; }` property
  - Add XML documentation for the new property
  - File: `Web/HereAndNow.Web/DTOs/CreateReminderRequest.cs`

- [x] **Task 2: Update Service Interface** (AC: #1)
  - Add `Guid id` as first parameter to `Create()` method
  - Update XML documentation
  - File: `Reminders/HereAndNow.Reminders/Services/IReminderInstanceService.cs`

- [x] **Task 3: Update In-Memory Service** (AC: #1, #3)
  - Update `Create()` signature to accept `id` parameter
  - Use provided `id` instead of `Guid.NewGuid()`
  - Check `TryAdd()` return value, throw `InvalidOperationException` if false
  - File: `Reminders/HereAndNow.Reminders/Services/ReminderInstanceService.cs`

- [x] **Task 4: Update Cosmos Service** (AC: #1, #3)
  - Update `Create()` signature to accept `id` parameter
  - Use provided `id` instead of `Guid.NewGuid()`
  - Catch `CosmosException` with 409 status, rethrow as `InvalidOperationException`
  - File: `Reminders/HereAndNow.Reminders/Services/CosmosReminderInstanceService.cs`

- [x] **Task 5: Update Controller** (AC: #1, #3, #4)
  - Pass `request.Id` to service `Create()` method
  - Wrap service call in try-catch for `InvalidOperationException`
  - Return `Conflict(new { message = "..." })` on duplicate
  - File: `Web/HereAndNow.Web/Controllers/ReminderInstancesController.cs`

- [x] **Task 6: Update Existing Tests** (AC: #1, #4)
  - Update all existing Create tests to provide `Id` in request
  - File: `Web/HereAndNow.Web.Tests/Controllers/ReminderInstancesControllerTests.cs`

- [x] **Task 7: Add New Tests** (AC: #2, #3)
  - Add test: `Create_WithDuplicateId_ShouldReturn409Conflict`
  - Add test: `Create_WithClientProvidedId_ShouldReturnSameIdInResponse`
  - File: `Web/HereAndNow.Web.Tests/Controllers/ReminderInstancesControllerTests.cs`

### Technical Summary

This story shifts reminder ID generation from server-side to client-side. The client provides a UUID in the POST request body, and the server uses that ID when creating the reminder. If the ID already exists for that user, the server returns 409 Conflict.

**Key Implementation Points:**
- ASP.NET `[Required]` attribute handles missing ID validation (400)
- Cosmos DB `CreateItemAsync()` throws 409 on duplicate document ID
- In-memory `ConcurrentDictionary.TryAdd()` returns false on duplicate

### Project Structure Notes

- **Files to modify:**
  - `Web/HereAndNow.Web/DTOs/CreateReminderRequest.cs`
  - `Reminders/HereAndNow.Reminders/Services/IReminderInstanceService.cs`
  - `Reminders/HereAndNow.Reminders/Services/ReminderInstanceService.cs`
  - `Reminders/HereAndNow.Reminders/Services/CosmosReminderInstanceService.cs`
  - `Web/HereAndNow.Web/Controllers/ReminderInstancesController.cs`
  - `Web/HereAndNow.Web.Tests/Controllers/ReminderInstancesControllerTests.cs`

- **Expected test locations:** `Web/HereAndNow.Web.Tests/Controllers/`

- **Estimated effort:** 3 story points (2-3 hours)

- **Prerequisites:** None

### Key Code References

| Reference | Location | Description |
|-----------|----------|-------------|
| Existing Required field | `CreateReminderRequest.cs:14-16` | Pattern for `[Required]` attribute |
| Service Create signature | `IReminderInstanceService.cs:34-39` | Current method signature |
| In-memory Create | `ReminderInstanceService.cs:63-100` | Implementation to update |
| Cosmos Create | `CosmosReminderInstanceService.cs:118-159` | Implementation to update |
| Controller Create | `ReminderInstancesController.cs:96-116` | POST endpoint |
| Conflict pattern | `ReminderInstancesController.cs:183-187` | Existing 409 response example |

---

## Context References

**Tech-Spec:** [tech-spec.md](../tech-spec.md) - Primary context document containing:

- Brownfield codebase analysis
- ASP.NET Core 8.0 framework details with versions
- Existing patterns to follow (record DTOs, structured logging, FluentAssertions)
- Integration points and dependencies
- Complete implementation guidance

**Architecture:** See tech-spec.md → Context → Existing Codebase Structure

---

## Dev Agent Record

### Agent Model Used

Claude Opus 4.5 (claude-opus-4-5-20251101)

### Debug Log References

**Implementation Plan (2025-12-19):**
1. Added `[Required] public Guid Id { get; init; }` to CreateReminderRequest DTO
2. Updated `IReminderInstanceService.Create()` signature to accept `Guid id` as first parameter
3. Modified both service implementations (in-memory and Cosmos) to use provided ID and throw `InvalidOperationException` on duplicates
4. Updated controller to pass `request.Id` to service and handle 409 Conflict response
5. Updated existing tests and added new tests for duplicate ID handling

### Completion Notes

**Story completed successfully.** All 7 tasks implemented and verified:

- DTO updated with required `Id` field and XML documentation
- Service interface modified with new signature and exception documentation
- In-memory service throws `InvalidOperationException` when `TryAdd()` returns false
- Cosmos service catches `CosmosException` with 409 status and rethrows as `InvalidOperationException`
- Controller passes client-provided ID and handles conflict response
- All existing Create tests updated to include `Id` in request
- 2 new tests added: `Create_WithDuplicateId_ShouldReturn409Conflict` and `Create_WithClientProvidedId_ShouldReturnSameIdInResponse`

**All acceptance criteria satisfied:**
- AC #1: POST with new UUID creates reminder with that exact ID (201 Created)
- AC #2: POST without `id` returns 400 Bad Request (via `[Required]` attribute validation)
- AC #3: POST with duplicate ID returns 409 Conflict with message
- AC #4: Returned reminder ID matches client-provided ID

### Files Modified

| File | Action | Description |
|------|--------|-------------|
| `Web/HereAndNow.Web/DTOs/CreateReminderRequest.cs` | Modified | Added `[Required] public Guid Id { get; init; }` property with XML docs |
| `Reminders/HereAndNow.Reminders/Services/IReminderInstanceService.cs` | Modified | Added `Guid id` parameter to `Create()` method signature |
| `Reminders/HereAndNow.Reminders/Services/ReminderInstanceService.cs` | Modified | Updated `Create()` to use provided ID and throw on duplicate |
| `Reminders/HereAndNow.Reminders/Services/CosmosReminderInstanceService.cs` | Modified | Updated `Create()` to use provided ID and handle Cosmos 409 Conflict |
| `Web/HereAndNow.Web/Controllers/ReminderInstancesController.cs` | Modified | Pass `request.Id` to service, handle 409 Conflict, added ProducesResponseType |
| `Web/HereAndNow.Web.Tests/Controllers/ReminderInstancesControllerTests.cs` | Modified | Updated existing tests, added 2 new tests |

### Test Results

```
Test Run Successful.
Total tests: 31
     Passed: 31
 Total time: 3.19 Seconds
```

**New tests added:**
- `Create_WithDuplicateId_ShouldReturn409Conflict` - Verifies 409 Conflict on duplicate
- `Create_WithClientProvidedId_ShouldReturnSameIdInResponse` - Verifies ID matches in response

**Updated tests:**
- `Create_ShouldReturnCreatedAtAction` - Now includes `Id` in request
- `Create_ShouldSetServerControlledFields` - Now includes `Id` in request

---

## Review Notes

<!-- Will be populated during code review -->
