# here-and-now-service - Epic Breakdown

**Date:** 2025-12-19
**Project Level:** Quick Flow (Brownfield)
**Tech-Spec Reference:** [tech-spec.md](./tech-spec.md)

---

## Epic 1: API Enhancement and Codebase Simplification

**Slug:** api-enhancements

### Goal

Improve the Reminder API's robustness and simplify the codebase by:
1. Enabling client-controlled reminder IDs for idempotent operations
2. Enforcing business rules around reminder state transitions
3. Removing unnecessary code duplication

### Scope

**In Scope:**
- Require client-provided UUID on reminder creation
- Validate reminder state before allowing scheduled time updates
- Remove the unused in-memory service implementation
- Update test infrastructure to use mocks

**Out of Scope:**
- Changes to Complete/Delete endpoints
- Modifications to State computation logic
- Database schema changes
- UI/client application changes

### Success Criteria

- [ ] All 3 stories completed and merged
- [ ] All existing tests pass
- [ ] New tests added for new functionality
- [ ] API documentation updated
- [ ] Codebase reduced by ~270 lines (in-memory service removal)
- [ ] Solution builds and deploys successfully

### Dependencies

- Azure Cosmos DB access for development
- No external dependencies or third-party integrations

---

## Story Map - Epic 1

```
Epic: API Enhancement and Codebase Simplification
├── Story 1.1: Client-Provided UUID on Create
│   ├── Add Id field to CreateReminderRequest
│   ├── Update service interface and Cosmos implementation
│   ├── Handle 409 Conflict for duplicates
│   └── Update tests
│
├── Story 1.2: ScheduledDateAndTime State Validation
│   ├── Add state check in Cosmos service Update method
│   ├── Return 400 Bad Request for non-Scheduled reminders
│   └── Add tests for state validation
│
└── Story 1.3: Remove In-Memory Service (MUST BE LAST)
    ├── Delete ReminderInstanceService.cs
    ├── Update Program.cs to require Cosmos
    ├── Update TestWebApplicationFactory with mock
    └── Verify all tests pass
```

**Implementation Order:** Stories MUST be implemented in order (1.1 → 1.2 → 1.3).
Story 1.3 removes the in-memory service and should only be done after Stories 1.1 and 1.2 are complete to avoid compilation errors during development.

---

## Stories - Epic 1

### Story 1.1: Client-Provided UUID on Create

As a **client application developer**,
I want **to provide the reminder ID when creating a reminder**,
So that **I can implement idempotent retry logic and offline-first patterns**.

**Acceptance Criteria:**

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

**Prerequisites:** None

**Technical Notes:** See [tech-spec.md](./tech-spec.md) → Story 1 Implementation Steps

**Estimated Effort:** 3 points (2-3 hours)

---

### Story 1.2: ScheduledDateAndTime State Validation

As a **client application developer**,
I want **the API to reject scheduled time updates on non-Scheduled reminders**,
So that **I receive clear feedback when attempting invalid operations**.

**Acceptance Criteria:**

1. **Given** a reminder in "Scheduled" state (future time, not completed, not deleted)
   **When** I send a PATCH with `scheduledDateAndTime`
   **Then** the update succeeds (200 OK) with the updated reminder

2. **Given** a reminder in "Active" state (time has passed, not completed, not deleted)
   **When** I send a PATCH with `scheduledDateAndTime`
   **Then** the server returns 400 Bad Request with message "Cannot update scheduled time. Reminder is in 'Active' state."

3. **Given** a reminder in "Completed" state
   **When** I send a PATCH with `scheduledDateAndTime`
   **Then** the server returns 400 Bad Request with message "Cannot update scheduled time. Reminder is in 'Completed' state."

4. **Given** a reminder in "Active" or "Completed" state
   **When** I send a PATCH with only `text`, `shouldPlaySound`, or `shouldDoVibration`
   **Then** the update succeeds (200 OK)

**Prerequisites:** Story 1.1 (for consistent exception handling pattern)

**Technical Notes:** See [tech-spec.md](./tech-spec.md) → Story 2 Implementation Steps

**Estimated Effort:** 2 points (1-2 hours)

---

### Story 1.3: Remove In-Memory Service

As a **developer maintaining this codebase**,
I want **the unused in-memory service implementation removed**,
So that **the codebase is simpler and has less maintenance burden**.

**Acceptance Criteria:**

1. **Given** the file `ReminderInstanceService.cs`
   **When** the story is complete
   **Then** the file no longer exists in the repository

2. **Given** the application startup (Program.cs)
   **When** Cosmos DB environment variables are not configured
   **Then** the application throws `InvalidOperationException` with clear error message

3. **Given** the application startup
   **When** Cosmos DB is properly configured
   **Then** the application starts and registers `CosmosReminderInstanceService`

4. **Given** the test project
   **When** integration tests run
   **Then** they use a mocked `IReminderInstanceService` (no Cosmos required)

5. **Given** all changes are complete
   **When** `dotnet build` and `dotnet test` are run
   **Then** both succeed with no errors

**Prerequisites:** Stories 1.1 and 1.2 must be complete

**Technical Notes:** See [tech-spec.md](./tech-spec.md) → Story 3 Implementation Steps

**Estimated Effort:** 2 points (1-2 hours)

---

## Implementation Timeline - Epic 1

**Total Story Points:** 7 points

**Estimated Timeline:** 4-7 hours

**Recommended Approach:**
1. Complete Story 1.1 and 1.2 first (can be done in either order, but 1.1 establishes exception pattern)
2. Run all tests to verify no regressions
3. Complete Story 1.3 last (removes in-memory service)
4. Final verification: `dotnet build && dotnet test`
5. Update API documentation

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Cosmos DB unavailable | Low | High | Tests use mocks; dev needs Cosmos access |
| Integration test failures | Medium | Medium | Mock setup in TestWebApplicationFactory |
| Breaking existing clients | Medium | High | Clear API documentation; 400/409 are standard HTTP |

---
