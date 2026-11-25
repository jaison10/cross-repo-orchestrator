---
name: branch-prep
description: Prepares the git branch (and optional worktree) before bug-fix or feature work. Called by the orchestration skill; main thread only.
---

# Branch prep

Runs in the MAIN THREAD, because it asks the user questions and subagents cannot.
Never push, force-push, reset, delete or commit without an explicit ask from the user.
Never use `git pull`. Fetch, then branch from the remote ref.
All git commands must work in Git Bash on Windows: forward slashes, quoted paths, `git -C <repo>`.
Questions: when GATE_UI=browser, every question in sections 2 to 4 goes on the Gate 1 page (section 4); none is asked in the console. Only stop notices (BLOCKED, task branch checked out in another worktree) are printed in the console. When GATE_UI=console, ask them in the console. Questions in section 5 come after Gate 1: when GATE_UI=browser use a blocker gate (`gate-5.json` up, see `browser-gate` section 3), else the console.

## 1. Routes
- Analysis route: no branch and no questions. Use the `## Branching` block's base and remote, or the detected defaults (section 2). Run `git -C <repo> fetch <remote>`. If `git -C <repo> rev-list --count HEAD..<remote>/<base>` is above 0, warn in one console line that the checkout is behind the remote. If no base was detected or the fetch failed, say in one line that the behind-check was skipped and why. Stop.
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
- Branch name: `{username}` from user config; `{type}` is `feat` (feature route) or `fix` (bug-fix route); `{slug}` is the task slug without the date (lowercase, kebab-case, max 40 chars).

## 3. Scan (read-only, every repo)
1. Stop checks first. BLOCKED: a merge or rebase is in progress (the paths from `git rev-parse --git-path MERGE_HEAD`, `rebase-merge` or `rebase-apply` exist), or HEAD is detached (`git symbolic-ref -q HEAD` fails). Task branch checked out in another worktree (`git worktree list`). If any repo hits either, print which repo and what to resolve in the console and stop the run. Write no gate file.
2. Fetch the remote. If fetch fails, record it; Gate 1 asks `fetch` (continue from the local base with a warning, or stop).
3. RESUME: `git branch --show-current` equals the task branch.
4. Branch already exists and is not checked out anywhere: Gate 1 asks `reuse` (reuse it, or the first free suffix `-2`, `-3`, ...).
5. DIRTY: `git status --porcelain` is non-empty. Applies on any branch, base branch included. Ignored files do not count. Otherwise CLEAN. DIRTY and the Keep/Stash/Worktree choice do not depend on the base.
6. Only when the base is known: on the base branch and `git log <remote>/<base>..<base> --oneline` is non-empty: Gate 1 asks `start` (local or remote base).
7. Risk flag for DIRTY repos: a file from `git status --porcelain` also appears in `git diff --name-only HEAD <START>` (START as in section 5; default `<remote>/<base>`). Base not known: `unknown: base not confirmed`. Fetch failed: use the existing `<remote>/<base>` ref and add `(remote not fetched)`, or `unknown: <remote> not fetched` if there is none.

## 4. Decide (one screen)
If GATE_UI=browser, build `gate-1.json` and do not use AskUserQuestion or a console table. Put each repo in `repos[]` (`id`: the repo folder name, unique in the run; `name`, `path`, `branch`, `dirty`, `dirtyFiles`, `risk`, `reason`: the discovery reason). Put every open question from sections 2 and 3 in `questions`, id `g-<kind>-<repo-id>`:

| kind | asked when | options | recommended |
|---|---|---|---|
| `base` | no `## Branching` block | detected base plus other of `main`/`master`/`develop` that exist as `<remote>/<name>`; free text allowed | detected base (omit if none) |
| `remote` | no block and `git remote` lists 2+ remotes, or the detected remote is not `origin` | names from `git remote` | `origin`, else the only remote |
| `saveblock` | no block | `Add to CLAUDE.md`, `Don't save` | `Add to CLAUDE.md` |
| `fetch` | `git fetch <remote>` failed | `Continue from local base`, `Stop the run` | `Continue from local base` |
| `reuse` | task branch exists, not checked out anywhere | `Reuse <branch>`, `New branch <branch>-<n>` (`<n>` = first free suffix from 2) | `New branch <branch>-<n>` |
| `start` | on the base branch and `git log <remote>/<base>..<base>` non-empty | `Remote <remote>/<base>`, `Local <base> (+N unpushed commits)` | `Remote <remote>/<base>` |

Read answers from `state.answers[<id>]` (`choice`, or the trimmed `text` if `choice` is empty) and repo choices from `state.repos[<id>].include` and `.dirtyChoice` (`keep`, `stash`, `worktree`). The rules below do not change: BLOCKED is reported in the console and stops the run, a DIRTY repo is never decided for the user, and nothing executes (section 5) before the page is approved.

After approve:
1. A `fetch` answer "Stop the run": stop, change nothing.
2. For each repo whose START changed from what was scanned (a `base` or `remote` answer differing from the detected default, `start` = local, `fetch` = continue): check the remote is listed and the base exists, then re-run only section 3 steps 2, 6 and 7. Keep every `include` and `dirtyChoice`.
3. If that adds a question (`start`, `fetch`, or an invalid `base`/`remote` re-asked with id suffix `-r<round>`) or changes a DIRTY repo's risk, rewrite `gate-1.json` with `round + 1`: all questions kept, new ones added, updated `risk`, and a one-line `thread` note (`from: "claude"`, no `to`) on what changed. `wait` again and repeat from 1.
4. Otherwise go to section 5. With a `## Branching` block nothing changes START, so Gate 1 is one round.

`revise`: re-run the scan (cross-repo: discovery too) with the answers and concerns, then a new round. A concern asking to stop aborts everything.

Console fallback (GATE_UI=console): show the new branch name, then one table: Repo | Current branch | Uncommitted | Action | Risk. Single-repo is one row. In console mode, PRINT the table as text: every in-scope repo, clean ones included, with branch, uncommitted state and proposed action (cross-repo: the discovery repo set). With a `## Branching` block, print it before asking anything; without one, first ask `base`, `remote`, `saveblock` via AskUserQuestion (the scan needs them), then fetch, scan and print it; then ask `fetch`, `reuse`, `start`, repo-set and Keep/Stash/Worktree questions. Asking base first means the console needs no second round.

Rules for both forms:
- CLEAN: branch in place (automatic).
- RESUME: continue on the branch (automatic).
- BLOCKED: stop the whole run, change nothing in any repo, say what to resolve.
- DIRTY: list the changed files and ask the user to pick one:
  1. Keep changes: carry them into the new branch.
  2. Stash: save them, then branch clean.
  3. Worktree: leave them where they are, create the branch in a separate folder.
- Console only: several dirty repos: ask once whether to apply one choice to all or choose per repo (the page has a choice per repo).
- Never decide for a DIRTY repo. If the user says "stop", abort everything.
- Cross-repo: this screen is merged with Gate 1. Scan the proposed repo set after discovery, then show repo-set confirmation and branch actions together.

## 5. Execute (only after confirmation)
START is `<remote>/<base>`, or the local base if the user chose it (`start` = local, or `fetch` = continue).
- CLEAN, or Keep: `git -C <repo> switch -c <branch> --no-track <START>`. If git aborts with "would be overwritten", nothing has changed. Then run `git -C <repo> stash push -u -m "orchestrator: <branch> <time>"`, then `switch -c`, then `git -C <repo> stash pop`. If the pop conflicts: stop, list the conflicted files, keep the stash entry, wait for the user.
- Stash: `git -C <repo> stash push -u -m "orchestrator: <branch> <time>"`, then `git -C <repo> switch -c <branch> --no-track <START>`. Remember the stash name.
- Reuse (`reuse` answer): use `git -C <repo> switch <branch>` instead of `switch -c ... <START>` (Worktree: `git -C <repo> worktree add <WORKPATH> <branch>`); the Keep and Stash handling is the same. START is `git -C <repo> merge-base <branch> <START>`.
- `saveblock` = add: once the branch exists, append the `## Branching` block (the answered base and remote, `branch: {username}/{type}/{slug}`) to CLAUDE.md at the WORK root, so it rides on the feature branch. This runs before the knowledge-updater, so the two never write it at once.
- Worktree: WORKPATH is `<worktree-root>/<repo-folder>-<slug>`. If it already exists, ask. Create `<worktree-root>` if missing. Run `git -C <repo> worktree add --no-track -b <branch> <WORKPATH> <START>`. The original folder stays untouched. Tell the user the new folder lacks ignored files (node_modules, bin/obj, .env), so they must restore or build there. If `<worktree-root>` is outside the folder Claude was launched from, tell the user to run `/add-dir <WORKPATH>` or start a session there.
- RESUME: nothing to do.
- Failure partway through a cross-repo run: stop, show which repos are done and which are not, and offer to undo the finished ones (switch back, delete the new empty branch, `git worktree remove`, restore the stash). Do each undo only with the user's agreement.

## 6. Return, per repo
- WORK: the folder where code is read and edited (the worktree folder if one was created, else the repo folder).
- MAIN: the main repo folder, `(cd <WORK> && cd "$(git rev-parse --git-common-dir)/.." && pwd)`.
- The branch name and START.

## 7. End of run
- Remind the user about any stash created, by name.
- If the user abandons before implementation, offer to delete the empty branch or worktree.
- After the work merges, remind the user to run `git worktree remove <path>` for worktree runs.