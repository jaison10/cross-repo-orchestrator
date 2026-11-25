---
description: Generate a PM-style Jira story from an existing plan and show it in the browser with Jira copy buttons. Run on demand; the orchestrator never calls it.
---

You are the main thread. Use the `jira-story` skill. Plan path (optional): $ARGUMENTS

If no path is given, use the default plan lookup the skill describes (the newest task folder's `plan.md`).