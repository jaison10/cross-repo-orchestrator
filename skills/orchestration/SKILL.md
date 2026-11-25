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
- HARD RULE: never dispatch the implementer until Gate 2 has been shown (browser, or console only if the browser is unusable) and the user pressed approve, even with zero questions. Gate 2 is plan approval first, questions second. Only the user's approve action allows appending `## Approved:` to plan.md.
- Answering the user (main-thread answers, gate `ask` replies, console questions):
  - Short and precise, with a short example where it helps. A straightforward question gets a word, a few words or one sentence, still checked and correct.
  - Extra explanation only when asked, but the answer must still give the user what they expected.
  - If the user did not follow (repeats the question, says "what?"), answer again shorter, with a short concrete example.
  - Many questions: one at a time. Console: ask in sequence and wait for each answer. Browser gate: the page lists all questions at once, so generate fewer, simpler ones with the recommended answer pre-selected; no long lists.

## Keys and task slug (compute once at Stage 0, pass to agents)
- task-slug: short kebab-case slug of the request plus the date, e.g. `refund-endpoint-2026-11-15`. Reuse the SAME slug for every stage of the run. On /resume it comes back with the session. In a fresh session, the user points at the task folder by path.
- repo-key (per repo): `<name>-<hash>`. Worktrees of one repo share a repo-key; the task-slug keeps their central folders apart.
  - `<name>`: the MAIN folder's name with every run of characters outside `A-Z a-z 0-9 . _ -` replaced by one `-`, leading and trailing non-alphanumerics trimmed, cut to 40 characters, case kept. The server accepts only `^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$`, so a name with spaces or other URL-unsafe characters is made safe here, when the folder is created.
  - `<hash>`: the first 8 hex characters of the SHA-1 of MAIN's absolute path written in one canonical form: forward slashes, an uppercase Windows drive letter (`D:/Jaison-Data/test`, never `D:\...` or the Git Bash form `/d/...`), no trailing slash. Example: `printf %s "D:/Jaison-Data/test" | sha1sum | cut -c1-8`.
  - Never compute the key any other way: a different path form or hash gives a different folder for the same repo.
- parent-key (cross-repo only): the same formula applied to the PARENT folder's path.
- Single-repo: there is one key.

## WORK and MAIN paths
- WORK is the folder where code is read and edited: the worktree folder if branch prep created one, otherwise the repo folder.
- MAIN is the main repo folder: `(cd <WORK> && cd "$(git rev-parse --git-common-dir)/.." && pwd)`.
- Code (read, edit, diff, tests) lives in WORK.
- repo-key and the git-excluded knowledge files live under MAIN.
- CLAUDE.md is tracked and lives at the WORK root, so its updates ride on the feature branch.
- Reason: git-excluded files do not exist inside a worktree folder. Anchoring them on MAIN keeps the knowledge files and decisions alive after the worktree is removed.
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
- ARCHITECTURE.md, FEATURES.md, decisions.md under `<MAIN>/.claude/orchestrator/`.
- Cross-repo parent only: `<parent>/.claude/orchestrator/WORKSPACE.md`, an index of the repos and their relationships. No ARCHITECTURE.md at the parent.

## What to pass each agent
- Every agent receives WORK, MAIN, the branch name and START (the ref the branch was created from; when the user stayed on the current branch, the HEAD commit at Execute). Always absolute paths.
- analyzer, planner, verifier also get the PARENT task path `<CENTRAL>/<parent-key>/tasks/<task-slug>/`.
- code-reviewer and test-author also get their repo's task path `<CENTRAL>/<repo-key>/tasks/<task-slug>/`.
- implementer gets the PARENT task path, its repo's task path, and the absolute path of the approved plan, `<PARENT task path>/plan.md`. Dispatch it only when that file has a `## Approved:` line; without the path or the line it returns a blocker.
- verifier also gets `route=<route>`. test-author on bug-fix also gets `mode=update-only` and the absolute path of `<PARENT task path>/verification-report.md`.

## Stage 0: intake, route, mode (you do this yourself)
1. Detect mode with git, not by looking for a .git folder (a worktree's .git is a file). If `git rev-parse --show-toplevel` succeeds, it is single-repo. Otherwise, if child folders are git repos (`git -C <child> rev-parse --show-toplevel` succeeds, skipping children whose `.git` is a file), it is cross-repo. If both apply (a repo containing sub-repos), ask the user once.
2. Pick the route: analysis, bug-fix or feature.
   - The user may pass a route flag anywhere in the request: `--analysis`, `--fix` (alias `--bug`) or `--feature`. Strip the flag from the requirement text. If it is given, use it, ask nothing, and echo `Route: <route>` so the user can correct it. If two different route flags appear, ask which one is meant.
   - If no flag is given, ask the user to choose between analysis, bug-fix and feature (AskUserQuestion, one question, three options), with your best guess from the wording listed first and marked "(Recommended)". If no requirement text was given either, ask for the route first, then the requirement.
   - The user can still override the route later at any gate.
3. Extract any repos or constraints the user already named.
4. Compute the task-slug and the key(s). Hold them for the whole run.
5. Load the `browser-gate` skill and run its section 1 (`config`, then `start`) once. Record GATE_UI=browser|console and the reason, and reuse it for every gate. GATE_UI is unknown before this step, which is why steps 1 and 2 ask in the console.

Questions before Gate 1:
- Console (AskUserQuestion), always: the mode question (step 1), and the route and requirement questions (step 2). They come before GATE_UI and the task folder exist.
- Console, always, as stop notices, not questions: a BLOCKED repo or the task branch checked out in another worktree (`branch-prep` section 3). The run stops before any Gate 1 page.
- Everything else branch prep needs (base, remote, saving the `## Branching` block, fetch failure, reuse branch, local or remote base, repo set, new branch or current branch, Keep/Stash/Worktree, worktree folder) is a Gate 1 question: on the Gate 1 page when GATE_UI=browser, in the console only when GATE_UI=console. A missing username uses the `setup` form when GATE_UI=browser. See `branch-prep` sections 2 to 4.
No other question is asked before Gate 1.

## Routes
- analysis: 0, then 0b (cross-repo), then Gate 1, then fetch and behind-warning only (see `branch-prep` section 1; no questions), then analyzer (dispatched with `route=analysis`, so it returns the short report view), then present the report (see "Presenting an analysis"), then STOP. The report is the deliverable.
- bug-fix: 0, then 0b (cross-repo), then Gate 1 merged with branch prep, then analyzer (narrow), then planner (light), then Gate 2 (plan approval and open questions, required before the implementer), then implementer (only once plan.md has `## Approved:`), then verifier, then Gate 4 only if the verifier lists affected tests (Dispatch step 6), then knowledge-updater. Skip Gate 3 unless the user asks.
- feature: all stages, all gates, including branch prep.
- Single-repo: skip 0b. Branch prep still runs on bug-fix and feature.

## Dispatch order and gates
Gates 1 and 2 are shown through the `browser-gate` skill when it is usable (user config `ui` is `browser` or unset, not a remote session, server starts), so the user can read the understanding, repos, plan and questions in a page, answer there, ask their own questions and raise concerns. Invoke the browser-gate skill (Skill tool) at Gate 1 and Gate 2 and follow its sections 1-4. Apart from the Stage 0 questions (see 'Questions before Gate 1'), do not ask questions in the console unless GATE_UI=console. Your run resumes when they submit. If it is not usable, run the gate in the console exactly as described below. Gates 3 and 4 are always console. Questions from the analyzer and planner arrive as a structured block (see `user-summaries`). In the browser, copy that `questions` block into the gate file and print no questions in the console, including the analyzer's numbered Open questions list. Only when the browser is unusable, print them numbered in the console.

1. (cross-repo) discovery.
2. Branch prep, per the `branch-prep` skill, on bug-fix and feature routes. Cross-repo: scan the proposed repo set, then show ONE combined Gate 1 screen (repo set plus branch actions). Single-repo: the same flow with a one-row table. Analysis route: Gate 1 confirms the repo set only. Title: `Gate 1 · Confirm repos` (analysis) or `Gate 1 · Confirm repos and branch`, see `browser-gate` Content fields. Invoke the browser-gate skill (Skill tool) for Gate 1 and follow its sections 1-4. GATE_UI decides the Gate 1 form, see `branch-prep` section 4. Branch-prep questions go in `gate-1.json` `questions`, and Gate 1 may take a second round (`branch-prep` section 4).
3. analyzer (per repo). Bug-fix and feature: show its understanding (browser: in the Gate 2 page, not the console). Analysis route: present the report as below, then STOP.
4. planner. GATE 2: invoke the browser-gate skill (Skill tool) and follow its sections 1-4. Title `Gate 2 · Approve the plan: <topic>` (`browser-gate` Content fields), the same in every round. The user approves the plan and answers open questions; with zero questions the gate is still shown and still needs approve. Append the answers and `## Approved: <timestamp>` to plan.md. In the browser, one page carries the understanding, the plan and the questions of both the analyzer and the planner; "revise" re-runs the planner and "ask" is answered by a subagent, each as a new round of the same gate.
5. implementer (one per repo).
6. verifier. Feature route: GATE 3, start review? Bug-fix route: if the verifier lists affected tests, GATE 4 (console, one yes/no question naming the count and repos): update and run them? Yes: test-author (step 8) only for those repos, update-only. No, or "none found": skip Gate 4 without asking and go to the knowledge-updater.
7. code-reviewer (per repo). Feature route: GATE 4, write tests?
8. test-author (per repo).
9. knowledge-updater (per repo).
10. End-of-run reminders from `branch-prep`.

## Presenting an analysis (analysis route)
The analysis report is shown as a page when the `browser-gate` conditions hold, because a short report with diagrams reads far better there than in a terminal.
1. Cross-repo: put each repo's returned report under a `### <repo>` heading (path and summary glue only; never read source). Single-repo: use the report as returned.
2. Write `gates/gate-2.json` (same GATE_DIR as the other gates) with `round: 1`, `view: "report"`, `route: "analysis"`, `mode`, `title` (`Report · Analysis: <topic>`, see `browser-gate` Content fields), `understanding` (the report text), `reports: ["analysis-report.md"]`, `reanalyse: true`, and `questions` from the analyzer's block (they are optional in this view). Use the `browser-gate` flow (`open`, then `wait`).
3. Actions: `approve` means Done, so stop. `ask`: the user wrote a follow-up; dispatch the analyzer with the question and the task paths, put its short answer in `thread` with `to`, raise `round`, and `wait` again. `console`: print it as in step 4. `revise` (Re-analyse with my answers): take the answers for the current questions from `state.answers`, re-dispatch the analyzer exactly as before (`route=analysis`, same task paths; cross-repo: each repo) plus the questions and answers, per analyzer 'When re-invoked with answers'. Rewrite `gate-2.json` with `round + 1`, the new report as `understanding` (cross-repo: re-glued as in step 1), the new `questions` block, `thread` and `reanalyse: true` kept. Then `wait --round <new> --timeout 120` in the background again. `approve` (Done) still just stops; answers not sent with Re-analyse are ignored. Do not append anything to plan.md: there is no plan on this route.
4. Console fallback (browser not usable, or the user chose it): print the TL;DR, Key findings and Assumptions as text, and the diagrams as fenced `mermaid` blocks. End with the path to `analysis-report.md`.
After the report, optionally suggest in one line that the user can run a bug-fix or feature route if they want to act on it. Do not offer a button for it.

## Showing a standalone report
`/orch:show <file.md>` opens any markdown file (a plan, an old analysis) in this same report page, with no pipeline run. See that command.

## Mid-run changes
At a gate: edit the relevant file and re-present (in the browser: a new round of the same gate). Mid-subagent: the user interrupts, you amend inputs and re-dispatch. After a later stage: edit the upstream file and re-run from that stage forward.

## Blockers
An agent may return a blocker (format in `rules/general.md`: type, what, why). Stop the stage and act on its type. The gates in this section use `browser-gate` when usable, else the console. The server accepts any `gate-N.json` (N up to 3 digits), so a blocker gate takes the next unused number from 5 up (`gate-5.json`, `gate-6.json`, ...); Gates 1 and 2 keep their files. Title: `Blocker · Question: <topic>` or `Blocker · Permission needed: <action>` (`browser-gate` Content fields); never call it by its file number.
- `needs-analysis`: re-run the analyzer (understanding wrong) or the planner (plan wrong or missing something) with the blocker as input. Show the revised plan as a new round of Gate 2. Re-dispatch the blocked agent only after approval.
- `question`: a gate with the question as `questions` (options and recommended). No planner run. Append the answer to the end of plan.md and re-dispatch the blocked agent with it (implementer: with the plan path again).
- `permission`: a gate showing the exact action, why, the risk and the safer alternative, as one question with options approve / deny. Approve: record the permission in plan.md and re-dispatch. Deny: re-dispatch told to proceed without it, or to return what it can.
- Unclear type: treat it as `question`.
- Blocker gates never add or remove `## Approved:`. A `question` or `permission` re-dispatch of the implementer needs the existing `## Approved:` line; it is there, because the implementer only runs after Gate 2. Agents before Gate 2 (analyzer, planner) need no approval. A `needs-analysis` re-plan rewrites plan.md in full, which drops `## Approved:`; re-dispatch the implementer only after the new Gate 2 round is approved and the line is appended again.

## Escalation
If an agent returns an escalation flag (the task is bigger or deeper than its route assumed), tell the user and offer to switch route rather than continuing on the light path.