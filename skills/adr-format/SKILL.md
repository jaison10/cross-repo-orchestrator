---
name: adr-format
description: The format for entries in decisions.md. Loaded by agents that record implementation decisions.
---

# Decision log format

Append to `<MAIN>/.claude/orchestrator/decisions.md`. One entry per consequential decision: a new dependency, a schema or interface change, a deviation from an existing pattern, anything a reasonable engineer could dispute.

Format, each entry:

    ## ADR-<NNN>: <short title>  (<date>)
    - Context: what forced a choice.
    - Decision: what was chosen.
    - Consequence: trade-off accepted, what this rules out.

Number sequentially (ADR-001, ADR-002, ...). Never edit past entries; append only.

Re-read the file immediately before appending, because another worktree session may have written to it.

## Location
decisions.md is per-repo and lives under MAIN: `<MAIN>/.claude/orchestrator/decisions.md`. In cross-repo work each repo keeps its own; there is no combined decisions file at the parent.