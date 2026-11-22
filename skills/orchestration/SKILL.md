---
name: orchestration
description: The master playbook for the repo-orchestrator. Defines mode detection, route selection, keys, task slug, WORK and MAIN paths, stage order, gates, and data-passing rules. The orchestrate command loads this first.
---

# Orchestration playbook

You are the main thread. You dispatch subagents and talk to the user. You never read repo source yourself.

## Core invariants
- Move only PATHS and SHORT SUMMARIES between stages. Never load file bodies into your context.
- Exclusive ownership: source goes to the implementer, tests to the test-author, knowledge docs to the knowledge-updater, plan to the planner. Never let two agents write the same thing.
- Gates are the user's steering points. Subagents cannot ask the user anything.

## Keys and task slug (compute once at Stage 0, pass to agents)
- task-slug: short kebab-case slug of the request plus the date, e.g. `refund-endpoint-2026-11-15`. Reuse the SAME slug for every stage of the run. On /resume it comes back with the session. In a fresh session, the user points at the task folder by path.
- repo-key (per repo): slug of the MAIN repo folder name + "-" + first 8 chars of a hash of the MAIN folder's absolute path. Worktrees of one repo share a repo-key; the task-slug keeps their central folders apart.
- parent-key (cross-repo only): the same formula applied to the PARENT folder's path.
- Single-repo: there is one key.

## WORK and MAIN paths
- WORK is the folder where code is read and edited: the worktree folder if branch prep created one, otherwise the repo folder.
- MAIN is the main repo folder: `(cd <WORK> && cd "$(git rev-parse --git-common-dir)/.." && pwd)`.
- Code (read, edit, diff, tests) lives in WORK.
- repo-key and the git-excluded knowledge files live under MAIN.
- CLAUDE.md is tracked and lives at the WORK root, so its updates ride on the feature branch.
- Reason: git-excluded files do not exist inside a worktree folder. Anchoring them on MAIN keeps the analysis cache and decisions alive after the worktree is removed.
- Ensure `.claude/orchestrator/` is listed in the exclude file at `$(git -C <MAIN> rev-parse --git-common-dir)/info/exclude`. Append it if missing. Never edit .gitignore.

## Where artifacts live
CENTRAL = `~/.orchestrator/` (expand `~` to an absolute path yourself). Within a key, work products live under `tasks/<task-slug>/`.

Whole-feature artifacts, under the PARENT-key (single-repo: the one key):
- `<CENTRAL>/<parent-key>/tasks/<task-slug>/` holds analysis-report.md, plan.md, verification-report.md
- `<CENTRAL>/<parent-key>/tasks/<task-slug>/gates/` holds the browser gates (`gate-N.json` written by you, `gate-N.state.json` written by the page). See `browser-gate`. The reports above stay the source the agents read; the gate files are the record of what the user answered.

Per-repo artifacts, under that repo's REPO-key:
- `<CENTRAL>/<repo-key>/tasks/<task-slug>/` holds review-report.md, test-report.md

In-repo, durable:
- CLAUDE.md at the WORK root.
- ARCHITECTURE.md, FEATURES.md, decisions.md, .repo-analysis.json under `<MAIN>/.claude/orchestrator/`.
- Cross-repo parent only: `<parent>/.claude/orchestrator/WORKSPACE.md`, an index of the repos and their relationships. No ARCHITECTURE.md at the parent.

## What to pass each agent
- Every agent receives WORK, MAIN, the branch name and START (the ref the branch was created from). Always absolute paths.
- analyzer, planner, verifier also get the PARENT task path `<CENTRAL>/<parent-key>/tasks/<task-slug>/`.
- implementer, code-reviewer, test-author also get their repo's task path `<CENTRAL>/<repo-key>/tasks/<task-slug>/`.

## Stage 0: intake, route, mode (you do this yourself)
1. Detect mode with git, not by looking for a .git folder (a worktree's .git is a file). If `git rev-parse --show-toplevel` succeeds, it is single-repo. Otherwise, if child folders are git repos (`git -C <child> rev-parse --show-toplevel` succeeds, skipping children whose `.git` is a file), it is cross-repo. If both apply (a repo containing sub-repos), ask the user once.
2. Pick the route: analysis, bug-fix or feature.
   - The user may pass a route flag anywhere in the request: `--analysis`, `--fix` (alias `--bug`) or `--feature`. Strip the flag from the requirement text. If it is given, use it, ask nothing, and echo `Route: <route>` so the user can correct it. If two different route flags appear, ask which one is meant.
   - If no flag is given, ask the user to choose between analysis, bug-fix and feature (AskUserQuestion, one question, three options), with your best guess from the wording listed first and marked "(Recommended)". If no requirement text was given either, ask for the route first, then the requirement.
   - The user can still override the route later at any gate.
3. Extract any repos or constraints the user already named.
4. Compute the task-slug and the key(s). Hold them for the whole run.

## Routes
- analysis: 0, then 0b (cross-repo), then Gate 1, then fetch and behind-warning only (see `branch-prep`), then analyzer (dispatched with `route=analysis`, so it returns the short report view), then present the report (see "Presenting an analysis"), then STOP. The report is the deliverable.
- bug-fix: 0, then 0b (cross-repo), then Gate 1 merged with branch prep, then analyzer (narrow), then planner (light), then implementer, then verifier, then knowledge-updater. Skip Gates 3 and 4 unless the user asks.
- feature: all stages, all gates, including branch prep.
- Single-repo: skip 0b. Branch prep still runs on bug-fix and feature.

## Dispatch order and gates
Gates 1 and 2 are shown through the `browser-gate` skill when it is usable (user config `ui` is `browser`, not a remote session, server starts), so the user can read the understanding, repos, plan and questions in a page, answer there, ask their own questions and raise concerns. Your run resumes when they submit. If it is not usable, run the gate in the console exactly as described below. Gates 3 and 4 are always console. Questions from the analyzer and planner arrive as a structured block (see `user-summaries`); in the console, print them numbered as before.

1. (cross-repo) discovery.
2. Branch prep, per the `branch-prep` skill, on bug-fix and feature routes. Cross-repo: scan the proposed repo set, then show ONE combined Gate 1 screen (repo set plus branch actions). Single-repo: the same flow with a one-row table. Analysis route: Gate 1 confirms the repo set only.
3. analyzer (per repo). Bug-fix and feature: show its understanding. Analysis route: present the report as below, then STOP.
4. planner. GATE 2: the user approves the plan and answers open questions. Append the answers and `## Approved: <timestamp>` to plan.md. In the browser, one page carries the understanding, the plan and the questions of both the analyzer and the planner; "revise" re-runs the planner and "ask" is answered by a subagent, each as a new round of the same gate.
5. implementer (one per repo).
6. verifier. Feature route: GATE 3, start review?
7. code-reviewer (per repo). Feature route: GATE 4, write tests?
8. test-author (per repo).
9. knowledge-updater (per repo).
10. End-of-run reminders from `branch-prep`.

## Presenting an analysis (analysis route)
The analysis report is shown as a page when the `browser-gate` conditions hold, because a short report with diagrams reads far better there than in a terminal.
1. Cross-repo: put each repo's returned report under a `### <repo>` heading (path and summary glue only; never read source). Single-repo: use the report as returned.
2. Write `gates/gate-2.json` (same GATE_DIR as the other gates) with `round: 1`, `view: "report"`, `route: "analysis"`, `mode`, `title`, `understanding` (the report text), `reports: ["analysis-report.md"]`, and `questions` from the analyzer's block (they are optional in this view). Use the `browser-gate` flow (`open`, then `wait`).
3. Actions: `approve` means Done, so stop. `ask`: the user wrote a follow-up; dispatch the analyzer with the question and the task paths, put its short answer in `thread` with `to`, raise `round`, and `wait` again. `console`: print it as in step 4. There is no `revise` here. Do not append anything to plan.md: there is no plan on this route.
4. Console fallback (browser not usable, or the user chose it): print the TL;DR, Key findings and Assumptions as text, and the diagrams as fenced `mermaid` blocks. End with the path to `analysis-report.md`.
After the report, optionally suggest in one line that the user can run a bug-fix or feature route if they want to act on it. Do not offer a button for it.

## Showing a standalone report
`/orch:show <file.md>` opens any markdown file (a plan, an old analysis) in this same report page, with no pipeline run. See that command.

## Mid-run changes
At a gate: edit the relevant file and re-present (in the browser: a new round of the same gate). Mid-subagent: the user interrupts, you amend inputs and re-dispatch. After a later stage: edit the upstream file and re-run from that stage forward.

## Escalation
If an agent returns an escalation flag (the task is bigger or deeper than its route assumed), tell the user and offer to switch route rather than continuing on the light path.

DUMMY ADDITIONAL ADDED