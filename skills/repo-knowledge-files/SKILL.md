---
name: repo-knowledge-files
description: Templates and placement rules for the per-repo knowledge files (CLAUDE.md, ARCHITECTURE.md, FEATURES.md) and the analysis cache metadata.
---

# Repo knowledge files

## CLAUDE.md — repo ROOT (auto-loaded every session, keep it lean)
What the repo is · how to build/run/test · key conventions · main entry points. Short. Detail belongs in the files below.

## ARCHITECTURE.md — <repo>/.claude/orchestrator/
Modules & layering · data flow · key abstractions · external dependencies.

## FEATURES.md — <repo>/.claude/orchestrator/
The capability map: what the repo does, feature by feature.

## .repo-analysis.json — <repo>/.claude/orchestrator/
`{ "tree_hash": "...", "updated_at": "...", "one_line": "..." }`. Analyzer skips re-analysis when `tree_hash` matches the current tree; discovery reads `one_line`.

Placement: only CLAUDE.md sits at the repo root (auto-load requires it). Everything else above lives under the repo's `.claude/orchestrator/` so the root stays clean.

## Central vs in-repo
Durable knowledge files (this skill's files) stay in-repo, one set per repo, never at the parent. `.repo-analysis.json` stays in-repo beside ARCHITECTURE/FEATURES because it validates their freshness.
Transient work products go to CENTRAL under a key + `tasks/<task-slug>/` (see orchestration skill): whole-feature artifacts under the parent-key, per-repo artifacts under each repo-key.
Cross-repo adds one in-repo file at the parent: `parent/.claude/orchestrator/WORKSPACE.md`, indexing the repos and their relationships.