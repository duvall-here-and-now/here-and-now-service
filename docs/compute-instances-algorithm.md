# ComputeInstances Algorithm — Deep Dive

This document provides a detailed explanation of the `RecurringTaskService.ComputeInstances()` method, the core algorithm that resolves recurring task states for display in the HereAndNow frontend.

## Purpose

`ComputeInstances` is a **pure computation function** that takes recurring task configurations, stored state overrides, a date range, and the current UTC time — and produces a list of `RecurringTaskInstance` objects, each with a resolved state. It is the single source of truth for what state every recurring task occurrence is in at any given moment.

Because it is pure (no I/O, no side effects), it is fully deterministic: the same inputs always produce the same outputs.

## Signature

```csharp
public IReadOnlyList<RecurringTaskInstance> ComputeInstances(
    IReadOnlyList<RecurringTaskConfigDocument> configs,
    IReadOnlyList<RecurringTaskStateOverrideDocument> overrides,
    DateTime from,
    DateTime to,
    DateTime utcNow)
```

### Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `configs` | `IReadOnlyList<RecurringTaskConfigDocument>` | All recurring task configurations for the user |
| `overrides` | `IReadOnlyList<RecurringTaskStateOverrideDocument>` | Stored state overrides (InProgress, Completed, Skipped) within the date range |
| `from` | `DateTime` (UTC) | Start of the query date range |
| `to` | `DateTime` (UTC) | End of the query date range |
| `utcNow` | `DateTime` (UTC) | The current time, used to classify occurrences as past or future |

### Return Value

`IReadOnlyList<RecurringTaskInstance>` — A flat list of all computed instances across all configs, each with a resolved state.

## Data Model Context

### RecurringTaskConfigDocument (Input)

A persisted document in Cosmos DB defining a recurring task:
- `Id` — Unique identifier
- `UserId` — Owner (partition key)
- `Text` — Task description
- `Rrule` — iCalendar recurrence rule string (e.g., `FREQ=DAILY;BYHOUR=9;BYMINUTE=0;BYSECOND=0`)
- `StartDateAndTime` — UTC datetime when the recurrence pattern begins (used as DTSTART)

### RecurringTaskStateOverrideDocument (Input)

A persisted document recording an explicit user action on a specific occurrence:
- `Id` — Composite key: `{ConfigId}_{RecurrenceDateAndTime:O}` (round-trip format)
- `ConfigId` — References the parent config
- `RecurrenceDateAndTime` — Which specific occurrence this override applies to
- `State` — The stored state: `InProgress`, `Completed`, or `Skipped`

Only these three states are ever written to Cosmos DB. States like `OnDeck` and `Scheduled` are computed, never stored.

### RecurringTaskInstance (Output)

A computation-only model (never persisted) representing one occurrence with its resolved state:
- `RecurringTaskConfigId` — Parent config ID
- `Text` — Task text (derived from config)
- `RecurrenceDateAndTime` — When this occurrence falls
- `State` — One of: `Scheduled`, `OnDeck`, `InProgress`, `Completed`, `Skipped`
- `Id` — Composite key: `{ConfigId}_{RecurrenceDateAndTime:yyyy-MM-ddTHH:mm:ssZ}`

### TaskState Constants

```
Scheduled   — Future occurrence, not yet actionable
OnDeck      — Most recent past occurrence ready for action
InProgress  — User has explicitly started working on this
Completed   — User marked as done (terminal)
Skipped     — Automatically skipped or user-skipped (terminal)
```

## Algorithm — Step by Step

### Phase 1: Build Override Lookup

```csharp
var overrideLookup = overrides.ToDictionary(
    o => $"{o.ConfigId}_{o.RecurrenceDateAndTime:O}");
```

All stored overrides are indexed into a dictionary keyed by `{ConfigId}_{OccurrenceDateTime}` using the round-trip (`:O`) format. This enables O(1) lookup when processing each occurrence.

**Critical detail:** Both the lookup key construction here and the key construction during traversal must use the identical `:O` format specifier. A format mismatch would cause overrides to silently fail to match their occurrences.

### Phase 2: Process Each Config Independently

```csharp
foreach (var config in configs)
{
    RecurringTaskInstance? activeCandidate = null;
    var configInstances = new List<RecurringTaskInstance>();
    // ... per-config processing
}
```

Each config is processed in complete isolation. The `activeCandidate` variable is scoped per-config, meaning one config's state resolution never affects another's. This enforces the invariant: **one active instance at a time, per config**.

### Phase 3: Generate Occurrences via RRULE

```csharp
var effectiveFrom = config.StartDateAndTime > from ? config.StartDateAndTime : from;
var occurrences = GetOccurrences(config.Rrule, config.StartDateAndTime, effectiveFrom, to);
```

The `GetOccurrences` helper uses **Ical.Net** to expand the RRULE pattern into concrete UTC datetimes:
- `DTSTART` is always the config's original `StartDateAndTime` (preserves the recurrence pattern's anchor point)
- The `effectiveFrom` parameter filters out occurrences before the config's start date (FR75/AC2), even if the query range starts earlier
- The `to` parameter caps the end of the range

### Phase 4: Newest-First Traversal (The Core Algorithm)

```csharp
foreach (var occurrence in occurrences.OrderByDescending(o => o))
```

This is the critical design decision. Occurrences are sorted **newest to oldest** before processing. This enables the `activeCandidate` sentinel pattern: by processing the most recent occurrence first, the algorithm can determine which occurrence "wins" the active slot and mark all older ones as Skipped.

### Phase 5: State Resolution (Per Occurrence)

For each occurrence, the algorithm checks — in priority order:

#### Branch 1: Has a Stored Override?

```csharp
if (overrideLookup.TryGetValue(key, out var stateOverride))
```

If the occurrence has a matching override in Cosmos DB, the stored state determines the branch:

##### Branch 1a: Terminal State (Completed or Skipped)

```csharp
if (storedState == TaskState.Completed || storedState == TaskState.Skipped)
{
    var inst = new RecurringTaskInstance(config, occurrence, storedState);
    configInstances.Add(inst);
    if (occurrence <= utcNow && activeCandidate == null)
        activeCandidate = inst;
}
```

Terminal states are **always respected** — they are never recalculated. The stored state passes through directly.

**The activeCandidate assignment** (added to fix the OnDeck leak bug): If this terminal occurrence is in the past and no active candidate has been claimed yet, the terminal instance is set as the `activeCandidate`. This prevents older past occurrences from incorrectly claiming the OnDeck slot. The logic: a Completed/Skipped occurrence has already "consumed" its active slot — even though it's no longer active, it has been handled, and older occurrences should not be promoted.

##### Branch 1b: InProgress (Active State with Override)

```csharp
else if (storedState == TaskState.InProgress)
{
    if (activeCandidate == null)
    {
        activeCandidate = new RecurringTaskInstance(config, occurrence, TaskState.InProgress);
        configInstances.Add(activeCandidate);
    }
    else
    {
        configInstances.Add(new RecurringTaskInstance(config, occurrence, TaskState.Skipped));
    }
}
```

InProgress is an **active** state subject to the one-active-at-a-time rule:
- If no active candidate exists yet, this InProgress instance claims the slot
- If an active candidate already exists (a more recent occurrence was already processed), this InProgress is **superseded** and becomes Skipped (AC6)

This handles the scenario where a user started working on an older occurrence, but a newer occurrence has since appeared. The newer one takes priority.

##### Branch 1c: Unexpected State (Defensive Fallback)

```csharp
else
{
    _logger.LogWarning(...);
    configInstances.Add(new RecurringTaskInstance(config, occurrence, storedState));
}
```

If the stored state is anything other than Completed/Skipped/InProgress (e.g., `OnDeck` or `Scheduled` — computed states that should never be persisted), the instance is **passed through as-is** with a warning log.

**Important:** This branch intentionally does NOT set `activeCandidate`. Unexpected states are treated as pass-through anomalies, not legitimate state transitions. This prevents data corruption from blocking the normal algorithm flow.

#### Branch 2: No Override, Future Occurrence

```csharp
else if (occurrence > utcNow)
{
    configInstances.Add(new RecurringTaskInstance(config, occurrence, TaskState.Scheduled));
}
```

Future occurrences with no override are always `Scheduled`. They cannot be OnDeck because they haven't happened yet.

#### Branch 3: No Override, Past Occurrence, No Active Candidate Yet

```csharp
else if (activeCandidate == null)
{
    activeCandidate = new RecurringTaskInstance(config, occurrence, TaskState.OnDeck);
    configInstances.Add(activeCandidate);
}
```

The **first** (most recent, due to newest-first ordering) past occurrence with no override becomes `OnDeck` — the one task instance the user should act on next. It also claims the `activeCandidate` slot, blocking all older occurrences.

#### Branch 4: No Override, Past Occurrence, Active Candidate Already Exists

```csharp
else
{
    configInstances.Add(new RecurringTaskInstance(config, occurrence, TaskState.Skipped));
}
```

All older past occurrences that weren't overridden are automatically `Skipped`. Since `activeCandidate` is no longer null, they cannot become OnDeck.

### Phase 6: Aggregate Results

```csharp
results.AddRange(configInstances);
```

After processing all occurrences for a config, the instances are appended to the global results list. The next config starts fresh with its own `activeCandidate = null`.

## The activeCandidate Sentinel — Visual Walkthrough

The `activeCandidate` variable is the heart of the algorithm. Here's a visual example:

### Scenario: Daily task, 3 occurrences in range, no overrides

```
utcNow = Feb 15 12:00 UTC

Occurrences (sorted newest-first for traversal):
  Feb 16 09:00  →  future (> utcNow)    →  Scheduled
  Feb 15 09:00  →  past, no candidate   →  OnDeck      ← activeCandidate set here
  Feb 14 09:00  →  past, has candidate  →  Skipped
```

### Scenario: Same task, but Feb 15 was Completed by user

```
utcNow = Feb 15 12:00 UTC
Override: Feb 15 = Completed

Occurrences (sorted newest-first):
  Feb 16 09:00  →  future              →  Scheduled
  Feb 15 09:00  →  override: Completed →  Completed    ← activeCandidate set here (past terminal)
  Feb 14 09:00  →  past, has candidate →  Skipped      ← correctly blocked from OnDeck
```

Without the terminal-state activeCandidate fix, Feb 14 would have incorrectly become OnDeck because `activeCandidate` would still be null after processing the Completed Feb 15.

### Scenario: InProgress on older occurrence, superseded by newer

```
utcNow = Feb 15 12:00 UTC
Override: Feb 14 = InProgress

Occurrences (sorted newest-first):
  Feb 16 09:00  →  future              →  Scheduled
  Feb 15 09:00  →  past, no candidate  →  OnDeck       ← activeCandidate set here
  Feb 14 09:00  →  override: InProgress, but candidate exists → Skipped (superseded)
```

## Key Invariants

1. **One active at a time (per config):** At most one occurrence per config can be OnDeck or InProgress. The `activeCandidate` sentinel enforces this.

2. **Terminal states are final:** Completed and Skipped overrides are never recalculated. They pass through exactly as stored.

3. **Newest wins:** The newest-first traversal guarantees that the most recent past occurrence gets first claim on the active slot.

4. **Past terminals block older promotions:** A Completed or Skipped occurrence in the past counts as "handled" — older occurrences cannot be promoted to OnDeck.

5. **Configs are independent:** Each config's state resolution is fully isolated. Config A's active candidate does not affect Config B.

6. **Deterministic:** Same inputs, same outputs. The `utcNow` parameter is injected (not read from the system clock), making the function testable and reproducible.

## Caller Context

`ComputeInstances` is called from two places:

1. **`GetComputedInstancesAsync`** — The public API path. Fetches configs and overrides from Cosmos DB using the two-query pattern (NFR44), then delegates to `ComputeInstances` with `DateTime.UtcNow`.

2. **`GetTargetInstanceAsync`** — Internal helper used by state transition commands (Start, Complete, Skip, Revert). Computes instances for a wide range (+-365 days) around the target occurrence to find the specific instance's current computed state and to check for newer active instances.

## State Transition Commands and ComputeInstances

The state transition methods (`StartRecurringTaskAsync`, `CompleteRecurringTaskAsync`, etc.) do NOT modify computed state directly. Instead they:

1. Call `ComputeInstances` to determine the **current computed state** of the target instance
2. Validate the transition is legal (e.g., only OnDeck can be started)
3. Write a `RecurringTaskStateOverrideDocument` to Cosmos DB
4. On the next call to `ComputeInstances`, the new override is picked up and the state resolves accordingly

This separation means `ComputeInstances` is always the authority on current state, and state transitions are validated against the computed view, not raw stored data.

## Performance Characteristics

- **Time complexity:** O(C * O * log O) where C = number of configs, O = max occurrences per config in range. The `log O` factor comes from sorting occurrences.
- **Space complexity:** O(N) where N = total instances across all configs.
- **All in-memory:** After the two initial DB queries, the entire computation happens without further I/O (NFR44).
- **Override lookup:** O(1) per occurrence via dictionary.

## Related Files

| File | Role |
|------|------|
| `Task/HereAndNow.Task/Services/RecurringTaskService.cs` | Implementation |
| `Task/HereAndNow.Task/Models/RecurringTaskInstance.cs` | Output model |
| `Task/HereAndNow.Task/Models/RecurringTaskConfigDocument.cs` | Config input model |
| `Task/HereAndNow.Task/Models/RecurringTaskStateOverrideDocument.cs` | Override input model |
| `Task/HereAndNow.Task/Models/TaskState.cs` | State constants |
| `Task/HereAndNow.Task.Tests/Services/RecurringTaskServiceTests.cs` | Unit tests |
