# Data Models

## Overview

The Here and Now Service uses domain models defined in the `HereAndNow.Reminders` assembly. Currently, data is stored in-memory using thread-safe collections, making it suitable for development and demo purposes.

**Storage:** In-memory (`ConcurrentDictionary<Guid, ReminderInstance>`)
**Persistence:** None (data lost on application restart)

---

## Domain Models

### ReminderInstance

**Namespace:** `HereAndNowService.Models`
**Location:** `Reminders/HereAndNow.Reminders/Models/ReminderInstance.cs`

Represents a reminder with scheduling and status tracking.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Id` | `Guid` | Auto-generated | Unique identifier for the reminder |
| `Text` | `string` | Yes (`required`) | The text content of the reminder |
| `ScheduledDateAndTime` | `DateTime` | No | When the reminder is scheduled |
| `Status` | `ReminderStatus` | No | Current status (defaults to first enum value) |

**C# Definition:**

```csharp
public class ReminderInstance
{
    public Guid Id { get; set; }
    public required string Text { get; set; }
    public DateTime ScheduledDateAndTime { get; set; }
    public ReminderStatus Status { get; set; }
}
```

**JSON Serialization Example:**

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "text": "Review quarterly reports",
  "scheduledDateAndTime": "2025-12-15T14:30:00Z",
  "status": "Scheduled"
}
```

---

### ReminderStatus

**Namespace:** `HereAndNowService.Models`
**Location:** `Reminders/HereAndNow.Reminders/Models/ReminderStatus.cs`
**Type:** `enum`

Represents the lifecycle status of a reminder instance.

| Value | Description |
|-------|-------------|
| `Scheduled` | Reminder is scheduled for future |
| `Active` | Reminder is currently active |
| `Completed` | Reminder has been completed |

**C# Definition:**

```csharp
public enum ReminderStatus
{
    Scheduled,
    Active,
    Completed
}
```

**JSON Serialization:** Serialized as string (configured via `JsonStringEnumConverter`)

---

### Message

**Namespace:** `HereAndNowService.Models`
**Location:** `Reminders/HereAndNow.Reminders/Models/Message.cs`

Represents a simple message response from the API.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `text` | `string?` | No | The text content of the message |

**C# Definition:**

```csharp
public class Message
{
    public string? text { get; set; }
}
```

**JSON Example:**

```json
{
  "text": "This is a protected message."
}
```

---

## Data Access Layer

### IReminderInstanceService

**Namespace:** `HereAndNowService.Services`
**Location:** `Reminders/HereAndNow.Reminders/Services/IReminderInstanceService.cs`

Service interface defining CRUD operations for reminders.

| Method | Return Type | Description |
|--------|-------------|-------------|
| `GetAll()` | `IEnumerable<ReminderInstance>` | Retrieves all reminders |
| `GetById(Guid id)` | `ReminderInstance?` | Retrieves a single reminder |
| `Create(ReminderInstance)` | `ReminderInstance` | Creates a new reminder |
| `Update(Guid id, ReminderInstance)` | `ReminderInstance?` | Updates existing reminder |
| `Delete(Guid id)` | `bool` | Deletes a reminder |

---

### ReminderInstanceService

**Namespace:** `HereAndNowService.Services`
**Location:** `Reminders/HereAndNow.Reminders/Services/ReminderInstanceService.cs`
**Lifetime:** Singleton (registered in DI container)

In-memory implementation using `ConcurrentDictionary` for thread safety.

**Key Implementation Details:**

1. **Thread Safety:** Uses `ConcurrentDictionary` for safe concurrent access
2. **ID Generation:** Auto-generates `Guid.NewGuid()` on create
3. **Optimistic Concurrency:** Uses `TryUpdate` to handle concurrent modifications
4. **Logging:** Comprehensive structured logging for all operations

**Storage Strategy:**

```csharp
private readonly ConcurrentDictionary<Guid, ReminderInstance> _reminders = new();
```

---

## Data Flow

```
┌─────────────────┐     ┌───────────────────────────┐     ┌─────────────────────────┐
│   Controller    │────▶│ IReminderInstanceService  │────▶│  ConcurrentDictionary   │
│  (API Layer)    │     │       (Interface)         │     │   (In-Memory Store)     │
└─────────────────┘     └───────────────────────────┘     └─────────────────────────┘
         │                          │
         │                          │
    HTTP Request              Business Logic
    Validation                ID Generation
    Authentication            Concurrency Control
```

---

## Future Considerations

### Database Migration Path

To add persistent storage, consider:

1. **Entity Framework Core** - Add EF Core packages and DbContext
2. **Repository Pattern** - Create `IReminderRepository` interface
3. **SQL Server / PostgreSQL** - Azure SQL or PostgreSQL for Azure
4. **Migration Strategy** - EF Migrations for schema management

### Suggested Schema (SQL)

```sql
CREATE TABLE ReminderInstances (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Text NVARCHAR(500) NOT NULL,
    ScheduledDateAndTime DATETIME2 NOT NULL,
    Status INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL
);

CREATE INDEX IX_ReminderInstances_Status ON ReminderInstances(Status);
CREATE INDEX IX_ReminderInstances_ScheduledDateAndTime ON ReminderInstances(ScheduledDateAndTime);
```

---

## Related Documentation

- [API Contracts](./api-contracts.md) - REST API endpoint documentation
- [Architecture](./architecture.md) - System architecture overview
