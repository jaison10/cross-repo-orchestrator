---
name: rules-efcore
description: Apply when the change touches EF Core. Loaded with database.md.
---

# EF Core rules
EF Core specifics for the generic rules in database.md.

## Read vs write
- `AsNoTracking()` on every read-only query; omit it only when you will mutate and save the entities.
- Use the read-replica context (e.g. `ReplicaDB`) for reads when one exists.

## Avoiding N+1
- `Include()` / `ThenInclude()` or `Select` projection to load related data in one round trip.
- Any navigation-property access inside a `foreach` over query results is a red flag.
- Prefer `Select` into a DTO over full entities; it often removes the need for `Include`.
- `AsSplitQuery()` when multiple collection `Include`s cause a cartesian explosion.

## Keeping queries in the database
- No `.ToList()` / `.ToArray()` / `.AsEnumerable()` before `Where`, `OrderBy` or paging.
- No untranslatable method calls in predicates.
- `Skip()` / `Take()` for large sets, after an `OrderBy` on a stable key.

## Existence and aggregates
- `AnyAsync(predicate)` for existence, not `CountAsync() > 0`.
- `CountAsync` / `SumAsync` / `MaxAsync` so aggregation runs in SQL.

## Async and correctness
- Async query methods (`ToListAsync`, `FirstOrDefaultAsync`, `AnyAsync`, ...); no sync equivalents on async paths.
- `SingleOrDefault` only when at most one row must match; `FirstOrDefault` when "first match" is the intent; no `Single` on unbounded sets.

## DbContext lifetime and batching
- Scoped `DbContext`; never share it across threads or capture it beyond its scope.
- `AddRange` and a single `SaveChangesAsync()` per unit of work, not saving inside a loop.
- `ExecuteUpdate` / `ExecuteDelete` for set-based changes that don't need loaded entities.
- Explicit transaction when several `SaveChanges` calls must succeed or fail together.

## Tracking hygiene
- Load entities tracked for update paths, or attach and mark modified explicitly when disconnected.
- Fetch only the entities and columns the write needs.
