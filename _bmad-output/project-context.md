---
project_name: 'here-and-now-service'
user_name: 'Mike DuVall'
date: '2026-03-29'
sections_completed: ['technology_stack', 'architecture_rules', 'runtime_rules', 'testing_rules', 'critical_rules']
status: 'complete'
rule_count: 39
optimized_for_llm: true
---

# Project Context for AI Agents

_This file captures the non-obvious project rules an implementation agent is likely to miss. Favor accuracy and durability over exhaustive detail._

---

## Technology Stack

- .NET 8 / C# 12
- ASP.NET Core Web API with Auth0 JWT bearer auth
- Azure Cosmos DB, single container with `/userId` partition key
- Ical.Net for RRULE parsing and validation
- Swagger with XML doc generation
- xUnit + Moq + FluentAssertions for tests

## Core Architecture Rules

- **New mutations use the command endpoint**: add new write operations to `POST /api/v1/commands`, not to new REST mutation endpoints.
- **Legacy endpoints still exist**: preserve existing non-command endpoints unless the task explicitly removes or migrates them.
- **Command dispatch lives in `CommandsController`**: it switches on the `command` string, deserializes the `payload`, and returns `IActionResult` responses directly.
- **Client generates IDs**: task, reminder, and recurring config IDs come from the client to support optimistic UI.
- **Normalize GUID IDs to lowercase** before persisting or comparing command IDs.
- **`userId` is always the first service parameter** and comes from `ClaimTypes.NameIdentifier`.
- **Use `TaskState` string constants**, not enums or duplicated string literals.
- **All timestamps are UTC**: use `DateTime.UtcNow`, store and transmit ISO 8601 UTC values only.
- **File-scoped namespaces only**.
- **Public API-facing properties use `[JsonPropertyName]`** for explicit JSON field names.
- **Public APIs should have XML docs** because Swagger includes XML comments when available.
- **Use structured logging** with user and resource identifiers in log messages.

## Data and Persistence Rules

- **All task-related documents share one Cosmos container**: `Task`, `TaskReminder`, `RecurringTaskConfig`, and `RecurringTaskStateOverride` are stored together.
- **Always scope Cosmos operations to the user partition**: every read, query, patch, replace, delete, and batch uses partition key `userId`.
- **Do not query across partitions**.
- **Use the document `type` discriminator** on stored documents.
- **Task/reminder multi-document updates use the Unity pattern**: if a task and reminder must change together, use a transactional batch.
- **Reminder `taskName` is denormalized**: changing a task name must also update the linked reminder atomically.
- **Recurring task instances are computed, not persisted**: only configs and state overrides are stored.
- **Computed recurring instances follow the two-query pattern**: fetch configs, fetch overrides, then compute in memory.
- **Override ID format is `{configId}_{yyyy-MM-ddTHH:mm:ssZ}`**.
- **Store RRULE values without the `RRULE:` prefix**.
- **Reject `Secondly` and `Minutely` RRULE frequencies**; accept Hourly, Daily, Weekly, Monthly, and Yearly.
- **One active recurring instance at a time**: only the most recent past instance may be `OnDeck` or `InProgress` for a config.
- **Recurring config deletes may require chunking**: Cosmos transactional batch limit is 100 operations.

## Runtime and API Rules

- **Required env vars**: `PORT`, `CLIENT_ORIGIN_URL`, `AUTH0_DOMAIN`, `AUTH0_AUDIENCE`.
- **Cosmos wiring is conditional**: task/reminder/recurring services and repositories are only registered when `COSMOS_CONNECTION_STRING` is set.
- **Without Cosmos configured, only non-Cosmos functionality is expected to work**.
- **Database and container are auto-created on startup** when Cosmos is configured.
- **`CLIENT_ORIGIN_URL` may contain multiple comma-separated origins**; CORS is built from that list.
- **Validation errors use the standard error envelope**.
- **`ScheduledTime` model validation failures map to `INVALID_SCHEDULED_TIME`**; other model validation failures map to `VALIDATION_ERROR`.
- **Controllers sometimes translate validation and domain errors directly**; middleware still handles cross-cutting and unhandled exceptions.
- **Standard error shape**:

  ```json
  { "error": { "code": "TASK_NOT_FOUND", "message": "Task with ID abc123 not found" } }
  ```

## State Rules

- **Regular task states**: `OnDeck`, `InProgress`, `Completed`, `Deleted`.
- **Recurring instance states**: `Scheduled`, `OnDeck`, `InProgress`, `Completed`, `Skipped`.
- **Regular transitions**: `OnDeck -> InProgress -> Completed`, `InProgress -> OnDeck`, `OnDeck/InProgress -> Deleted`.
- **Recurring transitions**: `Scheduled -> OnDeck -> InProgress -> Completed`, `InProgress -> OnDeck`, `OnDeck/InProgress -> Skipped`.
- **Completed, Deleted, and Skipped are terminal for normal state flow**; recurring commands also enforce the newer-active-instance rule.

## Code Organization Rules

- Domain models, exceptions, repositories, and services live under `Task/HereAndNow.Task/`.
- Commands, DTOs, mappers, validation attributes, controllers, and middleware live under `Web/HereAndNow.Web/`.
- Mappers are static classes; do not introduce AutoMapper for routine DTO mapping.
- Controller actions should declare `ProducesResponseType` metadata.

## Testing Rules

- Use xUnit, Moq, and FluentAssertions.
- Prefer `MethodName_Condition_ExpectedResult` naming.
- Follow Arrange / Act / Assert consistently.
- Use the standard authenticated test user: `auth0|test-user-123`.
- Controller tests set `ControllerContext.HttpContext.User` with a `ClaimTypes.NameIdentifier` claim.
- Unit tests mock services and repositories; do not hit Cosmos DB in unit tests.

## Critical Don't-Miss Rules

- **Never update a task and reminder in separate calls** when the operation must stay atomic.
- **Never persist recurring task instances**.
- **Never use `DateTime.Now`**.
- **Never add a new mutation endpoint when a command should be added instead**.
- **Never bypass the user partition key**.
- **Never store RRULE values with the `RRULE:` prefix**.
- **Never invent new state strings**; reuse `TaskState` constants.

---

## Usage Guidance

- Read this file before implementing service changes.
- If this file conflicts with code, verify the current code and update this file.
- Use this file as a decision filter, not as a substitute for reading the relevant controller, service, repository, and docs files.

---

_Generated by BMAD Method generate-project-context workflow_
_Source: docs/index.md + docs/architecture.md + Program.cs + CommandsController.cs + task models/repositories_
_Last Updated: 2026-03-29_
