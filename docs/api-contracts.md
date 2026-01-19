# Here and Now Service - API Contracts

**Date:** 2026-01-18
**Base URL:** `http://localhost:{PORT}` (local) or `https://here-and-now-service.azurewebsites.net` (production)
**Authentication:** Auth0 JWT Bearer Token

## Overview

This document describes all REST API endpoints exposed by the Here and Now Service. The API uses JSON for request/response bodies and requires JWT authentication for protected endpoints.

> **API Evolution (v1.3.0):** The API has evolved to a **Command Pattern** for mutations. The new `CommandsController` provides a single endpoint for all state-changing operations with explicit intent and client-generated IDs.

| Controller | Base Path | Endpoints | Purpose |
|------------|-----------|-----------|---------|
| Messages | `/api/messages` | 3 | Auth0 demo endpoints |
| **Commands** | `/api/v1/commands` | **1** | **All mutations (6 commands)** |
| Tasks | `/api/v1/tasks` | 4 | Task queries + legacy complete |
| Reminders | `/api/v1/reminders` | 4 | Reminder queries + legacy endpoints |

## Authentication

Protected endpoints require a valid Auth0 JWT token in the Authorization header:

```
Authorization: Bearer <your-jwt-token>
```

Unauthenticated requests to protected endpoints return `401 Unauthorized`.

---

## Error Response Format

All errors follow a consistent format:

```json
{
  "error": {
    "code": "TASK_NOT_FOUND",
    "message": "Task with ID abc123 not found"
  }
}
```

### Error Codes

| Code | HTTP Status | Description |
|------|-------------|-------------|
| `TASK_NOT_FOUND` | 404 | Task with given ID not found |
| `TASK_ALREADY_EXISTS` | 409 | Task with given ID already exists |
| `REMINDER_NOT_FOUND` | 404 | Reminder with given ID not found |
| `TASK_REMINDER_ALREADY_EXISTS` | 409 | Reminder with given ID already exists |
| `REMINDER_ALREADY_EXISTS` | 400 | Task already has a reminder attached |
| `REMINDER_ALREADY_DISMISSED` | 400 | Cannot modify a dismissed reminder |
| `INVALID_SCHEDULED_TIME` | 400 | Scheduled time must be in the future |
| `INVALID_STATE_TRANSITION` | 400 | Invalid task state transition (e.g., from Deleted) |
| `VALIDATION_ERROR` | 400 | General validation failure |
| `UNKNOWN_COMMAND` | 400 | Unrecognized command type |
| `UNITY_TRANSACTION_FAILED` | 500 | Atomic Task+Reminder operation failed |

---

## Commands Controller (Primary Mutation API)

**Base Path:** `/api/v1/commands`
**Source:** `Web/HereAndNow.Web/Controllers/CommandsController.cs`

The Commands API provides explicit intent-based operations with client-generated IDs for optimistic UI patterns. **All mutations should use this endpoint.**

### POST /api/v1/commands

Executes a command to modify system state.

**Authentication:** JWT Bearer token required

**Request Format:**
```json
{
  "command": "CommandName",
  "payload": { /* command-specific data */ }
}
```

**Response:** Command-specific (see individual commands below)

---

### Command: CreateTask

Creates a new task with a client-generated ID.

**Request:**
```json
{
  "command": "CreateTask",
  "payload": {
    "taskId": "550e8400-e29b-41d4-a716-446655440000",
    "name": "Buy groceries"
  }
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `taskId` | string | Yes | Valid GUID format |
| `name` | string | Yes | Non-empty, 1-500 characters |

**Response:** `201 Created`
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "name": "Buy groceries",
  "state": "OnDeck",
  "createdAt": "2026-01-18T10:00:00Z",
  "completedAt": null,
  "reminderId": null,
  "lastModifiedAt": "2026-01-18T10:00:00Z"
}
```

**Status Codes:**
| Code | Error Code | Description |
|------|------------|-------------|
| 201 | - | Task created |
| 400 | `VALIDATION_ERROR` | Invalid payload |
| 409 | `TASK_ALREADY_EXISTS` | Task ID already exists |

---

### Command: CreateTaskAndTaskReminder

Atomically creates a task and its associated reminder with client-generated IDs.

**Request:**
```json
{
  "command": "CreateTaskAndTaskReminder",
  "payload": {
    "taskId": "550e8400-e29b-41d4-a716-446655440000",
    "taskReminderId": "660e8400-e29b-41d4-a716-446655440001",
    "name": "Call dentist",
    "scheduledTime": "2026-01-20T09:00:00Z"
  }
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `taskId` | string | Yes | Valid GUID format |
| `taskReminderId` | string | Yes | Valid GUID format |
| `name` | string | Yes | Non-empty, 1-500 characters |
| `scheduledTime` | DateTime | Yes | UTC, must be in the future |

**Response:** `201 Created`
```json
{
  "task": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "name": "Call dentist",
    "state": "OnDeck",
    "createdAt": "2026-01-18T10:00:00Z",
    "completedAt": null,
    "reminderId": "660e8400-e29b-41d4-a716-446655440001",
    "lastModifiedAt": "2026-01-18T10:00:00Z"
  },
  "reminder": {
    "id": "660e8400-e29b-41d4-a716-446655440001",
    "taskId": "550e8400-e29b-41d4-a716-446655440000",
    "taskName": "Call dentist",
    "scheduledTime": "2026-01-20T09:00:00Z",
    "isDismissed": false,
    "dismissedAt": null,
    "createdAt": "2026-01-18T10:00:00Z",
    "lastModifiedAt": "2026-01-18T10:00:00Z"
  }
}
```

**Status Codes:**
| Code | Error Code | Description |
|------|------------|-------------|
| 201 | - | Task and reminder created |
| 400 | `VALIDATION_ERROR` | Invalid payload |
| 400 | `INVALID_SCHEDULED_TIME` | Time must be in the future |
| 409 | `TASK_ALREADY_EXISTS` | Task ID already exists |
| 409 | `TASK_REMINDER_ALREADY_EXISTS` | Reminder ID already exists |

---

### Command: UpdateTaskName

Updates a task's name. If the task has an active reminder, the reminder's denormalized TaskName is also updated atomically (Unity pattern).

**Request:**
```json
{
  "command": "UpdateTaskName",
  "payload": {
    "taskId": "550e8400-e29b-41d4-a716-446655440000",
    "name": "Buy groceries and milk"
  }
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `taskId` | string | Yes | Valid GUID format |
| `name` | string | Yes | Non-empty, 1-500 characters |

**Response:** `200 OK` - Returns updated TaskDto

**Status Codes:**
| Code | Error Code | Description |
|------|------------|-------------|
| 200 | - | Task updated |
| 400 | `VALIDATION_ERROR` | Invalid payload |
| 400 | `INVALID_STATE_TRANSITION` | Cannot modify Deleted tasks |
| 404 | `TASK_NOT_FOUND` | Task not found |

---

### Command: UpdateTaskState

Updates a task's state with comprehensive state machine logic.

**Request:**
```json
{
  "command": "UpdateTaskState",
  "payload": {
    "taskId": "550e8400-e29b-41d4-a716-446655440000",
    "state": "Completed"
  }
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `taskId` | string | Yes | Valid GUID format |
| `state` | string | Yes | `OnDeck`, `InProgress`, `Completed`, `Deleted` (case-sensitive) |

**State Machine Behavior:**
- **Idempotent:** Same state = no-op success
- **Terminal state:** `Deleted` cannot transition to other states
- **CompletedAt:** Auto-set when → Completed, cleared when ← Completed
- **Unity:** Transitioning to `Completed` or `Deleted` with reminder → atomically dismisses reminder

**Response:** `200 OK` - Returns updated TaskDto

**Status Codes:**
| Code | Error Code | Description |
|------|------------|-------------|
| 200 | - | State updated |
| 400 | `VALIDATION_ERROR` | Invalid state value |
| 400 | `INVALID_STATE_TRANSITION` | Cannot transition from Deleted |
| 404 | `TASK_NOT_FOUND` | Task not found |

---

### Command: UpdateTaskReminderScheduledTime

Reschedules (snoozes) a reminder to a new time.

**Request:**
```json
{
  "command": "UpdateTaskReminderScheduledTime",
  "payload": {
    "taskReminderId": "660e8400-e29b-41d4-a716-446655440001",
    "scheduledTime": "2026-01-25T14:00:00Z"
  }
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `taskReminderId` | string | Yes | Valid GUID format |
| `scheduledTime` | DateTime | Yes | UTC, must be in the future |

**Response:** `200 OK` - Returns updated TaskReminderDto

**Status Codes:**
| Code | Error Code | Description |
|------|------------|-------------|
| 200 | - | Reminder rescheduled |
| 400 | `INVALID_SCHEDULED_TIME` | Time must be in the future |
| 400 | `REMINDER_ALREADY_DISMISSED` | Cannot snooze dismissed reminder |
| 404 | `REMINDER_NOT_FOUND` | Reminder not found |

---

### Command: DismissTaskReminder

Dismisses a reminder. This is an **idempotent operation** - dismissing an already-dismissed reminder succeeds.

**Request:**
```json
{
  "command": "DismissTaskReminder",
  "payload": {
    "taskReminderId": "660e8400-e29b-41d4-a716-446655440001"
  }
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `taskReminderId` | string | Yes | Valid GUID format |

**Response:** `204 No Content`

**Status Codes:**
| Code | Error Code | Description |
|------|------------|-------------|
| 204 | - | Reminder dismissed |
| 400 | `VALIDATION_ERROR` | Invalid payload |
| 404 | `REMINDER_NOT_FOUND` | Reminder not found |

---

## Messages Controller

**Base Path:** `/api/messages`
**Source:** `Web/HereAndNow.Web/Controllers/MessagesController.cs`

Demo endpoints for Auth0 authentication testing.

### GET /api/messages/public

Retrieves a public message accessible without authentication.

**Authentication:** None required

**Response:** `200 OK`
```json
{
  "text": "This is a public message."
}
```

---

### GET /api/messages/protected

Retrieves a protected message accessible only to authenticated users.

**Authentication:** JWT Bearer token required

**Response:** `200 OK`
```json
{
  "text": "This is a protected message."
}
```

---

### GET /api/messages/admin

Retrieves an admin message accessible only to authenticated users.

**Authentication:** JWT Bearer token required

**Response:** `200 OK`
```json
{
  "text": "This is an admin message."
}
```

---

## Tasks Controller

**Base Path:** `/api/v1/tasks`
**Source:** `Web/HereAndNow.Web/Controllers/TasksController.cs`

Query endpoints for tasks. **For mutations, use the Commands API.**

### POST /api/v1/tasks [DEPRECATED]

> **Deprecated:** Use `POST /api/v1/commands` with `CreateTask` command instead.

Creates a new task with server-generated ID, optionally with a reminder.

**Authentication:** JWT Bearer token required

**Request Body:**
```json
{
  "name": "Buy groceries",
  "scheduledTime": "2026-01-20T10:00:00Z"
}
```

---

### GET /api/v1/tasks

Retrieves paginated list of tasks with sorting and filtering.

**Authentication:** JWT Bearer token required

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `state` | string | null | Filter by state: `OnDeck`, `InProgress`, `Completed` |
| `orderBy` | string | `createdAt` | Sort field: `createdAt`, `completedAt` |
| `direction` | string | `asc` | Sort direction: `asc`, `desc` |
| `skip` | int | 0 | Number of items to skip |
| `take` | int | 50 | Items per page (max 100) |

**Response:** `200 OK`
```json
{
  "items": [
    {
      "id": "abc123",
      "name": "Buy groceries",
      "state": "OnDeck",
      "createdAt": "2026-01-15T10:00:00Z",
      "completedAt": null,
      "reminderId": "xyz789",
      "lastModifiedAt": "2026-01-15T10:00:00Z"
    }
  ],
  "totalCount": 42,
  "hasMore": true
}
```

---

### GET /api/v1/tasks/{id}

Retrieves a single task by ID.

**Authentication:** JWT Bearer token required

**Response:** `200 OK` - Returns TaskDto

**Status Codes:**
| Code | Error Code | Description |
|------|------------|-------------|
| 200 | - | Success |
| 404 | `TASK_NOT_FOUND` | Task not found |

---

### PUT /api/v1/tasks/{id}/complete

Completes a task with Unity operation. If the task has a reminder, it is atomically dismissed.

> **Note:** Consider using `UpdateTaskState` command with `state: "Completed"` for the same behavior.

**Authentication:** JWT Bearer token required

**Response:** `200 OK` - Returns completed TaskDto

---

## Reminders Controller

**Base Path:** `/api/v1/reminders`
**Source:** `Web/HereAndNow.Web/Controllers/RemindersController.cs`

Reminder management endpoints.

### POST /api/v1/reminders

Creates a reminder for an existing task (server-generated ID).

> **Note:** For client-generated IDs and atomic task+reminder creation, use `CreateTaskAndTaskReminder` command.

**Authentication:** JWT Bearer token required

**Request Body:**
```json
{
  "taskId": "abc123",
  "scheduledTime": "2026-01-20T10:00:00Z"
}
```

**Response:** `201 Created` - Returns TaskReminderDto

---

### GET /api/v1/reminders

Retrieves all non-dismissed reminders sorted by scheduled time.

**Authentication:** JWT Bearer token required

**Response:** `200 OK` - Returns array of TaskReminderDto

---

### GET /api/v1/reminders/{id}

Retrieves a single reminder by ID.

**Authentication:** JWT Bearer token required

**Response:** `200 OK` - Returns TaskReminderDto

---

### PUT /api/v1/reminders/{id}/dismiss

Dismisses a reminder without affecting the associated task.

> **Note:** Consider using `DismissTaskReminder` command for consistency.

**Authentication:** JWT Bearer token required

**Response:** `204 No Content`

---

## Removed Endpoints (v1.3.0)

The following endpoints were removed in favor of the Commands API:

| Endpoint | Replacement |
|----------|-------------|
| `PUT /api/v1/tasks/{id}` | `UpdateTaskName` and/or `UpdateTaskState` commands |
| `DELETE /api/v1/tasks/{id}` | `UpdateTaskState` command with `state: "Deleted"` |
| `PUT /api/v1/reminders/{id}` (snooze) | `UpdateTaskReminderScheduledTime` command |

---

## Unity Operations

The "Unity" pattern ensures Task and Reminder are updated atomically using CosmosDB transactional batches:

| Command | Task Change | Reminder Change |
|---------|-------------|-----------------|
| `UpdateTaskState` (→ Completed) | state → Completed, completedAt set | isDismissed → true |
| `UpdateTaskState` (→ Deleted) | state → Deleted | isDismissed → true |
| `UpdateTaskName` (with reminder) | name updated | taskName synced |
| `PUT /tasks/{id}/complete` | state → Completed | isDismissed → true |

---

## Data Types

### TaskDto

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | Unique identifier (GUID) |
| `name` | string | Task name/title |
| `state` | string | OnDeck, InProgress, Completed, Deleted |
| `createdAt` | DateTime | UTC creation timestamp |
| `completedAt` | DateTime? | UTC completion timestamp (null if not completed) |
| `reminderId` | string? | Associated reminder ID (null if none) |
| `lastModifiedAt` | DateTime | UTC last modification timestamp |

### TaskReminderDto

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | Unique identifier (GUID) |
| `taskId` | string | Associated task ID |
| `taskName` | string | Denormalized task name |
| `scheduledTime` | DateTime | UTC time when reminder triggers |
| `isDismissed` | bool | Whether reminder is dismissed |
| `dismissedAt` | DateTime? | UTC dismissal timestamp |
| `createdAt` | DateTime | UTC creation timestamp |
| `lastModifiedAt` | DateTime | UTC last modification timestamp |

### TaskAndReminderDto

| Field | Type | Description |
|-------|------|-------------|
| `task` | TaskDto | The created task |
| `reminder` | TaskReminderDto | The created reminder |

### PagedTasksDto

| Field | Type | Description |
|-------|------|-------------|
| `items` | TaskDto[] | Tasks in current page |
| `totalCount` | int | Total matching tasks across all pages |
| `hasMore` | bool | Whether more items exist |

---

## CORS Configuration

The API supports CORS for configured origins:

- **Allowed Origins:** Configured via `CLIENT_ORIGIN_URL` environment variable (comma-separated)
- **Allowed Methods:** GET, POST, PUT, DELETE
- **Allowed Headers:** Content-Type, Authorization
- **Preflight Cache:** 86400 seconds (24 hours)

---

## Swagger/OpenAPI

Interactive API documentation is available at:

- **Swagger UI:** `/swagger`
- **OpenAPI Spec:** `/swagger/v1/swagger.json`

---

_Generated using BMAD Method `document-project` workflow_
_Last Updated: 2026-01-18_
