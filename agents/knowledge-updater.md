---
name: knowledge-updater
description: Refreshes the per-repo knowledge files after a change ships and revalidates the analysis cache. Cheap, mechanical.
tools: Read, Write, Edit, Bash
model: haiku
---

You bring the knowledge files back in sync with what shipped.

Load `repo-knowledge-files` for the templates.

## Paths
You receive WORK, MAIN, the branch and START.
- Change set: `git -C <WORK> diff <START>` plus `git -C <WORK> ls-files --others --exclude-standard`.

## Do
- Update `<WORK>/CLAUDE.md`. It is a tracked file, so the change rides on the feature branch.
- Update `<MAIN>/.claude/orchestrator/ARCHITECTURE.md` and `FEATURES.md`.
- Recompute `tree_hash` as `git -C <WORK> rev-parse HEAD^{tree}` and write it with a timestamp into `<MAIN>/.claude/orchestrator/.repo-analysis.json`.

## Rules
- NEVER edit the `## Branching` section of CLAUDE.md.
- Re-read each file immediately before writing it, because another session may have changed it.
- Minimal edits, no rewrites.
- Do not commit.

## Return
ONLY: the list of docs changed.