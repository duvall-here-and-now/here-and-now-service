# Here and Now Service - API Contracts

**Date:** 2026-05-01
**Base URL:** `http://localhost:{PORT}` (local) or `https://here-and-now-service.azurewebsites.net` (production)
**Authentication:** Auth0 JWT Bearer Token

---

## Authentication

All Task, Reminder, and Recurring Task endpoints require a JWT Bearer token from Auth0:

```
Authorization: Bearer <token>
```

Swagger UI at `/swagger` supports interactive Bearer token entry.

---

## Standard Error Envelope

All error responses use this shape:

```json
{
  "error": {
    "code": "TASK_NOT_FOUND",
    "message": "Task with ID abc123 not found"
  }
}
```

### Error Codes

| Code | HTTP | Description |
|------|------|-------------|
| `VALIDATION_ERROR` | 400 | Missing or invalid field |
| `INVALID_SCHEDULED_TIME` | 400 | ScheduledTime validation failure |
| `INVALID_STATE_TRANSITION` | 400 | Illegal state change attempted |
| `INVALID_RECURRENCE_RULE` | 400 | Bad RRULE string |
| `UNKNOWN_COMMAND` | 400 | Unrecognized command name |
| `TASK_NOT_FOUND` | 404 | Task ID does not exist |
| `REMINDER_NOT_FOUND` | 404 | Reminder ID does not exist |
| `RECURRING_TASK_CONFIG_NOT_FOUND` | 404 | Config ID does not exist |
| `TASK_ALREADY_EXISTS` | 409 | Client-supplied taskId already exists |
| `TASK_REMINDER_ALREADY_EXISTS` | 409 | Client-supplied taskReminderId already exists |
| `REMINDER_ALREADY_EXISTS` | 409 | Task already has a reminder |
| `REMINDER_ALREADY_DISMISSED` | 400 | Cannot snooze a dismissed reminder |
| `RECURRING_TASK_CONFIG_ALREADY_EXISTS` | 409 | Config ID already exists |
| `UNITY_TRANSACTION_FAILED` | 500 | Cosmos transactional batch failed |

---

## Commands Endpoint

### POST /api/v1/commands

All mutations flow through this single endpoint. The `command` field is the discriminator; `payload` is deserialized to the matching command type.

**Request shape:**
```json
{
  "command": "<CommandName>",
  "payload": { ... }
}
```

---

### CreateTask

Creates a task with a client-generated ID.

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

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `taskId` | string (GUID) | Yes | Client-generated; normalized to lowercase |
| `name` | string | Yes | Non-empty |

**Response:** `201 Created` → `TaskDto`

**Errors:** `400 VALIDATION_ERROR`, `409 TASK_ALREADY_EXISTS`

---

### CreateTaskAndTaskReminder

Atomically creates a task and a reminder in a single Cosmos transactional batch.

**Request:**
```json
{
  "command": "CreateTaskAndTaskReminder",
  "payload": {
    "taskId": "550e8400-e29b-41d4-a716-446655440000",
    "taskReminderId": "660e8400-e29b-41d4-a716-446655440001",
    "name": "Call dentist",
    "scheduledTime": "2026-06-15T09:00:00Z"
  }
}
```

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `taskId` | string (GUID) | Yes | Client-generated |
| `taskReminderId` | string (GUID) | Yes | Client-generated |
| `name` | string | Yes | Non-empty |
| `scheduledTime` | DateTime (UTC) | Yes | Past times accepted (mobile sync) |

**Response:** `201 Created` → `TaskAndReminderDto`

**Errors:** `400 VALIDATION_ERROR`, `409 TASK_ALREADY_EXISTS`, `409 TASK_REMINDER_ALREADY_EXISTS`

---

### UpdateTaskName

Renames a task. If the task has a reminder, the reminder's denormalized `taskName` is updated atomically (Unity).

**Request:**
```json
{
  "command": "UpdateTaskName",
  "payload": {
    "taskId": "550e8400-e29b-41d4-a716-446655440000",
    "name": "Call dentist (updated)"
  }
}
```

**Response:** `200 OK` → `TaskDto`

**Errors:** `400 VALIDATION_ERROR`, `400 INVALID_STATE_TRANSITION`, `404 TASK_NOT_FOUND`

---

### UpdateTaskState

Transitions a task's state. Idempotent (same state → no-op). Unity dismisses the reminder when transitioning to `Completed` or `Deleted`.

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

| `state` | Valid values |
|---------|-------------|
| Regular tasks | `OnDeck`, `InProgress`, `Completed`, `Deleted` (case-sensitive) |

**Transitions:**
- `OnDeck` ↔ `InProgress` ↔ `Completed`
- `OnDeck` / `InProgress` → `Deleted` (terminal)
- `Deleted` → any (rejected with `INVALID_STATE_TRANSITION`)

**Response:** `200 OK` → `TaskDto`

**Errors:** `400 VALIDATION_ERROR`, `400 INVALID_STATE_TRANSITION`, `404 TASK_NOT_FOUND`

---

### UpdateTaskReminderScheduledTime

Reschedules (snoozes) a reminder. Past times accepted for mobile offline sync.

**Request:**
```json
{
  "command": "UpdateTaskReminderScheduledTime",
  "payload": {
    "taskReminderId": "660e8400-e29b-41d4-a716-446655440001",
    "scheduledTime": "2026-06-20T14:00:00Z"
  }
}
```

**Response:** `200 OK` → `TaskReminderDto`

**Errors:** `400 VALIDATION_ERROR`, `400 REMINDER_ALREADY_DISMISSED`, `400 INVALID_SCHEDULED_TIME`, `404 REMINDER_NOT_FOUND`

---

### DismissTaskReminder

Dismisses a reminder without affecting its task. Idempotent.

**Request:**
```json
{
  "command": "DismissTaskReminder",
  "payload": {
    "taskReminderId": "660e8400-e29b-41d4-a716-446655440001"
  }
}
```

**Response:** `204 No Content`

**Errors:** `400 VALIDATION_ERROR`, `404 REMINDER_NOT_FOUND`

---

### CreateRecurringTaskConfig

Creates a recurring task configuration with a client-generated ID.

**Request:**
```json
{
  "command": "CreateRecurringTaskConfig",
  "payload": {
    "id": "770e8400-e29b-41d4-a716-446655440010",
    "text": "Morning standup",
    "recurrenceRule": "FREQ=DAILY;BYHOUR=9;BYMINUTE=0;BYSECOND=0",
    "startDateAndTime": "2026-06-01T09:00:00Z"
  }
}
```

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `id` | string (GUID) | Yes | Client-generated |
| `text` | string | Yes | Display name |
| `recurrenceRule` | string | Yes | RRULE without `RRULE:` prefix. Valid: Hourly, Daily, Weekly, Monthly, Yearly. Rejected: Secondly, Minutely |
| `startDateAndTime` | DateTime (UTC) | Yes | Must be UTC (`DateTimeKind.Utc`) |

**Response:** `201 Created` → `RecurringTaskConfigDto`

**Errors:** `400 VALIDATION_ERROR`, `400 INVALID_RECURRENCE_RULE`, `409 RECURRING_TASK_CONFIG_ALREADY_EXISTS`

---

### UpdateRecurringTaskConfig

Updates an existing recurring task configuration.

**Request:**
```json
{
  "command": "UpdateRecurringTaskConfig",
  "payload": {
    "id": "770e8400-e29b-41d4-a716-446655440010",
    "text": "Morning standup (updated)",
    "recurrenceRule": "FREQ=WEEKLY;BYDAY=MO,WE,FR;BYHOUR=9;BYMINUTE=0;BYSECOND=0",
    "startDateAndTime": "2026-06-01T09:00:00Z"
  }
}
```

**Response:** `200 OK` → `RecurringTaskConfigDto`

**Errors:** `400 VALIDATION_ERROR`, `400 INVALID_RECURRENCE_RULE`, `404 RECURRING_TASK_CONFIG_NOT_FOUND`

---

### DeleteRecurringTaskConfig

Deletes a recurring task configuration and all its state overrides. Deletion may be chunked (Cosmos transactional batch limit: 100 ops).

**Request:**
```json
{
  "command": "DeleteRecurringTaskConfig",
  "payload": {
    "id": "770e8400-e29b-41d4-a716-446655440010"
  }
}
```

**Response:** `204 No Content`

**Errors:** `400 VALIDATION_ERROR`, `404 RECURRING_TASK_CONFIG_NOT_FOUND`

---

### StartRecurringTask

Transitions a recurring task instance from `OnDeck` → `InProgress`.

**Request:**
```json
{
  "command": "StartRecurringTask",
  "payload": {
    "recurringTaskConfigId": "770e8400-e29b-41d4-a716-446655440010",
    "recurrenceDateAndTime": "2026-06-01T09:00:00Z"
  }
}
```

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `recurringTaskConfigId` | string (GUID) | Yes | Config ID |
| `recurrenceDateAndTime` | DateTime (UTC) | Yes | Identifies specific instance |

**Response:** `200 OK`

**Errors:** `400 VALIDATION_ERROR`, `400 INVALID_STATE_TRANSITION`, `404 RECURRING_TASK_CONFIG_NOT_FOUND`

**Rejected from:** `InProgress`, `Scheduled`, `Completed`, `Skipped`

---

### RevertRecurringTaskToOnDeck

Reverts an `InProgress` instance back to `OnDeck` (deletes its state override).

**Request:**
```json
{
  "command": "RevertRecurringTaskToOnDeck",
  "payload": {
    "recurringTaskConfigId": "770e8400-e29b-41d4-a716-446655440010",
    "recurrenceDateAndTime": "2026-06-01T09:00:00Z"
  }
}
```

**Response:** `200 OK`

**Errors:** `400 VALIDATION_ERROR`, `400 INVALID_STATE_TRANSITION`, `404 RECURRING_TASK_CONFIG_NOT_FOUND`

**Only valid from:** `InProgress`

---

### CompleteRecurringTask

Marks a recurring task instance as `Completed`. Idempotent from `Completed`.

**Request:**
```json
{
  "command": "CompleteRecurringTask",
  "payload": {
    "recurringTaskConfigId": "770e8400-e29b-41d4-a716-446655440010",
    "recurrenceDateAndTime": "2026-06-01T09:00:00Z"
  }
}
```

**Response:** `200 OK`

**Errors:** `400 INVALID_STATE_TRANSITION` (from `Scheduled`), `404 RECURRING_TASK_CONFIG_NOT_FOUND`

---

### SkipRecurringTask

Marks a recurring task instance as `Skipped`. Idempotent from `Skipped`.

**Request:**
```json
{
  "command": "SkipRecurringTask",
  "payload": {
    "recurringTaskConfigId": "770e8400-e29b-41d4-a716-446655440010",
    "recurrenceDateAndTime": "2026-06-01T09:00:00Z"
  }
}
```

**Response:** `200 OK`

**Errors:** `400 INVALID_STATE_TRANSITION` (from `Scheduled`), `404 RECURRING_TASK_CONFIG_NOT_FOUND`

---

## Tasks Endpoints

### GET /api/v1/tasks

Gets paginated tasks for the authenticated user.

**Query Parameters:**

| Parameter | Type | Default | Notes |
|-----------|------|---------|-------|
| `state` | string | null (all) | `OnDeck`, `InProgress`, `Completed`, `Deleted` |
| `orderBy` | string | `createdAt` | `createdAt` or `completedAt` |
| `direction` | string | `asc` | `asc` or `desc` |
| `skip` | int | `0` | Pagination offset |
| `take` | int | `50` | Max 100 |

**Response:** `200 OK` → `PagedTasksDto`

```json
{
  "items": [ TaskDto ],
  "totalCount": 42,
  "hasMore": true
}
```

---

### GET /api/v1/tasks/{id}

Gets a single task by ID.

**Response:** `200 OK` → `TaskDto`

**Errors:** `404 TASK_NOT_FOUND`

---

### PUT /api/v1/tasks/{id}/complete

Legacy Unity endpoint. Completes a task and atomically dismisses its reminder. **Prefer `UpdateTaskState` command for new code.**

**Response:** `200 OK` → `TaskDto`

**Errors:** `404 TASK_NOT_FOUND`, `500 UNITY_TRANSACTION_FAILED`

---

### POST /api/v1/tasks _(deprecated)_

Legacy task creation endpoint. **Use `CreateTask` or `CreateTaskAndTaskReminder` commands instead.**

**Request:** `CreateTaskDto` — `{ name, scheduledTime? }`

**Response:** `201 Created` → `TaskDto`

---

## Reminders Endpoints

### GET /api/v1/reminders

Gets all non-dismissed reminders for the authenticated user, sorted by scheduled time.

**Response:** `200 OK` → `TaskReminderDto[]`

---

### GET /api/v1/reminders/{id}

Gets a single reminder by ID.

**Response:** `200 OK` → `TaskReminderDto`

**Errors:** `404 REMINDER_NOT_FOUND`

---

### PUT /api/v1/reminders/{id}/dismiss

Dismisses a reminder. Idempotent — dismissing an already-dismissed reminder returns `400`.

**Response:** `204 No Content`

**Errors:** `400 REMINDER_ALREADY_DISMISSED`, `404 REMINDER_NOT_FOUND`

---

### POST /api/v1/reminders _(legacy)_

Creates a reminder for an existing task.

**Request:** `CreateReminderDto` — `{ taskId, scheduledTime }`

**Response:** `201 Created` → `TaskReminderDto`

**Errors:** `404 TASK_NOT_FOUND`, `400 REMINDER_ALREADY_EXISTS`

---

## Recurring Task Configs Endpoints

### GET /api/v1/recurring-task-configs

Gets all recurring task configurations for the authenticated user.

**Response:** `200 OK` → `RecurringTaskConfigDto[]`

---

### GET /api/v1/recurring-task-configs/{id}

Gets a specific configuration by ID.

**Response:** `200 OK` → `RecurringTaskConfigDto`

**Errors:** `404 RECURRING_TASK_CONFIG_NOT_FOUND`

---

## Recurring Tasks Endpoints

### GET /api/v1/recurring-tasks

Gets computed recurring task instances for a date range. Instances are computed in-memory from configs and state overrides — **not persisted**.

**Query Parameters:**

| Parameter | Type | Required | Notes |
|-----------|------|----------|-------|
| `from` | DateTime (ISO 8601 UTC) | Yes | Start of range (inclusive) |
| `to` | DateTime (ISO 8601 UTC) | Yes | End of range (inclusive) |

**Constraints:**
- `from` must be before `to`
- Range must not exceed 365 days

**Response:** `200 OK` → `RecurringTaskDto[]`

**Errors:** `400 VALIDATION_ERROR` (missing params, invalid range, >365 days)

---

## Messages Endpoints (Auth0 Demo)

### GET /api/messages/public

Public endpoint — no authentication required.

**Response:** `200 OK` → `{ text: string }`

---

### GET /api/messages/protected

Requires JWT Bearer.

**Response:** `200 OK` → `{ text: string }`

---

### GET /api/messages/admin

Requires JWT Bearer.

**Response:** `200 OK` → `{ text: string }`

---

## DTO Reference

### TaskDto

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "name": "Buy groceries",
  "state": "OnDeck",
  "createdAt": "2026-05-01T10:00:00Z",
  "completedAt": null,
  "reminderId": "660e8400-e29b-41d4-a716-446655440001",
  "lastModifiedAt": "2026-05-01T10:00:00Z"
}
```

### TaskReminderDto

```json
{
  "id": "660e8400-e29b-41d4-a716-446655440001",
  "taskId": "550e8400-e29b-41d4-a716-446655440000",
  "taskName": "Buy groceries",
  "scheduledTime": "2026-05-10T09:00:00Z",
  "isDismissed": false,
  "dismissedAt": null,
  "createdAt": "2026-05-01T10:00:00Z",
  "lastModifiedAt": "2026-05-01T10:00:00Z"
}
```

### TaskAndReminderDto

```json
{
  "task": { TaskDto },
  "reminder": { TaskReminderDto }
}
```

### RecurringTaskConfigDto

```json
{
  "id": "770e8400-e29b-41d4-a716-446655440010",
  "text": "Morning standup",
  "recurrenceRule": "FREQ=DAILY;BYHOUR=9;BYMINUTE=0;BYSECOND=0",
  "startDateAndTime": "2026-06-01T09:00:00Z",
  "createdAt": "2026-05-01T10:00:00Z"
}
```

### RecurringTaskDto

```json
{
  "id": "770e8400-e29b-41d4-a716-446655440010_2026-06-01T09:00:00Z",
  "configId": "770e8400-e29b-41d4-a716-446655440010",
  "text": "Morning standup",
  "recurrenceDateAndTime": "2026-06-01T09:00:00Z",
  "state": "OnDeck",
  "recurrenceRule": "FREQ=DAILY;BYHOUR=9;BYMINUTE=0;BYSECOND=0"
}
```

### PagedTasksDto

```json
{
  "items": [ TaskDto ],
  "totalCount": 42,
  "hasMore": true
}
```
