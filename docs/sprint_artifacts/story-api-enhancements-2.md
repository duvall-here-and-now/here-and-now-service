# Story 1.2: ScheduledDateAndTime State Validation

**Status:** Review

---

## User Story

As a **client application developer**,
I want **the API to reject scheduled time updates on non-Scheduled reminders**,
So that **I receive clear feedback when attempting invalid operations**.

---

## Acceptance Criteria

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

---

## Implementation Details

### Tasks / Subtasks

- [x] **Task 1: Add State Check in In-Memory Service** (AC: #1, #2, #3, #4)
  - In `Update()` method, before applying `scheduledDateAndTime`:
  - Compute current state using inline logic
  - If not Scheduled and `scheduledDateAndTime.HasValue`, throw `InvalidOperationException`
  - Include state name in exception message
  - File: `Reminders/HereAndNow.Reminders/Services/ReminderInstanceService.cs`

- [x] **Task 2: Add State Check in Cosmos Service** (AC: #1, #2, #3, #4)
  - Same validation logic as in-memory service
  - In `Update()` method, check state before applying time update
  - Throw `InvalidOperationException` with state name if not Scheduled
  - File: `Reminders/HereAndNow.Reminders/Services/CosmosReminderInstanceService.cs`

- [x] **Task 3: Update Controller to Handle State Validation** (AC: #2, #3)
  - In `Update()` action, catch `InvalidOperationException`
  - Distinguish between duplicate ID (409) and state validation (400)
  - Return `BadRequest(new { message = ex.Message })` for state validation
  - File: `Web/HereAndNow.Web/Controllers/ReminderInstancesController.cs`

- [x] **Task 4: Add Unit Tests for State Validation** (AC: #1, #2, #3, #4)
  - `Update_ScheduledTimeOnScheduledReminder_Returns200Ok`
  - `Update_ScheduledTimeOnActiveReminder_Returns400BadRequest`
  - `Update_ScheduledTimeOnCompletedReminder_Returns400BadRequest`
  - `Update_TextOnActiveReminder_Returns200Ok`
  - `Update_TextOnCompletedReminder_Returns200Ok`
  - File: `Web/HereAndNow.Web.Tests/Controllers/ReminderInstancesControllerTests.cs`

### Technical Summary

This story adds business rule validation to the PATCH endpoint. The `ScheduledDateAndTime` field can only be updated when the reminder is in the "Scheduled" state. Other fields (Text, ShouldPlaySound, ShouldDoVibration) remain updatable regardless of state (except Deleted, which already returns 404).

**State Computation Logic:**
```csharp
bool isScheduled = !existing.IsDeleted
                && !existing.IsCompleted
                && DateTime.UtcNow < existing.ScheduledDateAndTime;
```

**State Priority:**
1. IsDeleted → "Deleted" (returns 404, existing behavior)
2. IsCompleted → "Completed"
3. now >= ScheduledDateAndTime → "Active"
4. Otherwise → "Scheduled"

### Project Structure Notes

- **Files to modify:**
  - `Reminders/HereAndNow.Reminders/Services/ReminderInstanceService.cs`
  - `Reminders/HereAndNow.Reminders/Services/CosmosReminderInstanceService.cs`
  - `Web/HereAndNow.Web/Controllers/ReminderInstancesController.cs`
  - `Web/HereAndNow.Web.Tests/Controllers/ReminderInstancesControllerTests.cs`

- **Expected test locations:** `Web/HereAndNow.Web.Tests/Controllers/`

- **Estimated effort:** 2 story points (1-2 hours)

- **Prerequisites:** Story 1.1 (for consistent exception handling pattern in controller)

### Key Code References

| Reference | Location | Description |
|-----------|----------|-------------|
| State computation | `ReminderInstanceDto.cs:61-67` | Switch expression for state |
| In-memory Update | `ReminderInstanceService.cs:105-158` | Update method to modify |
| Cosmos Update | `CosmosReminderInstanceService.cs:164-214` | Update method to modify |
| Controller Update | `ReminderInstancesController.cs:130-151` | PATCH endpoint |
| IsDeleted check | `ReminderInstanceService.cs:127-131` | Pattern for validation |

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

**2025-12-19 Implementation Plan:**
- Task 1: Add state validation to `ReminderInstanceService.Update()` - check if reminder is Scheduled before allowing `scheduledDateAndTime` update
- Task 2: Mirror the same validation in `CosmosReminderInstanceService.Update()`
- Task 3: Update controller to catch `InvalidOperationException` from Update and return 400 Bad Request
- Task 4: Add 5 unit tests covering all acceptance criteria scenarios

**State computation logic (inline):**
```csharp
// Compute state: Completed > Active > Scheduled
string currentState;
if (existing.IsCompleted)
    currentState = "Completed";
else if (DateTime.UtcNow >= existing.ScheduledDateAndTime)
    currentState = "Active";
else
    currentState = "Scheduled";
```

### Completion Notes

**2025-12-19:** All tasks completed successfully.

- Added state validation logic to both service implementations (in-memory and Cosmos)
- State is computed inline: Completed > Active > Scheduled (Deleted already returns null/404)
- Controller now catches `InvalidOperationException` from Update and returns 400 Bad Request
- Error messages include the current state name for clarity
- All 5 acceptance criteria tests pass, plus existing regression tests

### Files Modified

| File | Change Type | Description |
|------|-------------|-------------|
| `Reminders/HereAndNow.Reminders/Services/ReminderInstanceService.cs` | Modified | Added state validation before scheduledDateAndTime update |
| `Reminders/HereAndNow.Reminders/Services/CosmosReminderInstanceService.cs` | Modified | Added state validation before scheduledDateAndTime update |
| `Web/HereAndNow.Web/Controllers/ReminderInstancesController.cs` | Modified | Added try-catch for InvalidOperationException, returns 400 |
| `Web/HereAndNow.Web.Tests/Controllers/ReminderInstancesControllerTests.cs` | Modified | Added 5 new state validation tests |

### Test Results

```
Test Run Successful.
Total tests: 36
     Passed: 36
 Total time: 1.1942 Seconds
```

**New tests added:**
- `Update_ScheduledTimeOnScheduledReminder_Returns200Ok` ✅
- `Update_ScheduledTimeOnActiveReminder_Returns400BadRequest` ✅
- `Update_ScheduledTimeOnCompletedReminder_Returns400BadRequest` ✅
- `Update_TextOnActiveReminder_Returns200Ok` ✅
- `Update_TextOnCompletedReminder_Returns200Ok` ✅

---

## Review Notes

<!-- Will be populated during code review -->
