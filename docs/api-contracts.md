# Here and Now Service - API Contracts

**Date:** 2026-03-19
**Base URL:** `http://localhost:{PORT}` (local) or `https://here-and-now-service.azurewebsites.net` (production)
**Authentication:** Auth0 JWT Bearer Token

## Overview

The API uses a **Command Pattern** for all mutations via a single endpoint, with separate query controllers for reads.

- **Commands:** `POST /api/v1/commands` — 13 command types for all mutations
- **Tasks:** `GET /api/v1/tasks` — Paginated task queries
- **Reminders:** `GET /api/v1/reminders` — Active reminder queries
- **Messages:** `GET /api/messages/*` — Demo endpoints (public/protected/admin)

## Error Response Format

All errors use a standardized format:

```json
{
  "error": {
    "code": "ERROR_CODE",
    "message": "Human-readable description"
  }
}
```

| Code | HTTP Status | Description |
|------|------------|-------------|
| TASK_NOT_FOUND | 404 | Task does not exist |
| REMINDER_NOT_FOUND | 404 | Reminder does not exist |
| RECURRING_CONFIG_NOT_FOUND | 404 | Recurring task config does not exist |
| REMINDER_ALREADY_EXISTS | 409 | Task already has a reminder |
| TASK_ALREADY_EXISTS | 409 | Task ID already in use |
| RECURRING_CONFIG_ALREADY_EXISTS | 409 | Config ID already in use |
| REMINDER_ALREADY_DISMISSED | 409 | Reminder was already dismissed |
| INVALID_STATE_TRANSITION | 409 | State transition not allowed |
| INVALID_SCHEDULED_TIME | 400 | Invalid time value |
| INVALID_RECURRENCE_RULE | 400 | RRULE validation failed |
| VALIDATION_ERROR | 400 | Request validation failed |
| UNITY_TRANSACTION_FAILED | 500 | Atomic operation failed |

---

## Commands Controller

### POST /api/v1/commands

**Auth:** Required (JWT Bearer)

All mutations use this single endpoint with a discriminated command payload.

**Request Format:**
```json
{
  "command": "CommandName",
  "payload": { /* command-specific fields */ }
}
```

**Response Format:**
```json
{
  "success": true,
  "message": "Description of what happened",
  "data": { /* optional, command-specific response data */ }
}
```

---

### Task Commands

#### CreateTask

Creates a task with a client-generated ID (enables optimistic UI).

**Payload:**
```json
{
  "taskId": "guid",
  "name": "string"
}
```

**Response data:** `TaskDto`

---

#### CreateTaskAndTaskReminder

Atomically creates a task and its reminder using Unity (transactional batch).

**Payload:**
```json
{
  "taskId": "guid",
  "taskReminderId": "guid",
  "name": "string",
  "scheduledTime": "2026-01-20T09:00:00Z"
}
```

**Response data:** `TaskAndReminderDto` (contains both task and reminder)

---

#### UpdateTaskName

Updates task name. If task has an active reminder, syncs denormalized `taskName` atomically (Unity).

**Payload:**
```json
{
  "taskId": "guid",
  "name": "string"
}
```

**Response data:** `TaskDto`

---

#### UpdateTaskState

Updates task state with full state machine logic. Handles completedAt auto-set/clear and Unity for Completed/Deleted transitions with reminders.

**Payload:**
```json
{
  "taskId": "guid",
  "state": "OnDeck | InProgress | Completed | Deleted"
}
```

**Response data:** `TaskDto`

**State transition rules:**
- Idempotent: transitioning to current state is no-op success
- Deleted is terminal: cannot transition from Deleted
- CompletedAt: auto-set on → Completed, cleared on Completed →
- Unity: Completed/Deleted with reminder → reminder atomically dismissed

---

### Reminder Commands

#### UpdateTaskReminderScheduledTime

Reschedules a reminder. Does NOT validate future time (supports mobile offline sync).

**Payload:**
```json
{
  "taskReminderId": "guid",
  "scheduledTime": "2026-01-20T09:00:00Z"
}
```

**Response data:** `TaskReminderDto`

---

#### DismissTaskReminder

Dismisses a reminder (idempotent — dismissing already-dismissed succeeds).

**Payload:**
```json
{
  "taskReminderId": "guid"
}
```

---

### Recurring Task Config Commands

#### CreateRecurringTaskConfig

Creates a new recurring task configuration with RRULE validation.

**Payload:**
```json
{
  "id": "guid",
  "text": "string",
  "recurrenceRule": "FREQ=DAILY;BYHOUR=7;BYMINUTE=0;BYSECOND=0",
  "startDateAndTime": "2026-01-01T07:00:00Z"
}
```

**Response data:** `RecurringTaskConfigDto`

**Validation:**
- RRULE parsed by Ical.Net
- Rejected frequencies: Secondly, Minutely
- startDateAndTime must be UTC

---

#### UpdateRecurringTaskConfig

Updates an existing recurring task configuration.

**Payload:**
```json
{
  "id": "guid",
  "text": "string",
  "recurrenceRule": "FREQ=WEEKLY;BYDAY=MO,WE,FR",
  "startDateAndTime": "2026-01-01T07:00:00Z"
}
```

**Response data:** `RecurringTaskConfigDto`

---

#### DeleteRecurringTaskConfig

Deletes a recurring task config and all its state overrides atomically (cascade delete using transactional batch). Handles >99 overrides with chunked batch deletion.

**Payload:**
```json
{
  "id": "guid"
}
```

---

### Recurring Task State Commands

All state commands target a specific instance identified by configId + recurrenceDateAndTime.

#### StartRecurringTask

Transitions instance from OnDeck → InProgress.

**Payload:**
```json
{
  "recurringTaskConfigId": "guid",
  "recurrenceDateAndTime": "2026-01-15T07:00:00Z"
}
```

---

#### RevertRecurringTaskToOnDeck

Reverts instance from InProgress → OnDeck (deletes the state override).

**Payload:**
```json
{
  "recurringTaskConfigId": "guid",
  "recurrenceDateAndTime": "2026-01-15T07:00:00Z"
}
```

---

#### CompleteRecurringTask

Completes an instance. Valid from: OnDeck, InProgress, Skipped (if no newer active instance). Idempotent on already-completed.

**Payload:**
```json
{
  "recurringTaskConfigId": "guid",
  "recurrenceDateAndTime": "2026-01-15T07:00:00Z"
}
```

---

#### SkipRecurringTask

Skips an instance. Valid from: OnDeck, InProgress, Completed (if no newer active instance). Idempotent on already-skipped.

**Payload:**
```json
{
  "recurringTaskConfigId": "guid",
  "recurrenceDateAndTime": "2026-01-15T07:00:00Z"
}
```

---

## Tasks Controller

### GET /api/v1/tasks

**Auth:** Required

Returns paginated tasks for the authenticated user. Excludes soft-deleted tasks by default.

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| state | string? | null | Filter: OnDeck, InProgress, Completed |
| orderBy | string | createdAt | Sort field: createdAt, completedAt |
| direction | string | asc | Sort direction: asc, desc |
| skip | int | 0 | Pagination offset |
| take | int | 50 | Page size (max 100) |

**Response:** `PagedTasksDto`
```json
{
  "items": [{ "id": "...", "name": "...", "state": "...", ... }],
  "totalCount": 42,
  "hasMore": true
}
```

### GET /api/v1/tasks/{taskId}

**Auth:** Required

Returns a specific task by ID.

**Response:** `TaskDto`

### PUT /api/v1/tasks/{taskId}/complete

**Auth:** Required (Legacy — use UpdateTaskState command instead)

Completes a task using Unity pattern (atomically dismisses reminder if present).

**Response:** `TaskDto`

---

## Reminders Controller

### GET /api/v1/reminders

**Auth:** Required

Returns all non-dismissed reminders for the authenticated user, sorted by scheduledTime ascending.

**Response:** `TaskReminderDto[]`

### GET /api/v1/reminders/{reminderId}

**Auth:** Required

Returns a specific reminder by ID.

**Response:** `TaskReminderDto`

### POST /api/v1/tasks/{taskId}/reminder

**Auth:** Required (Legacy — use CreateTaskAndTaskReminder command instead)

Creates a reminder for an existing task.

**Request:**
```json
{
  "scheduledTime": "2026-01-20T09:00:00Z"
}
```

**Response:** `TaskReminderDto`

### PUT /api/v1/reminders/{reminderId}/dismiss

**Auth:** Required (Legacy — use DismissTaskReminder command instead)

Dismisses a reminder (idempotent).

---

## Messages Controller (Demo)

### GET /api/messages/public

**Auth:** None

Returns a public message.

### GET /api/messages/protected

**Auth:** Required

Returns a protected message.

### GET /api/messages/admin

**Auth:** Required

Returns an admin message.

---

## DTO Reference

### TaskDto
```json
{
  "id": "string",
  "name": "string",
  "state": "OnDeck | InProgress | Completed | Deleted",
  "createdAt": "datetime",
  "completedAt": "datetime?",
  "reminderId": "string?",
  "lastModifiedAt": "datetime"
}
```

### TaskReminderDto
```json
{
  "id": "string",
  "taskId": "string",
  "taskName": "string",
  "scheduledTime": "datetime",
  "isDismissed": "boolean",
  "dismissedAt": "datetime?",
  "createdAt": "datetime",
  "lastModifiedAt": "datetime"
}
```

### TaskAndReminderDto
```json
{
  "task": { /* TaskDto */ },
  "reminder": { /* TaskReminderDto */ }
}
```

### RecurringTaskConfigDto
```json
{
  "id": "string",
  "text": "string",
  "rrule": "string",
  "startDateAndTime": "datetime",
  "createdAt": "datetime"
}
```

### PagedTasksDto
```json
{
  "items": [ /* TaskDto[] */ ],
  "totalCount": "int",
  "hasMore": "boolean"
}
```

### ErrorResponseDto
```json
{
  "error": {
    "code": "string",
    "message": "string"
  }
}
```

---

_Generated by BMAD document-project workflow | Exhaustive Scan | 2026-03-19_
