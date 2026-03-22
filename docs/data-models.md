# Here and Now Service - Data Models

**Date:** 2026-03-19

## Overview

All domain models are stored in a single Azure Cosmos DB container (`Tasks`) using a type discriminator pattern. The partition key is `/userId`, ensuring all user data is co-located for efficient queries and transactional batches.

## Cosmos DB Document Types

### TaskDocument

Represents a user's task.

| Property | Type | JSON Name | Description |
|----------|------|-----------|-------------|
| Id | string | id | Unique GUID identifier |
| Type | string | type | Always `"Task"` |
| UserId | string | userId | Owner's user ID (partition key) |
| Name | string | name | Task title/description |
| State | string | state | Current state (OnDeck, InProgress, Completed, Deleted) |
| CreatedAt | DateTime | createdAt | UTC creation timestamp |
| CompletedAt | DateTime? | completedAt | UTC completion timestamp (null if not completed) |
| ReminderId | string? | reminderId | Linked reminder ID (null if none) |
| LastModifiedAt | DateTime | lastModifiedAt | UTC last modification timestamp |

### TaskReminderDocument

Represents a time-based reminder attached to a task. Stores a denormalized `TaskName` to avoid joins.

| Property | Type | JSON Name | Description |
|----------|------|-----------|-------------|
| Id | string | id | Unique GUID identifier |
| Type | string | type | Always `"TaskReminder"` |
| UserId | string | userId | Owner's user ID (partition key) |
| TaskId | string | taskId | Associated task ID |
| TaskName | string | taskName | Denormalized task name (synced via Unity) |
| ScheduledTime | DateTime | scheduledTime | UTC trigger time |
| IsDismissed | bool | isDismissed | Whether dismissed |
| DismissedAt | DateTime? | dismissedAt | UTC dismissal timestamp |
| CreatedAt | DateTime | createdAt | UTC creation timestamp |
| LastModifiedAt | DateTime | lastModifiedAt | UTC last modification timestamp |

### RecurringTaskConfigDocument

Defines a recurrence pattern for a repeating task using RFC 5545 RRULE syntax.

| Property | Type | JSON Name | Description |
|----------|------|-----------|-------------|
| Id | string | id | Unique GUID identifier (client-generated) |
| Type | string | type | Always `"RecurringTaskConfig"` |
| UserId | string | userId | Owner's user ID (partition key) |
| Text | string | text | Display text/name of the recurring task |
| Rrule | string | rrule | RRULE string without prefix (e.g., `"FREQ=DAILY;BYHOUR=7;BYMINUTE=0;BYSECOND=0"`) |
| StartDateAndTime | DateTime | startDateAndTime | UTC start date/time for the recurrence pattern |
| CreatedAt | DateTime | createdAt | UTC creation timestamp |

### RecurringTaskStateOverrideDocument

Stores explicit state changes for specific recurring task instances. Only instances that deviate from the default computed state need an override.

| Property | Type | JSON Name | Description |
|----------|------|-----------|-------------|
| Id | string | id | Composite: `{configId}_{yyyy-MM-ddTHH:mm:ssZ}` |
| Type | string | type | Always `"RecurringTaskStateOverride"` |
| UserId | string | userId | Owner's user ID (partition key) |
| ConfigId | string | configId | Parent RecurringTaskConfig.Id |
| RecurrenceDateAndTime | DateTime | recurrenceDateAndTime | UTC date/time of the specific occurrence |
| State | string | state | Overridden state (InProgress, Completed, Skipped) |
| UpdatedAt | DateTime | updatedAt | UTC last update timestamp |

**Static method:** `GenerateId(configId, recurrenceDateAndTime)` — produces the composite ID, enforces UTC.

## Computed Model (Not Persisted)

### RecurringTaskInstance

Generated in-memory by `RecurringTaskService.ComputeInstances()`. Represents one occurrence of a recurring task with its resolved state.

| Property | Type | Description |
|----------|------|-------------|
| RecurringTaskConfigId | string | Parent config ID |
| Text | string | Task text (from config, read-only) |
| RecurrenceDateAndTime | DateTime | UTC occurrence date/time |
| State | string | Computed state: Scheduled, OnDeck, InProgress, Completed, Skipped |
| RecurrenceRule | string | RRULE from parent config |
| Id | string | Computed composite: `{configId}_{datetime}` |

## State Constants (TaskState)

```csharp
public static class TaskState
{
    public const string OnDeck = "OnDeck";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Deleted = "Deleted";
    public const string Scheduled = "Scheduled";    // Recurring only
    public const string Skipped = "Skipped";         // Recurring only
}
```

**Regular task states:** OnDeck, InProgress, Completed, Deleted
**Recurring task states:** Scheduled, OnDeck, InProgress, Completed, Skipped

## Pagination Model

### PagedResult\<T\>

Generic wrapper for paginated query results.

| Property | Type | Description |
|----------|------|-------------|
| Items | IReadOnlyList\<T\> | Items in current page |
| TotalCount | int | Total matching items across all pages |
| HasMore | bool | Whether more items exist beyond current page |

## Domain Exceptions (12)

| Exception | HTTP Status | Description |
|-----------|------------|-------------|
| TaskNotFoundException | 404 | Task not found. Properties: `TaskId` |
| TaskAlreadyExistsException | 409 | Task ID already exists. Properties: `TaskId` |
| ReminderNotFoundException | 404 | Reminder not found. Properties: `ReminderId` |
| ReminderAlreadyExistsException | 409 | Task already has a reminder. Properties: `TaskId` |
| ReminderAlreadyDismissedException | 409 | Reminder already dismissed. Properties: `ReminderId` |
| TaskReminderAlreadyExistsException | 409 | Reminder ID already exists. Properties: `ReminderId` |
| InvalidScheduledTimeException | 400 | Invalid scheduled time value |
| InvalidStateTransitionException | 409 | Invalid state transition. Properties: `TaskId`, `CurrentState`, `AttemptedAction` |
| UnityTransactionFailedException | 500 | Transactional batch failed. Properties: `TaskId` |
| RecurringTaskConfigNotFoundException | 404 | Config not found. Properties: `ConfigId` |
| RecurringTaskConfigAlreadyExistsException | 409 | Config ID already exists. Properties: `ConfigId` |
| InvalidRecurrenceRuleException | 400 | RRULE validation failed. Properties: `RecurrenceRule` |

## Service Layer

### ITaskService / TaskService

Core task business logic with Unity pattern support.

| Method | Description | Unity? |
|--------|------------|--------|
| CreateTaskAsync | Create with server-generated ID | No |
| CreateTaskWithIdAsync | Create with client-generated ID | No |
| CreateTaskWithOptionalReminderAsync | Create task, optionally create+link reminder | Partial |
| CreateTaskWithReminderAsync | Atomic task+reminder creation | Yes |
| GetTasksAsync | Get tasks with optional state filter | No |
| GetTasksPagedAsync | Paginated, sorted, filtered query | No |
| GetTaskByIdAsync | Single task lookup | No |
| UpdateTaskAsync | Update name/state (legacy) | Conditional |
| UpdateTaskNameAsync | Update name with reminder sync | Conditional |
| UpdateStateAsync | State machine with Unity for Complete/Delete | Conditional |
| CompleteTaskWithUnityAsync | Complete + dismiss reminder | Yes |
| DeleteTaskWithUnityAsync | Soft-delete + dismiss reminder | Yes |

### ITaskReminderService / TaskReminderService

Reminder operations with idempotent dismiss.

| Method | Description |
|--------|------------|
| CreateReminderAsync | Create reminder + atomic task link |
| GetRemindersAsync | Non-dismissed reminders, sorted by time |
| GetReminderByIdAsync | Single reminder lookup |
| SnoozeAsync | Reschedule (no future-time validation for mobile sync) |
| DismissAsync | Dismiss (idempotent) |

### IRecurringTaskService / RecurringTaskService

RRULE-based computation and state management.

| Method | Description | I/O |
|--------|------------|-----|
| GetComputedInstancesAsync | Fetch configs + overrides, compute instances | 2 DB queries |
| ComputeInstances | Pure function — RRULE expansion + state resolution | None (in-memory) |
| CreateConfigAsync | Create config with RRULE validation | 1 DB write |
| UpdateConfigAsync | Update config with RRULE re-validation | 1 DB read + 1 write |
| DeleteConfigAsync | Cascade delete config + all overrides | Batch delete |
| StartRecurringTaskAsync | OnDeck → InProgress | Compute + upsert |
| RevertRecurringTaskToOnDeckAsync | InProgress → OnDeck (delete override) | Compute + delete |
| CompleteRecurringTaskAsync | → Completed (idempotent) | Compute + upsert |
| SkipRecurringTaskAsync | → Skipped (idempotent) | Compute + upsert |

## Repository Layer

### ITaskRepository / TaskRepository

Cosmos DB operations with Unity transactional batches.

| Method | Pattern |
|--------|---------|
| CreateAsync | Point write |
| GetByIdAsync | Point read (1 RU) |
| ExistsAsync | Point read for existence check |
| GetByUserIdAsync | Partition query with type + state filter |
| GetByUserIdPagedAsync | Partition query with ORDER BY + OFFSET/LIMIT |
| UpdateAsync | Point replace |
| UpdateReminderIdAsync | Patch operation (partial update) |
| CompleteWithUnityAsync | Transactional batch (task + reminder) |
| DeleteWithUnityAsync | Transactional batch (task + reminder) |
| UpdateWithReminderSyncAsync | Transactional batch (task + reminder) |
| CreateTaskWithReminderBatchAsync | Transactional batch (create task + create reminder) |

### ITaskReminderRepository / TaskReminderRepository

| Method | Pattern |
|--------|---------|
| CreateAsync | Point write |
| GetByIdAsync | Point read with type verification |
| GetByUserIdAsync | Partition query (non-dismissed, sorted) |
| GetByTaskIdAsync | Partition query by taskId |
| UpdateAsync | Point replace |
| CreateWithTaskLinkAsync | Transactional batch (create reminder + patch task) |
| ExistsAsync | Point read with type verification |

### IRecurringTaskRepository / RecurringTaskRepository

| Method | Pattern |
|--------|---------|
| CreateConfigAsync | Point write |
| GetConfigByIdAsync | Point read with type verification |
| GetAllConfigsAsync | Partition query (type=RecurringTaskConfig) |
| UpdateConfigAsync | Point replace |
| DeleteConfigWithOverridesAsync | Batch delete (chunked if >99 overrides) |
| UpsertStateOverrideAsync | Upsert (create or replace) |
| DeleteStateOverrideAsync | Point delete (idempotent) |
| GetStateOverridesForDateRangeAsync | Partition query with date range filter |

---

_Generated by BMAD document-project workflow | Exhaustive Scan | 2026-03-19_
