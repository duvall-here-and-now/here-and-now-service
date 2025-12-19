# Data Models

> Domain models, DTOs, and persistence layer documentation

---

## Overview

| Attribute | Value |
|-----------|-------|
| **Primary Storage** | Azure Cosmos DB (NoSQL) |
| **Fallback Storage** | In-memory (ConcurrentDictionary) |
| **Partition Strategy** | User ID (multi-tenant isolation) |
| **Delete Strategy** | Soft delete (`isDeleted` flag) |

---

## Storage Architecture

```
┌─────────────────┐     ┌───────────────────────────┐
│   Controller    │────▶│ IReminderInstanceService  │
│  (API Layer)    │     │       (Interface)         │
└─────────────────┘     └───────────────────────────┘
                                    │
                    ┌───────────────┴───────────────┐
                    ▼                               ▼
        ┌───────────────────────┐     ┌───────────────────────────┐
        │ CosmosReminderService │     │ ReminderInstanceService   │
        │    (Production)       │     │    (Development/Test)     │
        └───────────────────────┘     └───────────────────────────┘
                    │                               │
                    ▼                               ▼
        ┌───────────────────────┐     ┌───────────────────────────┐
        │   Azure Cosmos DB     │     │   ConcurrentDictionary    │
        │  (Partition: userId)  │     │     (In-Memory)           │
        └───────────────────────┘     └───────────────────────────┘
```

---

## Domain Models

### ReminderInstance

**Namespace:** `HereAndNowService.Models`
**Location:** `Reminders/HereAndNow.Reminders/Models/ReminderInstance.cs:7`

Core domain model representing a reminder with scheduling and completion tracking.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Id` | `Guid` | Auto-generated | Unique identifier |
| `UserId` | `string?` | Yes (runtime) | Owner's Auth0 user ID (partition key) |
| `Text` | `string` | Yes (`required`) | Reminder content |
| `ScheduledDateAndTime` | `DateTime` | No | When the reminder triggers |
| `IsCompleted` | `bool` | No | Completion status |
| `IsDeleted` | `bool` | No | Soft delete flag |
| `ShouldPlaySound` | `bool` | No | Audio notification setting |
| `ShouldDoVibration` | `bool` | No | Haptic notification setting |

**C# Definition:**

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

### Message

**Namespace:** `HereAndNowService.Models`
**Location:** `Reminders/HereAndNow.Reminders/Models/Message.cs:6`

Simple message response model for the Messages API.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `text` | `string?` | No | Message content |

```csharp
public class Message
{
    public string? text { get; set; }
}
```

---

## DTOs (Data Transfer Objects)

### ReminderInstanceDto

**Namespace:** `HereAndNowService.DTOs`
**Location:** `Web/HereAndNow.Web/DTOs/ReminderInstanceDto.cs:6`

API transfer object with computed `State` property. Note: `UserId` is intentionally excluded from the DTO to prevent exposure in API responses.

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `Guid` | Reminder identifier |
| `Text` | `string` | Reminder content |
| `ScheduledDateAndTime` | `DateTime` | Scheduled time |
| `IsCompleted` | `bool` | Completion status |
| `IsDeleted` | `bool` | Soft delete flag |
| `ShouldPlaySound` | `bool` | Audio notification |
| `ShouldDoVibration` | `bool` | Haptic notification |
| `State` | `ReminderState` | **Computed** - Current state |

**State Computation Logic:**

```csharp
public ReminderState State => this switch
{
    { IsDeleted: true } => ReminderState.Deleted,
    { IsCompleted: true } => ReminderState.Completed,
    _ when DateTime.UtcNow >= ScheduledDateAndTime => ReminderState.Active,
    _ => ReminderState.Scheduled
};
```

---

### ReminderState (Enum)

**Namespace:** `HereAndNowService.DTOs`
**Location:** `Web/HereAndNow.Web/DTOs/ReminderState.cs:6`

Computed state enum representing the reminder lifecycle.

| Value | Description |
|-------|-------------|
| `Scheduled` | Reminder is scheduled for a future time |
| `Active` | Reminder time has arrived/passed, awaiting action |
| `Completed` | Reminder marked as completed |
| `Deleted` | Reminder has been soft-deleted |

**JSON Serialization:** Serialized as string via `JsonStringEnumConverter`

---

## Persistence Layer

### ReminderDocument

**Namespace:** `HereAndNowService.Persistence`
**Location:** `Reminders/HereAndNow.Reminders/Persistence/ReminderDocument.cs:9`
**Visibility:** `internal`

Cosmos DB document representation with string-based ID for Cosmos compatibility.

| Property | Type | Cosmos Role |
|----------|------|-------------|
| `Id` | `string` | Document ID (GUID as string) |
| `UserId` | `string` | **Partition Key** (`/userId`) |
| `Text` | `string` | - |
| `ScheduledDateAndTime` | `DateTime` | - |
| `IsCompleted` | `bool` | - |
| `IsDeleted` | `bool` | Used for soft delete queries |
| `ShouldPlaySound` | `bool` | - |
| `ShouldDoVibration` | `bool` | - |

**Mapping Methods:**

```csharp
// Domain → Document
public static ReminderDocument FromDomain(ReminderInstance domain);

// Document → Domain
public ReminderInstance ToDomain();
```

---

### CosmosDbSettings

**Namespace:** `HereAndNowService.Configuration`
**Location:** `Web/HereAndNow.Web/Configuration/CosmosDbSettings.cs:6`

Configuration class for Cosmos DB connection.

| Property | Type | Environment Variable |
|----------|------|---------------------|
| `Endpoint` | `string` | `COSMOS_ENDPOINT` |
| `PrimaryKey` | `string` | `COSMOS_PRIMARY_KEY` |
| `DatabaseName` | `string` | `COSMOS_DATABASE_NAME` |
| `ContainerName` | `string` | `COSMOS_CONTAINER_NAME` |

---

## Service Layer

### IReminderInstanceService

**Namespace:** `HereAndNowService.Services`
**Location:** `Reminders/HereAndNow.Reminders/Services/IReminderInstanceService.cs:8`

Service interface with user-scoped operations for multi-tenant isolation.

| Method | Return Type | Description |
|--------|-------------|-------------|
| `GetAll(userId)` | `IEnumerable<ReminderInstance>` | Get user's non-deleted reminders |
| `GetById(id, userId)` | `ReminderInstance?` | Get single reminder (null if not found/deleted) |
| `Create(reminder)` | `ReminderInstance` | Create with auto-generated ID |
| `Update(id, reminder)` | `ReminderInstance?` | Update (null if not found) |
| `Delete(id, userId)` | `bool` | Soft delete (returns success) |

---

### ReminderInstanceService (In-Memory)

**Namespace:** `HereAndNowService.Services`
**Location:** `Reminders/HereAndNow.Reminders/Services/ReminderInstanceService.cs:10`
**DI Lifetime:** Singleton (when Cosmos not configured)

Thread-safe in-memory implementation for development/testing.

**Key Features:**
- `ConcurrentDictionary<Guid, ReminderInstance>` storage
- Optimistic concurrency via `TryUpdate`
- Comprehensive structured logging
- User isolation via `userId` filtering

---

### CosmosReminderInstanceService

**Namespace:** `HereAndNowService.Services`
**Location:** `Reminders/HereAndNow.Reminders/Services/CosmosReminderInstanceService.cs:13`
**DI Lifetime:** Scoped (when Cosmos configured)

Production Cosmos DB implementation with resilience patterns.

**Key Features:**

1. **Partition Key Strategy**: `userId` for efficient multi-tenant queries
2. **Soft Delete Queries**: `WHERE c.isDeleted = false`
3. **Error Handling**: Wraps transient errors in `ServiceUnavailableException`
4. **SDK Retry Policy**: 9 retries, 30s max wait for 429 throttling

**Transient Error Detection:**

```csharp
private static bool IsServiceUnavailable(CosmosException ex)
{
    return ex.StatusCode == HttpStatusCode.ServiceUnavailable
        || ex.StatusCode == HttpStatusCode.RequestTimeout
        || ex.StatusCode == HttpStatusCode.GatewayTimeout
        || ex.StatusCode == HttpStatusCode.InternalServerError;
}
```

---

## Mapper

### ReminderInstanceMapper

**Namespace:** `HereAndNowService.Mappers`
**Location:** `Web/HereAndNow.Web/Mappers/ReminderInstanceMapper.cs:9`

Static mapper class for Domain ↔ DTO conversions.

| Method | Description |
|--------|-------------|
| `ToDto(ReminderInstance)` | Domain → DTO |
| `ToDomain(ReminderInstanceDto)` | DTO → Domain |
| `ToDtos(IEnumerable<ReminderInstance>)` | Batch Domain → DTO |

---

## Exceptions

### ServiceUnavailableException

**Namespace:** `HereAndNowService.Exceptions`
**Location:** `Reminders/HereAndNow.Reminders/Exceptions/ServiceUnavailableException.cs:6`

Custom exception for external service failures.

| Property | Type | Description |
|----------|------|-------------|
| `ServiceName` | `string` | Name of unavailable service ("CosmosDB") |

**Usage:** Thrown by `CosmosReminderInstanceService` on transient Cosmos failures, caught by `ErrorHandlerMiddleware` and returned as 503 response.

---

## Cosmos DB Schema

### Container Configuration

| Setting | Value |
|---------|-------|
| **Partition Key** | `/userId` |
| **Indexing** | Default (all properties) |
| **Consistency** | Session (default) |

### Sample Document

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "userId": "auth0|abc123",
  "text": "Take medication",
  "scheduledDateAndTime": "2025-12-17T10:00:00Z",
  "isCompleted": false,
  "isDeleted": false,
  "shouldPlaySound": true,
  "shouldDoVibration": false
}
```

### Query Patterns

**Get all for user:**
```sql
SELECT * FROM c WHERE c.userId = @userId AND c.isDeleted = false
```

**Point read (by ID + partition key):**
```csharp
container.ReadItemAsync<ReminderDocument>(id.ToString(), new PartitionKey(userId))
```

---

## Related Documentation

- [API Contracts](./api-contracts.md) - REST API endpoint documentation
- [Architecture](./architecture.md) - System architecture overview
- [Development Guide](./development-guide.md) - Local setup instructions

---

## Documentation Metadata

| Field | Value |
|-------|-------|
| **Generated** | 2025-12-19 |
| **Scan Level** | Exhaustive |
| **Workflow** | document-project v1.2.0 |
| **Models Documented** | 8 (2 domain, 4 DTO, 1 persistence, 1 exception) |
| **Services Documented** | 4 (2 interfaces, 2 implementations) |
