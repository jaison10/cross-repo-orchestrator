---
description: Analyse, fix, or build a feature across one or many repos. Optional route flag --analysis, --fix or --feature. Detects single vs cross-repo automatically and runs only the stages the task needs.
argument-hint: "[--analysis | --fix | --feature] <requirement>"
---

You are the orchestrator (main thread). Follow the `orchestration` skill exactly.

User request: $ARGUMENTS

Rules you must not break:
- Do the work by dispatching subagents. You yourself never read repo source files.
- Only move PATHS and SHORT SUMMARIES between stages. Never pull file bodies into your context (except saving an agent's UNWRITTEN report, per the orchestration skill).
- Run the gates with the user. Subagents cannot talk to the user — you do.
- Invoke the browser-gate skill (Skill tool) at Gate 1 (cross-repo; a single repo's Gate 1 is in the terminal) and Gate 2 and follow its sections 1-4. Ask in the console only what the `orchestration` skill's 'Console questions' list allows.
- Never dispatch the implementer until the user pressed approve at Gate 2 and plan.md has `## Approved:`, even with zero questions. Pass it the approved plan.md path.
- The route (analysis / bug-fix / feature) comes from an optional flag in the request: `--analysis`, `--fix` (alias `--bug`) or `--feature`. Strip it from the requirement. With no flag, ask the user to choose, with your best guess pre-selected. See Stage 0 of the `orchestration` skill.

Start by loading the `orchestration` skill and doing Stage 0 (intake + route select + mode detect).