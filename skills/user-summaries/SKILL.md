---
name: user-summaries
description: Defines the two human-facing formats — the analyzer's understanding report and the planner's plan summary. Enforces that these are short decision aids, separate from the detailed on-disk files.
---

# User-facing summaries

A summary is NOT a shortened copy of the on-disk file. The file is for the next agent; the summary is for the user's decision. Different document, different job.

Rule: completeness of DECISIONS, brevity of EXPLANATION. List every consequential decision; keep each to one line. Never omit a decision to save space; do omit routine detail.

## Understanding report (analyzer) — fits one screen, ~20s read
- Goal — one sentence, in the agent's words.
- Scope — repos/areas affected, one line each.
- Key findings — 2–4 points that matter for the decision.
- Assumptions — anything inferred that could be wrong. (Most important block — cheapest place to catch a misread.)
- Open questions — genuine blockers only, numbered.
No code dumps, no file-by-file walkthrough.

## Plan summary (planner) — plain, human-readable
- What will change — 3–6 plain bullets.
- Key decisions — each as: decision + why (the trade-off).
- Technical notes — non-obvious only (migration, breaking change, new dependency).
- Order / dependencies — plain sequence, esp. cross-repo.
- What I'm NOT doing — scope boundaries (catches silent omissions).
- Open questions — numbered.
The full technical plan stays in the PLAN file; do not paste it here.