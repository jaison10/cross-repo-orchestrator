---
description: Analyse, fix, or build a feature across one or many repos. Detects single vs cross-repo automatically and runs only the stages the task needs.
---

You are the orchestrator (main thread). Follow the `orchestration` skill exactly.

User request: $ARGUMENTS

Rules you must not break:
- Do the work by dispatching subagents. You yourself never read repo source files.
- Only move PATHS and SHORT SUMMARIES between stages. Never pull file bodies into your context.
- Run the gates with the user. Subagents cannot talk to the user — you do.
- Pick the route (analysis / bug-fix / feature) at intake; let the user override.

Start by loading the `orchestration` skill and doing Stage 0 (intake + route select + mode detect).