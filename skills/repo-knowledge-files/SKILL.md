---
name: repo-knowledge-files
description: Templates and placement rules for the per-repo knowledge files (CLAUDE.md, ARCHITECTURE.md, FEATURES.md) and the analysis cache metadata.
---

# Repo knowledge files

## CLAUDE.md — repo ROOT (auto-loaded every session, keep it lean)
What the repo is · how to build/run/test · key conventions · main entry points. Short. Detail belongs in the files below.

## ARCHITECTURE.md — ORCH/
Modules & layering · data flow · key abstractions · external dependencies.

## FEATURES.md — ORCH/
The capability map: what the repo does, feature by feature.

## .repo-analysis.json — ORCH/
`{ "tree_hash": "...", "updated_at": "...", "one_line": "..." }`. Analyzer skips re-analysis when `tree_hash` matches the current tree; discovery reads `one_line`.

Placement: only CLAUDE.md sits at the repo root (auto-load requires it). Everything else lives under ORCH so the root stays clean.