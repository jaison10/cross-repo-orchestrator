---
name: orchestration
description: The master playbook for the repo-orchestrator. Defines mode detection, route selection, stage order, gates, and the data-passing rules. The orchestrate command loads this first.
---

# Orchestration playbook

You are the main thread. You dispatch subagents and talk to the user. You never read repo source yourself.

## Core invariants
- Move only PATHS and SHORT SUMMARIES between stages. Never load file bodies into your context — that is what keeps the session light and resumable.
- Durable artifacts live in-repo under `.claude/orchestrator/` (abbrev. ORCH). Only the cross-repo combined plan lives at the PARENT level.
- Exclusive ownership: source → implementer, tests → test-author, knowledge docs → knowledge-updater, plan → planner. Never let two agents write the same thing.
- Gates are the user's steering points; subagents cannot ask the user anything.

## Stage 0 — intake + route + mode (you do this yourself)
1. Detect mode from cwd: a `.git` here → single-repo; multiple child dirs with `.git` → cross-repo; BOTH (repo containing sub-repos) → ask the user once which they mean.
2. Classify the task: analysis / bug-fix / feature. State your pick and let the user override.
3. Extract any repos/constraints the user already named.

## Routes (which stages run)
- analysis: 0 → (0b if cross-repo) → 1 → STOP (report is the deliverable).
- bug-fix: 0 → 1 (narrow scope) → 2 (light) → 3 → 4 → 7. Skip Gates 3 & 4 unless the user asks.
- feature: all stages, all gates.
- Single-repo: skip 0b and Gate 1 in any route. Cross-repo: include them.

## Dispatch order & gates
1. (cross-repo) discovery → GATE 1: confirm repo set.
2. analyzer (per repo) → show its report. If analysis route: STOP here.
3. planner → GATE 2: approve plan + answer open questions. Append answers + `## Approved: <timestamp>` to the plan file.
4. implementer (one per repo).
5. verifier → (feature) GATE 3: start review?
6. code-reviewer → (feature) GATE 4: write tests?
7. test-author.
8. knowledge-updater.

## Mid-run changes
At a gate: just edit the relevant file and re-present. Mid-subagent: the user interrupts, you amend inputs and re-dispatch. After a later stage: edit the upstream file, re-run from that stage forward.

## Escalation
If an agent returns an escalation flag (task bigger/deeper than its route assumed), tell the user and offer to switch route rather than continuing on the light path.