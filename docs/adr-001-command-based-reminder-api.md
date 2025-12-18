# ADR-001: Command-Based Reminder API Redesign

> Architecture Decision Record for transitioning from CRUD-style to command-based API semantics

---

## Status

**Proposed** | Date: 2025-12-18

---

## Context

The current Reminder API uses traditional CRUD semantics where:
- **POST** accepts a full `ReminderInstanceDto` including server-controlled fields (`Id`, `IsCompleted`, `IsDeleted`, `State`)
- **PUT** performs full resource replacement, allowing clients to modify any field including state flags

### Problems with Current Design

| Issue | Risk Level | Description |
|-------|------------|-------------|
| **Overfetching on Create** | Medium | Client sends `Id`, `IsCompleted`, `IsDeleted` which server ignores/overwrites |
| **Dangerous PUT semantics** | High | Clients can set `IsCompleted: true` and `IsDeleted: true` simultaneously or manipulate state inappropriately |
| **Missing audit trail** | Medium | No timestamps for when reminders were completed or deleted |
| **Unclear intent** | Low | "Update reminder" conflates editing content vs. completing vs. deleting |

### Current ReminderInstance Model

```csharp
public class ReminderInstance
{
    public Guid Id { get; set; }
    public string? UserId { get; set; }
    public required string Text { get; set; }
    public DateTime ScheduledDateAndTime { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsDeleted { get; set; }
    public bool ShouldPlaySound { get; set; }
    public bool ShouldDoVibration { get; set; }
}
```

---

## Decision

Transition to a **Command-Based (Task-Based) API** pattern with:
1. Separate DTOs for create and update operations
2. Dedicated endpoints for state transitions (complete, delete)
3. Server-controlled timestamps for audit trail
4. Removal of full-replacement PUT semantics

---

## Detailed Design

### 1. Updated Domain Model

Add timestamp fields to `ReminderInstance`:

```csharp
public class ReminderInstance
{
    // Existing fields
    public Guid Id { get; set; }
    public string? UserId { get; set; }
    public required string Text { get; set; }
    public DateTime ScheduledDateAndTime { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsDeleted { get; set; }
    public bool ShouldPlaySound { get; set; }
    public bool ShouldDoVibration { get; set; }

    // NEW: Audit timestamps
    public DateTime CreatedDateAndTime { get; set; }
    public DateTime? CompletedDateAndTime { get; set; }
    public DateTime? DeletedDateAndTime { get; set; }
}
```

| Field | Set By | When |
|-------|--------|------|
| `CreatedDateAndTime` | Server | On Create |
| `CompletedDateAndTime` | Server | On Complete command |
| `DeletedDateAndTime` | Server | On Delete command |

---

### 2. New Request DTOs

#### CreateReminderRequest

Used by: `POST /api/reminder-instances`

```csharp
/// <summary>
/// Request DTO for creating a new reminder.
/// Server will set: Id, UserId, CreatedDateAndTime, IsCompleted=false, IsDeleted=false
/// </summary>
public record CreateReminderRequest
{
    /// <summary>
    /// The reminder text content.
    /// </summary>
    [Required]
    [StringLength(1000, MinimumLength = 1)]
    public required string Text { get; init; }

    /// <summary>
    /// When the reminder should trigger.
    /// </summary>
    [Required]
    public DateTime ScheduledDateAndTime { get; init; }

    /// <summary>
    /// Whether to play a sound when the reminder triggers.
    /// </summary>
    public bool ShouldPlaySound { get; init; }

    /// <summary>
    /// Whether to vibrate when the reminder triggers.
    /// </summary>
    public bool ShouldDoVibration { get; init; }
}
```

**Fields intentionally excluded:**
- `Id` — Server generates via `Guid.NewGuid()`
- `UserId` — Server extracts from JWT `sub` claim
- `IsCompleted` — Always `false` on creation
- `IsDeleted` — Always `false` on creation
- `State` — Computed property
- All timestamps — Server-controlled

---

#### UpdateReminderRequest

Used by: `PATCH /api/reminder-instances/{id}`

```csharp
/// <summary>
/// Request DTO for updating reminder properties.
/// Only non-null fields will be updated (partial update semantics).
/// Cannot modify: IsCompleted, IsDeleted, timestamps.
/// </summary>
public record UpdateReminderRequest
{
    /// <summary>
    /// New reminder text. Null means no change.
    /// </summary>
    [StringLength(1000, MinimumLength = 1)]
    public string? Text { get; init; }

    /// <summary>
    /// New scheduled time. Null means no change.
    /// </summary>
    public DateTime? ScheduledDateAndTime { get; init; }

    /// <summary>
    /// New sound setting. Null means no change.
    /// </summary>
    public bool? ShouldPlaySound { get; init; }

    /// <summary>
    /// New vibration setting. Null means no change.
    /// </summary>
    public bool? ShouldDoVibration { get; init; }
}
```

**Partial Update Semantics:**
- Only fields with non-null values are updated
- Fields with `null` retain their current values
- State flags (`IsCompleted`, `IsDeleted`) cannot be modified via this endpoint

---

### 3. Updated Response DTO

```csharp
/// <summary>
/// Response DTO for reminder data.
/// </summary>
public record ReminderInstanceDto
{
    public Guid Id { get; init; }
    public required string Text { get; init; }
    public DateTime ScheduledDateAndTime { get; init; }
    public bool IsCompleted { get; init; }
    public bool IsDeleted { get; init; }
    public bool ShouldPlaySound { get; init; }
    public bool ShouldDoVibration { get; init; }

    // NEW: Audit timestamps
    public DateTime CreatedDateAndTime { get; init; }
    public DateTime? CompletedDateAndTime { get; init; }
    public DateTime? DeletedDateAndTime { get; init; }

    // Computed property (unchanged)
    public ReminderState State => IsDeleted ? ReminderState.Deleted
        : IsCompleted ? ReminderState.Completed
        : DateTime.UtcNow >= ScheduledDateAndTime ? ReminderState.Active
        : ReminderState.Scheduled;
}
```

---

### 4. Endpoint Changes

#### Summary Table

| Current | Proposed | Method | Description |
|---------|----------|--------|-------------|
| `POST /api/reminder-instances` | `POST /api/reminder-instances` | POST | Create reminder (new DTO) |
| `PUT /api/reminder-instances/{id}` | **REMOVED** | - | Full replacement removed |
| - | `PATCH /api/reminder-instances/{id}` | PATCH | Partial update (new endpoint) |
| - | `POST /api/reminder-instances/{id}/complete` | POST | Complete reminder (new endpoint) |
| `DELETE /api/reminder-instances/{id}` | `DELETE /api/reminder-instances/{id}` | DELETE | Soft-delete (unchanged route, adds timestamp) |
| `GET /api/reminder-instances` | `GET /api/reminder-instances` | GET | List reminders (unchanged) |
| `GET /api/reminder-instances/{id}` | `GET /api/reminder-instances/{id}` | GET | Get single reminder (unchanged) |

---

#### POST /api/reminder-instances (Create)

**Request Body:** `CreateReminderRequest`

```json
{
  "text": "Take medication",
  "scheduledDateAndTime": "2025-12-18T10:00:00Z",
  "shouldPlaySound": true,
  "shouldDoVibration": false
}
```

**Response (201 Created):** `ReminderInstanceDto`

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "text": "Take medication",
  "scheduledDateAndTime": "2025-12-18T10:00:00Z",
  "isCompleted": false,
  "isDeleted": false,
  "shouldPlaySound": true,
  "shouldDoVibration": false,
  "createdDateAndTime": "2025-12-18T09:30:00Z",
  "completedDateAndTime": null,
  "deletedDateAndTime": null,
  "state": "Scheduled"
}
```

**Server Actions:**
1. Generate `Id = Guid.NewGuid()`
2. Extract `UserId` from JWT `sub` claim
3. Set `IsCompleted = false`, `IsDeleted = false`
4. Set `CreatedDateAndTime = DateTime.UtcNow`
5. Set `CompletedDateAndTime = null`, `DeletedDateAndTime = null`

---

#### PATCH /api/reminder-instances/{id} (Update)

**Request Body:** `UpdateReminderRequest`

```json
{
  "text": "Take medication with food",
  "scheduledDateAndTime": "2025-12-18T12:00:00Z"
}
```

**Response (200 OK):** `ReminderInstanceDto` with updated values

| Status Code | Condition |
|-------------|-----------|
| 200 OK | Successfully updated |
| 400 Bad Request | Validation failure (empty text, etc.) |
| 401 Unauthorized | Missing or invalid token |
| 404 Not Found | Reminder not found or belongs to different user |

**Server Actions:**
1. Load existing reminder by `id` and `userId`
2. For each non-null field in request, update the corresponding property
3. Preserve all other fields including state flags and timestamps
4. Save and return updated reminder

---

#### POST /api/reminder-instances/{id}/complete (Complete)

**Request Body:** None required

**Response (200 OK):** `ReminderInstanceDto`

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "text": "Take medication",
  "scheduledDateAndTime": "2025-12-18T10:00:00Z",
  "isCompleted": true,
  "isDeleted": false,
  "shouldPlaySound": true,
  "shouldDoVibration": false,
  "createdDateAndTime": "2025-12-18T09:30:00Z",
  "completedDateAndTime": "2025-12-18T10:15:00Z",
  "deletedDateAndTime": null,
  "state": "Completed"
}
```

| Status Code | Condition |
|-------------|-----------|
| 200 OK | Successfully completed (or already completed - idempotent) |
| 401 Unauthorized | Missing or invalid token |
| 404 Not Found | Reminder not found or belongs to different user |
| 409 Conflict | Reminder is deleted (cannot complete deleted reminder) |

**Server Actions:**
1. Load existing reminder by `id` and `userId`
2. If `IsDeleted == true`, return 409 Conflict
3. If already completed, return current state (idempotent)
4. Set `IsCompleted = true`
5. Set `CompletedDateAndTime = DateTime.UtcNow`
6. Save and return updated reminder

**Idempotency:** Calling complete on an already-completed reminder returns 200 OK with current state.

---

#### DELETE /api/reminder-instances/{id} (Soft Delete)

**Request Body:** None

**Response (204 No Content)**

| Status Code | Condition |
|-------------|-----------|
| 204 No Content | Successfully deleted (or already deleted - idempotent) |
| 401 Unauthorized | Missing or invalid token |
| 404 Not Found | Reminder not found or belongs to different user |

**Server Actions:**
1. Load existing reminder by `id` and `userId`
2. If already deleted, return 204 (idempotent)
3. Set `IsDeleted = true`
4. Set `DeletedDateAndTime = DateTime.UtcNow`
5. Save reminder

---

### 5. Service Interface Changes

```csharp
public interface IReminderInstanceService
{
    // Unchanged
    IEnumerable<ReminderInstance> GetAll(string userId);
    ReminderInstance? GetById(Guid id, string userId);

    // Modified: Takes only user-provided fields
    ReminderInstance Create(
        string userId,
        string text,
        DateTime scheduledDateAndTime,
        bool shouldPlaySound,
        bool shouldDoVibration);

    // New: Partial update
    ReminderInstance? Update(
        Guid id,
        string userId,
        string? text = null,
        DateTime? scheduledDateAndTime = null,
        bool? shouldPlaySound = null,
        bool? shouldDoVibration = null);

    // New: Complete command
    ReminderInstance? Complete(Guid id, string userId);

    // Modified: Soft delete with timestamp
    bool Delete(Guid id, string userId);
}
```

---

## Migration Considerations

### Cosmos DB Document Migration

Existing documents lack the new timestamp fields. Options:

| Approach | Pros | Cons | Recommendation |
|----------|------|------|----------------|
| **Lazy Migration** | No downtime, simple | Old docs have null timestamps | **Recommended** |
| **Batch Migration** | All docs consistent | Requires RU budget, script | For later |
| **Default Values** | Consistency | Loses "unknown" semantics | Not recommended |

**Lazy Migration Strategy:**
1. New fields are nullable in model
2. Code handles null timestamps gracefully
3. When a reminder is updated/completed/deleted, timestamps are set
4. Old reminders get timestamps as they're interacted with

### Client Migration

1. **Android App**: Update to use new DTOs, remove server-controlled fields from create requests
2. **SPA**: Same updates for create forms
3. **Versioning**: Consider API versioning if backward compatibility needed

```
Option A: Clean break (recommended for MVP)
  - Update all clients simultaneously
  - Remove old endpoints

Option B: Versioned API
  - Add /v2/reminder-instances with new behavior
  - Deprecate /v1 after client migration
```

---

## Consequences

### Positive

- **Explicit intent**: Actions like "complete" and "delete" are clear commands
- **Server authority**: Clients cannot manipulate state flags or timestamps
- **Audit trail**: Full lifecycle timestamps for compliance/debugging
- **Safer updates**: Partial updates prevent accidental data loss
- **Idempotent operations**: Safe retry behavior for mobile clients

### Negative

- **Breaking change**: Clients must update to new DTOs
- **More endpoints**: One additional endpoint (`/complete`)
- **Migration effort**: Existing documents need timestamp handling

### Risks & Mitigations

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Client breaks on deploy | Medium | High | Coordinate client/server deploy |
| Old documents cause null errors | Low | Medium | Defensive null handling |
| Complete endpoint misuse | Low | Low | Idempotent design |

---

## Implementation Order

1. **Model Changes**: Add timestamp fields to `ReminderInstance` and `ReminderDocument`
2. **New DTOs**: Create `CreateReminderRequest` and `UpdateReminderRequest`
3. **Service Layer**: Update `IReminderInstanceService` and implementations
4. **Controller**: Add PATCH and Complete endpoints, update POST
5. **Remove PUT**: Delete the full-replacement endpoint
6. **Update Response DTO**: Add timestamp fields
7. **Tests**: Update unit and integration tests
8. **Documentation**: Update api-contracts.md and architecture.md

---

## Related Documents

- [API Contracts](./api-contracts.md) — Will be updated after implementation
- [Architecture](./architecture.md) — API Design section will be updated
- [Data Models](./data-models.md) — Will reflect new model fields

---

## Decision Outcome

**Approved** — Proceed with implementation following the design above.

---

## Document Metadata

| Field | Value |
|-------|-------|
| **Author** | Winston (Architect) |
| **Created** | 2025-12-18 |
| **Status** | Proposed |
| **Stakeholders** | Mike (Product Owner) |
