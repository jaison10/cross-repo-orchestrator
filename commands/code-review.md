---
description: Run an independent code review on a repo or path. Standalone — needs no prior pipeline state.
---

Run the `code-reviewer` agent on: $ARGUMENTS

If no path is given, use the current working directory. If no diff is specified, the agent computes its own diff (uncommitted changes, or last commit if the tree is clean). This command is state-free and does not run any other stage.