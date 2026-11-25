---
name: jira-story
description: Generates a PM-style Jira story from an existing implementation plan and shows it in the browser with Jira copy buttons. User-invoked only, never run by the orchestrator.
disable-model-invocation: true
model: sonnet
---

# Jira story generator

Turn an existing plan into one Jira story written the way a PM would write it. This is a rewrite task: do not analyse code, do not re-plan.

Runs on the main thread. You write every file yourself; never hand writing to a subagent (subagents cannot write reports).

## Input
1. If the user gave a plan path, use it.
2. Otherwise use `plan.md` in the PARENT task folder: `<CENTRAL>/<parent-key>/tasks/<task-slug>/plan.md` (single-repo: the one key). If several tasks exist and none is specified, use the newest task folder. Optionally read `analysis-report.md` from the same folder, for Background only.
3. If no plan is found, say so and stop. Do not invent one.
4. The task folder is the plan's folder. If a user-given plan path is not inside `~/.orchestrator/<key>/tasks/<task-slug>/`, use `~/.orchestrator/<repo-key>/tasks/jira-<plan-stem>-<YYYY-MM-DD>/` instead (repo-key as the `orchestration` skill defines it) for both `jira/` and `gates/`.

## Template (fill every section; respect every limit)

```markdown
# <Title: verb-first outcome, max 80 chars>
Type: Story | Priority: <suggested> | Labels: <suggested> | Components: <repos>

## Summary
As a <persona>, I want <capability>, so that <benefit>.

## Background
<at most 2 lines: the problem and why now>

## Scope
- **In:** <deliverable>
- **Out:** <exclusion>

## Acceptance Criteria
- Given <context>, when <action>, then <observable result>

## Dependencies & Order
1. <step>

## Assumptions & Open Questions
- <item> (owner: <name or TBD>)

## Technical Notes
- <decision or risk>

## Sub-tasks
- **<repo>**: <task>
```

Limits:

| Section | Limit |
|---|---|
| Summary | Exactly 1 sentence. |
| Background | At most 2 lines. |
| Scope | 3-5 bullets total, In first then Out. At most one level of sub-points, at most 2 per bullet. |
| Acceptance Criteria | 3-5 plain `- ` bullets, never `- [ ]` (Jira Cloud turns those into action items). |
| Dependencies & Order | A numbered list of at most 4 steps, or the single line `None`. |
| Assumptions & Open Questions | At most 4. |
| Technical Notes | At most 3 bullets. Inline `code` allowed for names, no code blocks. |
| Sub-tasks | One line each, one per repo. |
| Every bullet | At most 20 words. Use a table only when it is shorter than the bullets would be. |

## Where each section comes from
- Summary, Background: the requirement and analysis report
- Scope In: plan's "What will change"
- Scope Out: plan's "What I'm NOT doing"
- Acceptance Criteria: "What will change" rewritten as testable outcomes
- Dependencies & Order: plan's "Order / dependencies"
- Assumptions & Open Questions: plan's open questions + analysis assumptions
- Technical Notes: plan's "Key decisions" and "Technical notes"
- Sub-tasks: the plan's per-repo sections

## Rules
- PM voice. The body is business-facing: no file paths, no code, no jargon. Technical detail lives only in Technical Notes.
- Nothing invented. If the plan does not say it, write "TBD". Never guess personas, priorities, or criteria the plan does not support. Priority and labels are suggestions, marked as such.
- Respect every limit above; cut, never summarise into longer lines.
- Keep the `## ` headings exactly as named, and use no other `## `: the page splits and copies on them.
- No story points. The team estimates.
- Cross-repo work is ONE story with one sub-task per repo, not one story per repo.
- Acceptance criteria must be observable behaviour, not implementation steps.

## Output
1. Write the story to `<task folder>/jira/STORY-<slug>-<YYYY-MM-DD>.md`. Create the folder if missing. Never overwrite an earlier story: if the name exists, add `-2`, `-3`, and so on.
2. Browser step. Load `browser-gate` and use its "The binary" and section 4.
   - Always run `orchestrator-gate start` and ignore the config `ui` (same as `/orch:show`). If `start` fails, go to step 3 and report its exact error line.
   - GATE_DIR = `<task folder>/gates/`. N = (highest number among the existing `gate-<n>.json` files there, ignoring `.state.json`) + 1, at least 5. If N would exceed 999, say so and stop.
   - Write `gate-N.json.tmp` with `round` 1, `title` "Report · Jira story: <Title>", `route` "jira-story", `view` "report", `copy` "jira" and `understanding` = the story text exactly as saved (JSON-escaped), then `mv` it over `gate-N.json`.
   - Run `orchestrator-gate open <gate-file>`, tell the user the URL, then `orchestrator-gate wait <gate-file> --round <round>` with `run_in_background`.
   - Act on the result:
     - `approve` (Done): stop.
     - `console`: print the story in chat.
     - `ask`: answer from the plan and story you already read (no code reading, no subagent), per `orchestration` "Answering the user". If the user asks for a change to the story, apply it within the template limits and overwrite only this run's STORY file and `understanding`. Add the reply to `thread` with `to`, raise `round` by 1, rewrite via `.tmp` + `mv` and `wait` again.
     - Exit code 2 (timeout): say the story is saved and the page stays at the URL while the server runs; stop.
   - In browser mode the chat shows only the story path, the URL and the TBD line.
3. Console fallback (`start` failed): show the story in chat.
4. End with one line listing any "TBD" items to fill in before pasting into Jira.
