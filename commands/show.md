---
description: Open a markdown report or plan in the browser as a readable page (diagrams, collapsible sections). No pipeline run.
argument-hint: "<path to a .md file>"
---

You are the main thread. Load the `browser-gate` skill and follow it.

File to show: $ARGUMENTS

1. If no path was given, ask for one. Check the file exists and is `.md`. If not, say so and stop.
2. Compute `repo-key` for the current folder as the `orchestration` skill defines it, and a task-slug `show-<file-stem>-<YYYY-MM-DD>`. GATE_DIR is `~/.orchestrator/<repo-key>/tasks/<task-slug>/gates/` (expand `~` yourself).
3. Read the file only to pass its text on. If it has no `## ` headings, put the whole text under one `## Report` heading. Do not rewrite or shorten it.
4. Write `gate-1.json` there (write `.tmp`, then rename) with `round: 1`, `view: "report"`, `title` (the file name), `understanding` (the text). Use the browser-gate flow: `open`, then `wait`. `approve` means Done: stop. `ask` is answered by a short subagent that reads the file; add the answer to `thread` and raise `round`.
5. If the browser is not usable (see `browser-gate`, section 1), print the file's text in the console instead and say why once.
