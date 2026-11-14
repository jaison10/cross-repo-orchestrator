---
name: jira-story
description: Generates a PM-style Jira story from an existing implementation plan. User-invoked only, never run by the orchestrator.
disable-model-invocation: true
model: sonnet
---

# Jira story generator

Turn an existing plan into one Jira story written the way a PM would write it. This is a rewrite task: do not analyse code, do not re-plan.

## Input
1. If the user gave a plan path, use it.
2. Otherwise use the newest file in `plans/`: the parent folder's `.claude/orchestrator/plans/` for cross-repo, or the current repo's `.claude/orchestrator/plans/` for single-repo.
3. Optionally read `analysis-report.md` from the same `.claude/orchestrator/` folder, for Background only.
4. If no plan is found, say so and stop. Do not invent one.

## Template (fill every section; one page total)

Title: <verb-first, outcome-focused, max 80 chars>
Type: Story | Priority: <suggested> | Labels: <suggested> | Components: <repos>

**Summary**
As a <user/persona>, I want <capability>, so that <benefit>.

**Background**
2-3 lines: the problem and why it matters now.

**Scope**
- In: what this story delivers
- Out: explicit exclusions

**Acceptance Criteria**
- [ ] Given <context>, when <action>, then <observable result>
(3-7 items, each testable by QA)

**Dependencies & Order**
Plain sequence, e.g. "API first, then client".

**Assumptions & Open Questions**
- Each item, with an owner if known.

**Technical Notes (for devs)**
- 3-5 lines: key decisions and non-obvious risks. No code.

**Suggested Sub-tasks**
- [repo] <task> (one per repo)

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
- One page. Cut routine detail; keep every consequential decision as one line.
- No story points. The team estimates.
- Cross-repo work is ONE story with one sub-task per repo, not one story per repo.
- Acceptance criteria must be observable behaviour, not implementation steps.

## Output
1. Write to `.claude/orchestrator/jira/STORY-<slug>-<YYYY-MM-DD>.md` (same parent/repo rule as the plan). Create the folder if missing. Never overwrite an existing story file.
2. Show the story in chat.
3. End with one line listing any "TBD" items the user should fill in before pasting into Jira.

Plain Markdown output, so it pastes into Jira's editor.