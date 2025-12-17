# Engineering Backlog

This backlog collects cross-cutting or future action items that emerge from reviews and planning.

Routing guidance:

- Use this file for non-urgent optimizations, refactors, or follow-ups that span multiple stories/epics.
- Must-fix items to ship a story belong in that story's `Tasks / Subtasks`.
- Same-epic improvements may also be captured under the epic Tech Spec `Post-Review Follow-ups` section.

| Date | Story | Epic | Type | Severity | Owner | Status | Notes |
| ---- | ----- | ---- | ---- | -------- | ----- | ------ | ----- |
| 2025-12-17 | STORY-cosmos-db-migration | N/A | Test Gap | High | TBD | Open | Add unit tests for `CosmosReminderInstanceService` with mocked `Container` - Task 7.2 marked complete but not implemented |
| 2025-12-17 | STORY-cosmos-db-migration | N/A | Refactor | Low | TBD | Open | Convert synchronous Cosmos operations to async (CosmosReminderInstanceService.cs:61, 90, 132, 160, 174, 206, 218-221) |
| 2025-12-17 | STORY-cosmos-db-migration | N/A | Bug | Low | TBD | Open | Add explicit null validation for `UserId` in `Update` method (CosmosReminderInstanceService.cs:159) |
