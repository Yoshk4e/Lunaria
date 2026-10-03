# Automatic state and persistence tracking

Gameplay changes values directly. `TrackedObject` and the tracked collections record original values on the first mutation; `Player.Changes` observes the state graph. There are no gameplay `Dirty()` calls or manager-flag aggregation lists.

## Declaring state

Use a partial class derived from `TrackedObject`. A tracked property has a backing field named `__tracked` followed by the exact property name:

```csharp
private uint __trackedLevel = 1;
[Tracked]
public partial uint Level { get; private set; }

private readonly TrackedSortedDictionary<int, long> __tracked_balances = [];
[Tracked]
private partial TrackedSortedDictionary<int, long> _balances { get; }
```

The Roslyn generator implements getters and setters. Collection getters connect their journal to the owner. `[TrackChildren]` generates the player's connections to its managers, so adding another tracked manager does not require changing `IsDirty`.

- `TrackedDictionary` and `TrackedSortedDictionary` record additions, replacements, removals, and mutations to tracked values by key.
- `TrackedList` tracks ordered content. Supply a stable key selector for entity lists, as the roster and role list do, to track individual rows.
- `TrackedSet` tracks membership by value and uses sorted iteration. Elements must have a stable identity and ordering.
- Mutable entry objects derive from `TrackedObject`; immutable records are replaced with `with`. Read-only collection members of immutable records must remain immutable after construction. Never mutate their backing collections through casts or retained aliases.
- `[Untracked]` explicitly identifies transient state, caches, callbacks, and pending protocol notifications.

`LUNTRACK001` rejects mutable fields/properties without a tracking declaration. `LUNTRACK002` rejects ordinary mutable collections and mutable entry types inside tracked values, including entries nested in read-only collection declarations. These checks run in the existing analyzer build pipeline.

Repeated edits coalesce against the original value. Assigning the same value, reverting a value, and adding then removing an entry produce no persistence change. Structural comparisons include nested values; they do not rely on hash equality. Snapshots are taken only for mutation candidates, not the entire player on each request.

## Loading and committing

Each session's event loop owns its state and serializes operations and saves. A standalone manager or inactive player's `Load` establishes a baseline. Replacing state on an active player remains a tracked mutation. Database hydration accepts the complete graph before initialization; starter grants and initialization then run with tracking enabled. Save-document repairs and legacy-format conversion explicitly invalidate the persistence baseline.

`RoleStateStore` captures the selected rows, sections, and journal batch before awaiting database work. It accepts that batch and the section baselines only after transaction commit. Failure retains changes for retry. Accepting a batch cannot clear mutations made after its capture. `ClearDirty`/`ClearSaveDirty` remain available for explicit baseline setup; normal persistence uses captured batches.

Protocol notification queues remain independent. Accepting persistence changes does not drain unsent inventory, wallet, or motive notifications.

## Persistence units and migration

Characters, motives, and mail use stable IDs. Repositories load only affected rows, and EF Core updates only different columns. The shared instance-ID counter is tracked independently of the roster. Guide state remains one guide document.

`RoleSaveMapper.Sections` defines the independently persisted save-document properties and their tracked dependencies. Capture functions build only candidate sections. `RoleSaveBaseline` compares each candidate with its last committed representation before issuing a write. Add a section and its dependency when adding persisted state; the coverage test checks that every top-level save-document property is registered.

`role_save_sections` uses `(role_id, name)` as its key. `role_saves` retains a small version/existence header. Changes to wallet, bag, map, or quests update their own sections; a changed section is rewritten as a unit. Individual entries inside those JSON sections are not separate SQL rows.

The `SplitRoleSaveSections` migration creates the new table. Existing documents are read with the existing payload-version migration rules, then converted on the next successful role save in the same transaction as gameplay changes. Corrupt or future-version documents are rejected. Downgrading reassembles the legacy document before dropping the section table.

Checks cover scalar and nested mutations, reverted changes, captured-batch acceptance, SQL write scope, instance IDs, active-state replacement, partial-write rollback/retry, legacy loading, and database migration round trips.
