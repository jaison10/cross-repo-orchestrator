---
name: repo-knowledge-files
description: Templates and placement rules for the per-repo knowledge files (CLAUDE.md, ARCHITECTURE.md, FEATURES.md).
---

# Repo knowledge files

## CLAUDE.md: WORK root (auto-loaded every session, keep it lean)
What the repo is (open with a one-line description, which discovery reads), how to build/run/test, key conventions, main entry points. Short. Detail belongs in the files below.

CLAUDE.md also carries the team's branching convention. Keep this block in it:

    ## Branching
    - base: develop
    - remote: origin
    - branch: {username}/{type}/{slug}

This block is team-owned and read by `branch-prep`. The knowledge-updater must never edit it. CLAUDE.md is tracked and committed; do not git-exclude it.

## ARCHITECTURE.md: <MAIN>/.claude/orchestrator/
Modules and layering, data flow, key abstractions, external dependencies.

## FEATURES.md: <MAIN>/.claude/orchestrator/
The capability map: what the repo does, feature by feature.

## Placement
- CLAUDE.md sits at the WORK root, because auto-load requires the repo root and it is tracked.
- Everything else lives under `<MAIN>/.claude/orchestrator/`, because git-excluded files do not exist in a worktree folder. Anchoring on MAIN keeps the cache alive after a worktree is removed.
- One set per repo, never at the parent.

## Central vs in-repo
Durable knowledge files (this skill's files) stay in-repo. Transient work products go to CENTRAL under a key and `tasks/<task-slug>/` (see the orchestration skill): whole-feature artifacts under the parent-key, per-repo artifacts under each repo-key.
Cross-repo adds one in-repo file at the parent: `<parent>/.claude/orchestrator/WORKSPACE.md`, indexing the repos and their relationships.