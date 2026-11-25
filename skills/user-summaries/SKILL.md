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

## Report view (analysis route) — what the browser page renders
On the analysis route the report is the deliverable, so it is shown as a page, not read in a terminal. Write for a reader who skims: short, structured, diagrams over prose. The page splits the text on `## ` headings into collapsible sections, so use exactly these headings, in this order, and leave out any that do not apply:

- `## TL;DR` — 1 to 3 lines, at most 280 characters. The direct answer to the request.
- `## How it works` — one or two diagrams in ```mermaid fences. A `flowchart TD`/`LR` for control or data flow, a `sequenceDiagram` for calls between components or repos. At most about 10 nodes and 4 words per label. If a diagram would be bigger, split it. If the thing is a flat list, skip the diagram: no diagram beats a wrong one.
- `## Components` — a table `| Component | Path | Role |`, at most 8 rows, Role at most 10 words. Paths only, no code.
- `## Key findings` — 3 to 5 bullets, each at most 20 words. Tag the ones that need attention: `**[risk]**`, `**[gap]**`.
- `## Assumptions` — bullets. Still the most important block.
- `## Details` — optional, at most 15 lines. Hidden behind a "Show details" button. The full findings stay in `analysis-report.md`.

Mermaid rules (a diagram that fails to parse shows as raw source): only `flowchart` and `sequenceDiagram`; wrap any label containing punctuation in double quotes; no HTML in labels; no `click`, `href`, `style` or `classDef` lines; never put code or file bodies in labels.

Open questions are not part of the text: they go in the `questions` block below, and are optional in this view. Do not repeat any of this as a long summary; the page IS the summary.

## Questions block (analyzer and planner)
After the summary, end the return with the open questions as a fenced block labelled `questions` holding a JSON array. The orchestrator shows them as answerable cards in the browser gate, or numbered in the console.

    ```questions
    [{"id": "a1", "text": "Allow partial refunds?", "why": "Changes the schema.", "options": ["Full only", "Partial too"], "recommended": "Full only"}]
    ```

- `id`: unique and short. The analyzer uses `a1`, `a2`, ...; the planner uses `p1`, `p2`, ...
- `text`: the question, one sentence. `why`: one line on what it changes. Both required in spirit; `why` may be omitted.
- `options`: 2 to 4 short answers when the question has a closed set; leave it out for open questions. `recommended` must be one of `options`.
- Same bar as "Open questions" above: genuine blockers only. No questions, no block (write `[]`).
- Fewer, simpler questions per `orchestration` "Answering the user"; it also governs answers to the user's own questions.
- Keep the numbered "Open questions" list in the summary too; the block is the machine-readable twin. The orchestrator prints the numbered list only when the browser is unusable; in the browser it copies the block into the gate file and prints no questions.

## Plan summary (planner) — plain, human-readable
- What will change — 3–6 plain bullets.
- Key decisions — each as: decision + why (the trade-off).
- Technical notes — non-obvious only (migration, breaking change, new dependency).
- Order / dependencies — plain sequence, esp. cross-repo.
- What I'm NOT doing — scope boundaries (catches silent omissions).
- Open questions — numbered.
The full technical plan stays in the plan file; do not paste it here.