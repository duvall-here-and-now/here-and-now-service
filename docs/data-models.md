# Here and Now Service - Data Models

**Date:** 2025-12-29

## Overview

This document describes the domain models and data structures used in the Here and Now Service. The project follows a clean separation between domain models (business logic layer) and DTOs (API contract layer).

## Domain Models

Located in: `Reminders/HereAndNow.Reminders/Models/`

### ReminderInstance

**File:** `ReminderInstance.cs`
**Purpose:** Represents a reminder with scheduling, completion tracking, and notification preferences.

```csharp
public class ReminderInstance
{
    public Guid Id { get; set; }
    public required string Text { get; set; }
    public DateTime ScheduledDateAndTime { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsDeleted { get; set; }
    public bool ShouldPlaySound { get; set; }
    public bool ShouldDoVibration { get; set; }
}
```

**Field Descriptions:**

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Unique identifier (auto-generated on create) |
| Text | string | The reminder text content (required) |
| ScheduledDateAndTime | DateTime | When the reminder should trigger |
| IsCompleted | bool | Whether the reminder has been marked done |
| IsDeleted | bool | Soft-delete flag (true = deleted) |
| ShouldPlaySound | bool | Enable audio notification |
| ShouldDoVibration | bool | Enable vibration notification |

**Notes:**
- XML comment indicates this model maps to Cosmos DB storage schema (future implementation)
- Uses `required` modifier for Text property (C# 11+ feature)
- Soft-delete pattern: records are never physically deleted

---

### Message

**File:** `Message.cs`
**Purpose:** Simple message response model for the messages API.

```csharp
public class Message
{
    public string? text { get; set; }
}
```

**Field Descriptions:**

| Field | Type | Description |
|-------|------|-------------|
| text | string? | The message content (nullable) |

**Notes:**
- Uses lowercase `text` property name (matches JSON convention)
- Used for demo endpoints showing public/protected/admin messages

---

## Data Transfer Objects (DTOs)

Located in: `Web/HereAndNow.Web/DTOs/`

DTOs are separate from domain models to:
1. Control what's exposed in the API
2. Add computed properties for API consumers
3. Allow API contract evolution independent of domain changes

### ReminderInstanceDto

**File:** `ReminderInstanceDto.cs`
**Purpose:** API contract for reminder instances with computed state.

```csharp
public class ReminderInstanceDto
{
    public Guid Id { get; set; }
    public required string Text { get; set; }
    public DateTime ScheduledDateAndTime { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsDeleted { get; set; }
    public bool ShouldPlaySound { get; set; }
    public bool ShouldDoVibration { get; set; }

    // Computed property
    public ReminderState State => this switch
    {
        { IsDeleted: true } => ReminderState.Deleted,
        { IsCompleted: true } => ReminderState.Completed,
        _ when DateTime.UtcNow >= ScheduledDateAndTime => ReminderState.Active,
        _ => ReminderState.Scheduled
    };
}
```

**Key Difference from Domain Model:**
- Includes computed `State` property using C# pattern matching
- State is calculated on-the-fly based on flags and current time

---

### ReminderState (Enum)

**File:** `ReminderState.cs`
**Purpose:** Represents the lifecycle state of a reminder.

```csharp
public enum ReminderState
{
    Scheduled,  // Future reminder
    Active,     // Time has passed, awaiting action
    Completed,  // Marked as done
    Deleted     // Soft-deleted
}
```

**State Transition Logic:**

```
┌─────────────┐
│  Scheduled  │ ← Initial state (future time)
└──────┬──────┘
       │ (time passes)
       ▼
┌─────────────┐
│   Active    │ ← ScheduledDateAndTime <= Now
└──────┬──────┘
       │ (user marks complete)
       ▼
┌─────────────┐
│  Completed  │ ← IsCompleted = true
└─────────────┘

Any state can transition to:
┌─────────────┐
│   Deleted   │ ← IsDeleted = true (takes priority)
└─────────────┘
```

---

## Mappers

Located in: `Web/HereAndNow.Web/Mappers/`

### ReminderInstanceMapper

**File:** `ReminderInstanceMapper.cs`
**Purpose:** Converts between domain models and DTOs.

```csharp
public static class ReminderInstanceMapper
{
    public static ReminderInstanceDto ToDto(ReminderInstance domain);
    public static ReminderInstance ToDomain(ReminderInstanceDto dto);
    public static IEnumerable<ReminderInstanceDto> ToDtos(IEnumerable<ReminderInstance> domains);
}
```

**Usage Pattern:**
```csharp
// Controller receiving request
var domain = ReminderInstanceMapper.ToDomain(requestDto);
var created = _service.Create(domain);
var responseDto = ReminderInstanceMapper.ToDto(created);
```

---

## Data Storage

### Current Implementation

The service currently uses **in-memory storage** via `ConcurrentDictionary`:

```csharp
// In ReminderInstanceService.cs
private readonly ConcurrentDictionary<Guid, ReminderInstance> _reminders = new();
```

**Characteristics:**
- Thread-safe for concurrent access
- Data is lost on application restart
- Suitable for development/demo purposes

### Future Storage (Indicated)

XML comments in `ReminderInstance.cs` indicate planned Cosmos DB integration:
```csharp
/// This model maps directly to the Cosmos DB storage schema.
```

---

## Data Relationships

```
┌─────────────────────────┐
│    ReminderInstance     │ (Domain Model)
│   - Business Logic      │
│   - Storage Schema      │
└───────────┬─────────────┘
            │
            │ ReminderInstanceMapper
            │
            ▼
┌─────────────────────────┐
│   ReminderInstanceDto   │ (API Contract)
│   - Computed State      │
│   - JSON Serialization  │
└─────────────────────────┘
```

---

## JSON Serialization

The API uses `System.Text.Json` with the following configuration:

```csharp
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
```

**Effect:** Enums (like `ReminderState`) are serialized as strings, not integers.

**Example Response:**
```json
{
  "id": "...",
  "state": "Scheduled"  // String, not 0
}
```

---

## Validation

Currently, validation is minimal:
- `required` modifier on `Text` property enforces non-null
- ID mismatch check in PUT endpoint

**Recommendation for future:** Consider adding FluentValidation or Data Annotations for more robust validation.

---

_Generated using BMAD Method `document-project` workflow_
