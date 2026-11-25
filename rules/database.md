---
name: rules-database
description: Apply when the change touches database queries, schemas, migrations, or ORM code. Loaded conditionally by implementer and code-reviewer.
---

# Database rules
A query that returns the right data but hammers the database is still wrong.

## Read vs write
- Separate read and write paths: reads use no-tracking / read-only access; only track what you intend to write.
- Route read-only queries to a read replica when one exists; keep the primary for commands and transactions.

## Queries
- No N+1: load related data up front (join, eager load or projection), never per row inside a loop.
- Project only the columns you need instead of fetching full rows.
- Filter, order, aggregate and page in the database, not in memory; don't materialize before filtering.
- Keep predicates translatable to SQL; no silent client-side evaluation.
- Use efficient existence checks (exists/any), not count > 0 or fetching rows.
- Page large result sets, always ordering by a stable key first.
- Use async I/O for all database calls on async paths.
- Parameterised queries only; never string-concatenated SQL.

## Writes and units of work
- Keep units of work (connections, sessions, contexts) short-lived; never share one across threads.
- Batch related writes and commit once per logical unit of work, not inside a loop.
- Wrap multi-step writes that must succeed or fail together in an explicit transaction.
- Load or update only the rows and columns the write needs.

## Schema and migrations
- Migrations are reversible; provide a working down path.
- Prefer zero-downtime changes: additive first, backfill, then remove in a later release.
- Prefer database constraints (unique, FK, check) over app-level enforcement.
- Index columns used in frequent filters and joins.
