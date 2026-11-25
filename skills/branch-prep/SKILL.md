---
name: branch-prep
description: Prepares the git branch (and optional worktree) before bug-fix or feature work. Called by the orchestration skill; main thread only.
---

# Branch prep

Runs in the MAIN THREAD, because it asks the user questions and subagents cannot.
Never push, force-push, reset, delete or commit without an explicit ask from the user.
Never use `git pull`. Fetch, then branch from the remote ref.
All git commands must work in Git Bash on Windows: forward slashes, quoted paths, `git -C <repo>`.
Questions: where they are asked is defined in section 4, 'Where Gate 1 is asked'. Single repo: always the terminal, one at a time. Cross-repo with the browser: sections 2 to 4 on the Gate 1 page, section 5 on a blocker gate (`gate-5.json` up, `browser-gate` section 3). GATE_UI=console: the terminal.

## 1. Routes
- Analysis route: no branch and no questions. Single repo: print one line `Gate 1: <repo>, analysis, no branch` and continue; no page. Cross-repo: the Gate 1 page confirms the repo set. Use the `## Branching` block's base and remote, or the detected defaults (section 2). Run `git -C <repo> fetch <remote>`. If `git -C <repo> rev-list --count HEAD..<remote>/<base>` is above 0, warn in one console line that the checkout is behind the remote. If no base was detected or the fetch failed, say in one line that the behind-check was skipped and why. Stop.
- Bug-fix and feature routes: run the full flow below, before analysis.

## 2. Config
User-level, `~/.orchestrator/config.json`:

    { "username": "jdsouza", "worktree-root": "C:/Users/jdsouza/Red/worktrees" }

The file is optional: it does not exist until something needs it, and the browser-gate tool has other keys with defaults (`ui`, `port`, `browser`, `idleMinutes`). Read the effective config with `orchestrator-gate config` (see `browser-gate`). Here `orchestrator-gate` is the full quoted path defined in `browser-gate`, "The binary".

- Missing username: when GATE_UI=browser, run `orchestrator-gate setup` (see `browser-gate`, section "First run"); when GATE_UI=console, ask once in the console, then write it with `orchestrator-gate set username <value>`. Valid means git-ref-safe: no spaces, no `..`, no leading `-`. The tool refuses invalid names; re-ask. Do this before the scan: the branch name needs it.
- `worktree-root` is asked only when the user first picks the Worktree option. Save it with `orchestrator-gate set worktree-root <folder>`.

Team-level, a `## Branching` block in the repo's CLAUDE.md (tracked, team-owned):

    ## Branching
    - base: develop
    - remote: origin
    - branch: {username}/{type}/{slug}

- Missing block: detect defaults and ask on Gate 1, not now. Remote: `origin` if `git -C <repo> remote` lists it, else the only remote, else none. Base: `git -C <repo> symbolic-ref --short refs/remotes/<remote>/HEAD` without the `<remote>/` prefix; if that fails, the first of `main`, `master`, `develop` that exists as `<remote>/<name>`; else none. Scan with these (section 3); Gate 1 asks `base`, `remote` and `saveblock` (section 4). No remote at all: START is the local base and there is no `remote` or `fetch` question.
- Branch name: `{username}` from user config; `{type}` is `feat` (feature route) or `fix` (bug-fix route); `{slug}` is the task slug without the date and without any `-N` suffix (lowercase, kebab-case, max 40 chars).

## 3. Scan (read-only, every repo)
1. Stop checks first. BLOCKED: a merge or rebase is in progress (the paths from `git rev-parse --git-path MERGE_HEAD`, `rebase-merge` or `rebase-apply` exist), or HEAD is detached (`git symbolic-ref -q HEAD` fails). If `git -C <repo> worktree list --porcelain` shows the task branch checked out at a path other than the repo folder, the repo is RESUME with WORK = that path (step 3): run the BLOCKED checks there too, and steps 2 to 7 with `git -C <that path>`. If any repo is BLOCKED, print which repo and what to resolve in the console and stop the run. Write no gate file.
2. Fetch the remote. If fetch fails, record it; Gate 1 asks `fetch` (continue from the local base with a warning, or stop).
3. RESUME: the task branch is checked out in the repo folder (`git branch --show-current` equals it; WORK = the repo folder) or in another worktree (step 1; WORK = that path). RESUME: skip steps 4, 6 and 7; step 5 still records the dirty file list (for PRE-EXISTING and the one-line note). No `## Branching` block: use the detected defaults (section 2), as on the analysis route. What RESUME asks and runs: section 4 rules and section 5. Not RESUME: record `<current>` (`git -C <repo> branch --show-current`), whether it is the base or its upstream (`git -C <repo> rev-parse --abbrev-ref <current>@{upstream}`) is `<remote>/<base>` (only when the base is known), and its behind count against its upstream (`git -C <repo> rev-list --count HEAD..@{upstream}`; only when it has one and the fetch succeeded). Read-only; used by the `g-branch` question (section 4).
4. Branch already exists and is not checked out anywhere: Gate 1 asks `reuse` (reuse it, or the first free suffix `-2`, `-3`, ...).
5. DIRTY: `git status --porcelain` is non-empty. Applies on any branch, base branch included. Ignored files do not count. Otherwise CLEAN. DIRTY and the Keep/Stash/Worktree choice do not depend on the base.
6. Only when the base is known: on the base branch and `git log <remote>/<base>..<base> --oneline` is non-empty: Gate 1 asks `start` (local or remote base).
7. Risk flag for DIRTY repos: a file from `git status --porcelain` also appears in `git diff --name-only HEAD <START>` (START as in section 5; default `<remote>/<base>`). Base not known: `unknown: base not confirmed`. Fetch failed: use the existing `<remote>/<base>` ref and add `(remote not fetched)`, or `unknown: <remote> not fetched` if there is none.

## 4. Decide (one screen)
Where Gate 1 is asked:
- Cross-repo with GATE_UI=browser: the `gate-1.json` page, below.
- Single repo (any GATE_UI), and cross-repo with GATE_UI=console: the terminal, one question at a time (AskUserQuestion, recommended first), with free-text triage per `orchestration` 'Free text on a question' step 7. See 'Terminal form' below.
- Single repo on the analysis route (section 1), or a single repo that is RESUME: no questions. Print one line (section 1, or the RESUME line of section 5) and continue.
- Section 5 execute-time questions (Keep overwrite, worktree folder exists): the terminal for a single repo or GATE_UI=console; a blocker gate for cross-repo with the browser.

Single repo, terminal order:
1. If DIRTY and not RESUME, print `<repo> has N uncommitted files:` followed by the file list.
2. No `## Branching` block: ask `base`, `remote` (only when it applies), then `saveblock`.
3. Run fetch and the scan. If the fetch failed, ask `fetch`.
4. Ask `New branch <branch> or Stay on <current>?`, with the `branch` warnings as the description.
5. Only on New branch: `reuse` and `start` if they apply; then, if DIRTY, `Keep, Stash or Worktree?`; Worktree with no `worktree-root` set: ask for the folder.

Cross-repo with GATE_UI=browser: build `gate-1.json` (`title` per `browser-gate` Content fields: `Gate 1 · Confirm repos and branch`) and do not use AskUserQuestion or a console table. Put each repo in `repos[]` (`id`: the repo folder name, unique in the run; `name`, `path`, `branch`, `dirty`, `dirtyFiles`, `risk`, `reason`: the discovery reason). RESUME repos go in `repos[]` with `dirty: false` and `reason` ending `resuming <branch>, N uncommitted files kept`, so the page asks no Keep/Stash/Worktree for them. Put every open question from sections 2 and 3 in `questions`, id `g-<kind>-<repo-id>`:

| kind | asked when | options | recommended |
|---|---|---|---|
| `base` | no `## Branching` block | detected base plus other of `main`/`master`/`develop` that exist as `<remote>/<name>`; free text allowed | detected base (omit if none) |
| `remote` | no block and `git remote` lists 2+ remotes, or the detected remote is not `origin` | names from `git remote` | `origin`, else the only remote |
| `saveblock` | no block | `Add to CLAUDE.md`, `Don't save` | `Add to CLAUDE.md` |
| `fetch` | `git fetch <remote>` failed | `Continue from local base`, `Stop the run` | `Continue from local base` |
| `reuse` | task branch exists, not checked out anywhere | `Reuse <branch>`, `New branch <branch>-<n>` (`<n>` = first free suffix from 2) | `New branch <branch>-<n>` |
| `start` | on the base branch and `git log <remote>/<base>..<base>` non-empty | `Remote <remote>/<base>`, `Local <base> (+N unpushed commits)` | `Remote <remote>/<base>` |
| `branch` | bug-fix/feature, repo not RESUME | `New branch <branch>`, `Stay on <current>` | `New branch <branch>` |

`branch` question: text `<repo-name>: work on a new branch or stay on <current>?`; `<branch>` is the task branch name (with a `reuse` question, the plain `<branch>`; the `reuse` answer still decides the final name). Its `why` is `New branch: made from <remote>/<base>, your uncommitted changes come along. Stay: no switch, nothing created, changes stay where they are.` (base unknown: `made from the base branch`), plus each warning that applies, one short sentence each: current is the base: `Warning: <current> is the base branch; staying puts the work directly on it (nothing is committed or pushed).`; current tracks the base (and is not the base): `Warning: <current> tracks <remote>/<base>; staying puts the work on a branch that follows the base.`; behind its upstream (N > 0): `<current> is N commits behind <upstream>; Claude does not pull.` (no upstream or fetch failed: none); DIRTY, browser page only: `If you stay, the Keep/Stash/Worktree choice is ignored.` (the terminal asks Keep/Stash/Worktree only after New branch, so it omits this).

Read answers only for the current round's question ids; a refined id `g-<kind>-<repo-id>-r<N>` counts as `g-<kind>-<repo-id>` (match the repo id against `repos[]`). Use `choice`, or the trimmed `text` only when triage (`orchestration` 'Free text on a question') marks it ANSWER; an implied option counts as that option. For `g-branch` only `choice` counts: text without a choice is refined, never read as NEW-BRANCH. Read repo choices from `state.repos[<id>].include` and `.dirtyChoice` (`keep`, `stash`, `worktree`). A `g-branch` choice starting `Stay on ` makes the repo SAME-BRANCH, otherwise NEW-BRANCH; for a SAME-BRANCH repo ignore its `dirtyChoice` and its `reuse`/`start` answers. The rules below do not change: BLOCKED is reported in the console and stops the run, a DIRTY repo is never decided for the user, and nothing executes (section 5) before the page is approved.

After approve:
0. Triage first; on any CLARIFY, the orchestrator refines those `g-*` questions itself from the scan (text, `why` with an example such as `Example: New branch moves your edits to <branch>; Stay keeps them on <current>.`), and writes round + 1 with the thread note. Nothing in steps 1-4 or section 5 runs.
1. A `fetch` answer "Stop the run": stop, change nothing.
2. For each repo whose START changed from what was scanned (a `base` or `remote` answer differing from the detected default, `start` = local, `fetch` = continue): check the remote is listed and the base exists, then re-run only section 3 steps 2, 6 and 7. Keep every `include` and `dirtyChoice`. Re-scan only NEW-BRANCH repos; never add `start`/`reuse` questions for SAME-BRANCH repos. If a SAME-BRANCH repo now is or tracks the changed base, add the base warning as a one-line `thread` note only if a new round happens anyway; Execute prints it regardless.
3. If that adds a question (`start`, `fetch`, or an invalid `base`/`remote` re-asked with id suffix `-r<round>`, the same scheme as a refined question) or changes a DIRTY repo's risk, rewrite `gate-1.json` with `round + 1`: all questions kept (a refined one replaces its old one), new ones added, updated `risk`, and a one-line `thread` note (`from: "claude"`, no `to`) on what changed. `wait` again and repeat from 1.
4. Otherwise go to section 5. With a `## Branching` block nothing changes START, so Gate 1 is one round.

`revise`: triage too; re-run the scan (cross-repo: discovery too) with the answers and concerns, then a new round, refining CLARIFY questions in that same round. A concern asking to stop aborts everything.

Terminal form (single repo always; cross-repo when GATE_UI=console). Single repo: no table; ask in the order above ('Single repo, terminal order'). RESUME repos are skipped with their one line (section 5). Cross-repo console: show the new branch name, then one table: Repo | Current branch | Uncommitted | Action | Risk. PRINT the table as text: every in-scope repo, clean ones included, with branch, uncommitted state and proposed action (cross-repo: the discovery repo set). With a `## Branching` block, print it before asking anything; without one, first ask `base`, `remote`, `saveblock` via AskUserQuestion (the scan needs them), then fetch, scan and print it. A reply that triages CLARIFY ('Free text on a question') is reworded and re-asked immediately. Then ask per repo (one AskUserQuestion at a time, recommended first) "<repo>: new branch <branch> (Recommended) or stay on <current>?", with the `branch` warnings in the description; several repos: first ask once "Same choice for all repos, or per repo?". The Action column then shows `new branch <branch>` or `stay on <current>`. Then ask `fetch`, `reuse`, `start` and Keep/Stash/Worktree ONLY for NEW-BRANCH repos (the console skips moot questions; the page cannot), and the repo-set questions. Asking base first means the console needs no second round.

Rules for both forms:
- CLEAN: ask `g-branch`; a new branch is created in place.
- RESUME (section 3 step 3): continue on the task branch (in its worktree if it is checked out there); no `g-branch`, no Keep/Stash/Worktree even when DIRTY; uncommitted changes stay, as SAME-BRANCH.
- SAME-BRANCH: no switch, no stash, no worktree; uncommitted changes stay. Allowed on the base branch with the warning.
- BLOCKED: stop the whole run, change nothing in any repo, say what to resolve.
- DIRTY: list the changed files and, when the repo goes to a new branch (never on SAME-BRANCH or RESUME), ask the user to pick one:
  1. Keep changes: carry them into the new branch.
  2. Stash: save them, then branch clean.
  3. Worktree: leave them where they are, create the branch in a separate folder.
- Console only: several dirty repos: ask once whether to apply one choice to all or choose per repo (the page has a choice per repo).
- Never decide for a DIRTY repo. If the user says "stop", abort everything.
- Cross-repo: this screen is merged with Gate 1. Scan the proposed repo set after discovery, then show repo-set confirmation and branch actions together. One `g-branch` per repo; mixed answers allowed.

## 5. Execute (only after confirmation)
START is `<remote>/<base>`, or the local base if the user chose it (`start` = local, or `fetch` = continue).
- DIRTY repo on SAME-BRANCH, RESUME or Keep: first record PRE-EXISTING, the file paths (paths only) from `git -C <repo> status --porcelain` (RESUME: `git -C <WORK>`): the files the user had changed or left untracked before this run.
- SAME-BRANCH: run nothing except `git -C <repo> rev-parse HEAD` (START, stored as the full SHA). Print one console line: `<repo>: staying on <current>, START <short sha>`, plus the base/tracks warning if it applies. No fetch beyond the scan, no pull. `saveblock` = add still applies (CLAUDE.md at the WORK root, on the current branch).
- CLEAN, or Keep: `git -C <repo> switch -c <branch> --no-track <START>`. If git aborts with "would be overwritten", nothing has changed. Stop that repo and ask `<repo>: switching would overwrite your uncommitted files. Stash or Worktree?` Options `Stash`, `Worktree`, no recommended answer, because a DIRTY repo is never decided for the user. Ask it where section 5 asks (section 4, 'Where Gate 1 is asked'). Then run the chosen path below and drop that repo's PRE-EXISTING list. No automatic stash or pop.
- Stash: `git -C <repo> stash push -u -m "orchestrator: <branch> <time>"`, then `git -C <repo> switch -c <branch> --no-track <START>`. Remember the stash name.
- Reuse (`reuse` answer): use `git -C <repo> switch <branch>` instead of `switch -c ... <START>` (Worktree: `git -C <repo> worktree add <WORKPATH> <branch>`); the Keep and Stash handling is the same. START is `git -C <repo> merge-base <branch> <START>`.
- `saveblock` = add: once the branch exists, append the `## Branching` block (the answered base and remote, `branch: {username}/{type}/{slug}`) to CLAUDE.md at the WORK root, so it rides on the feature branch. This runs before the knowledge-updater, so the two never write it at once.
- Worktree: WORKPATH is `<worktree-root>/<repo-folder>-<slug>`. If it already exists, ask (where section 5 asks, section 4 'Where Gate 1 is asked'). Create `<worktree-root>` if missing. Run `git -C <repo> worktree add --no-track -b <branch> <WORKPATH> <START>`. The original folder stays untouched. Tell the user the new folder lacks ignored files (node_modules, bin/obj, .env), so they must restore or build there. If `<worktree-root>` is outside the folder Claude was launched from, tell the user to run `/add-dir <WORKPATH>` or start a session there.
- RESUME: no switch. START = `git -C <WORK> merge-base HEAD <remote>/<base>`, stored as the full SHA; use the local `<base>` when there is no remote or the fetch failed. Print one console line: `<repo>: resuming <branch>[ in <WORK>], START <short sha>[, N uncommitted files kept]`. If WORK is outside the folder Claude was launched from, add one line telling the user to run `/add-dir <WORK>`.
- Failure partway through a cross-repo run: stop, show which repos are done and which are not, and offer to undo the finished ones (switch back, delete the new empty branch, `git worktree remove`, restore the stash). Do each undo only with the user's agreement. SAME-BRANCH repos have nothing to undo.

## 6. Return, per repo
- WORK: the folder where code is read and edited (the worktree folder if one was created, else the repo folder).
- MAIN: the main repo folder, `(cd <WORK> && cd "$(git rev-parse --git-common-dir)/.." && pwd)`.
- The branch name and START.
- PRE-EXISTING, when recorded (section 5).
- SAME-BRANCH: WORK = repo folder, branch = `<current>`, START = the HEAD SHA from Execute.
- RESUME: WORK = the repo folder, or the worktree path where the task branch is checked out; branch = the task branch; START = the merge-base from section 5.

## 7. End of run
- Remind the user about any stash created, by name.
- SAME-BRANCH: remind that the changes are uncommitted on `<current>`; if `<current>` is or tracks the base, add: to review via a PR, create a branch first: `git switch -c <name>` (carries the changes).
- If the user abandons before implementation, offer to delete the empty branch or worktree. SAME-BRANCH: nothing to delete; `git diff <START>` shows what changed. Never offer to discard.
- After the work merges, remind the user to run `git worktree remove <path>` for worktree runs.