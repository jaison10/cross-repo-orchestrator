---
name: orchestration
description: The master playbook for the repo-orchestrator. Defines mode detection, route selection, keys, task slug, stage order, gates, and data-passing rules. The orchestrate command loads this first.
---

# Orchestration playbook

You are the main thread. You dispatch subagents and talk to the user. You never read repo source yourself.

## Core invariants
- Move only PATHS and SHORT SUMMARIES between stages. Never load file bodies into your context — that is what keeps the session light and resumable.
- Exclusive ownership: source → implementer, tests → test-author, knowledge docs → knowledge-updater, plan → planner. Never let two agents write the same thing.
- Gates are the user's steering points; subagents cannot ask the user anything.

## Keys and task slug (compute once at Stage 0, pass to agents)
- task-slug: derive from the request at intake, e.g. `refund-endpoint`, then append date → `refund-endpoint-2026-11-11`. Reuse the SAME slug for every stage of this run. On /resume it comes back with the session; in a fresh session, the user points at the task folder by path.
- repo-key (per repo): slug of the repo folder name + "-" + first 8 chars of a hash of its ABSOLUTE path. Same repo → same key; different repos never collide.
- parent-key (cross-repo only): the same formula applied to the PARENT folder's path.
- Single-repo: parent-key and repo-key are the same; there is only one key.

## Where artifacts live
CENTRAL = `~/.orchestrator/`. Within a key, work products live under `tasks/<task-slug>/`.

Whole-feature artifacts → PARENT-key (single-repo: the one key):
- `<CENTRAL>/<parent-key>/tasks/<task-slug>/` → analysis-report.md, plan.md, verification-report.md

Per-repo artifacts → that repo's REPO-key:
- `<CENTRAL>/<repo-key>/tasks/<task-slug>/` → review-report.md, test-report.md

In-repo, durable (per repo, never at the parent): CLAUDE.md (root); ARCHITECTURE.md, FEATURES.md, decisions.md, .repo-analysis.json under `<repo>/.claude/orchestrator/`.
Parent (cross-repo only): `parent/.claude/orchestrator/WORKSPACE.md` — index of repos + relationships. No ARCHITECTURE.md at the parent.

## What to pass each agent
- analyzer, planner, verifier → the PARENT task path `<CENTRAL>/<parent-key>/tasks/<task-slug>/` + the repo path(s).
- implementer, code-reviewer, test-author → that repo's task path `<CENTRAL>/<repo-key>/tasks/<task-slug>/` + the repo path.
- Always pass resolved ABSOLUTE paths (expand `~` yourself); do not rely on agents expanding `~`.

## Stage 0 — intake + route + mode (you do this yourself)
1. Detect mode from cwd: a `.git` here → single-repo; multiple child dirs with `.git` → cross-repo; BOTH (repo containing sub-repos) → ask the user once which they mean.
2. Classify the task: analysis / bug-fix / feature. State your pick and let the user override.
3. Extract any repos/constraints the user already named.
4. Compute task-slug and the key(s); hold them for the whole run.

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
6. code-reviewer (per repo) → (feature) GATE 4: write tests?
7. test-author (per repo).
8. knowledge-updater (per repo).

## Mid-run changes
At a gate: edit the relevant file and re-present. Mid-subagent: the user interrupts, you amend inputs and re-dispatch. After a later stage: edit the upstream file, re-run from that stage forward.

## Escalation
If an agent returns an escalation flag (task bigger/deeper than its route assumed), tell the user and offer to switch route rather than continuing on the light path.