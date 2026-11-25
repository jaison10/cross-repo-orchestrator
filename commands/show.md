---
description: Open a markdown report or plan in the browser as a readable page (diagrams, collapsible sections). No pipeline run.
argument-hint: "<path to a .md file>"
---

You are the main thread. Load the `browser-gate` skill and follow it.

File to show: $ARGUMENTS

1. If no path was given, ask for one. Check the file exists and is `.md`. If not, say so and stop.
2. Compute `repo-key` for the current folder as the `orchestration` skill defines it, and a task-slug `show-<file-stem>-<YYYY-MM-DD>`. GATE_DIR is `~/.orchestrator/<repo-key>/tasks/<task-slug>/gates/` (expand `~` yourself).
3. Read the file only to pass its text on. If it has no `## ` headings, put the whole text under one `## Report` heading. Do not rewrite or shorten it.
4. Always show it in the browser, whatever the config `ui` says. Always run `orchestrator-gate start` (it starts the server or confirms it is up; `orchestrator-gate` is the full quoted path from `browser-gate`, "The binary").
5. Write `gate-1.json` there (write `.tmp`, then rename) with `round: 1`, `view: "report"`, `title` `Report · <file name>`, `understanding` (the text). Use the browser-gate flow: `open`, then `wait`. Actions:
   - `approve` means Done: stop.
   - `console` ("Show in terminal instead"): print the file's text in the console.
   - `ask`: dispatch a read-only subagent given only the file path (no repo source). Put its short answer (per `orchestration` "Answering the user") in `thread` with `to`, raise `round` by 1, and `wait` again. browser-gate step 4's "task paths" wording does not apply here.
6. Only if `start` fails: print the file's text in the console instead and report its exact error line.
