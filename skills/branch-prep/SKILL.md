---
name: branch-prep
description: Prepares the git branch (and optional worktree) before bug-fix or feature work. Called by the orchestration skill; main thread only.
---

# Branch prep

Runs in the MAIN THREAD, because it asks the user questions and subagents cannot.
Never push, force-push, reset, delete or commit without an explicit ask from the user.
Never use `git pull`. Fetch, then branch from the remote ref.
All git commands must work in Git Bash on Windows: forward slashes, quoted paths, `git -C <repo>`.

## 1. Routes
- Analysis route: no branch. Run `git -C <repo> fetch <remote>`. If `git -C <repo> rev-list --count HEAD..<remote>/<base>` is above 0, warn that the checkout is behind the remote. Stop.
- Bug-fix and feature routes: run the full flow below, before analysis.

## 2. Config
User-level, `~/.orchestrator/config.json`:

    { "username": "jdsouza", "worktree-root": "C:/Users/jdsouza/Red/worktrees" }

The file is optional: it does not exist until something needs it, and the browser-gate tool has other keys with defaults (`ui`, `port`, `browser`, `idleMinutes`). Read the effective config with `orchestrator-gate config` (see `browser-gate`).

- Missing username: with the browser gate in use, run `orchestrator-gate setup` (see `browser-gate`, section "First run"); otherwise ask once in the console, then write it with `orchestrator-gate set username <value>`. Valid means git-ref-safe: no spaces, no `..`, no leading `-`. The tool refuses invalid names; re-ask.
- `worktree-root` is asked only when the user first picks the Worktree option. Save it with `orchestrator-gate set worktree-root <folder>`.

Team-level, a `## Branching` block in the repo's CLAUDE.md (tracked, team-owned):

    ## Branching
    - base: develop
    - remote: origin
    - branch: {username}/{type}/{slug}

- Missing block: ask once for base and remote, and offer to write it.
- Branch name: `{username}` from user config; `{type}` is `feat` (feature route) or `fix` (bug-fix route); `{slug}` is the task slug without the date (lowercase, kebab-case, max 40 chars).

## 3. Scan (read-only, every repo)
- Fetch the remote. If fetch fails, offer: continue from the local base with a warning, or stop.
- BLOCKED: a merge or rebase is in progress (the paths from `git rev-parse --git-path MERGE_HEAD`, `rebase-merge` or `rebase-apply` exist), or HEAD is detached (`git symbolic-ref -q HEAD` fails).
- RESUME: `git branch --show-current` equals the task branch.
- Branch already exists: not checked out anywhere, so ask whether to reuse it or add suffix `-2`. Checked out in another worktree (`git worktree list`): show that path and stop.
- DIRTY: `git status --porcelain` is non-empty. Applies on any branch, base branch included. Ignored files do not count. Otherwise CLEAN.
- On the base branch and `git log <remote>/<base>..<base> --oneline` is non-empty: ask whether to branch from the local base or the remote base.
- Risk flag for DIRTY repos: a file from `git status --porcelain` also appears in `git diff --name-only HEAD <remote>/<base>`.

## 4. Decide (one screen)
Show the new branch name, then one table: Repo | Current branch | Uncommitted | Action | Risk. Single-repo is one row.
- CLEAN: branch in place (automatic).
- RESUME: continue on the branch (automatic).
- BLOCKED: stop the whole run, change nothing in any repo, say what to resolve.
- DIRTY: list the changed files and ask the user to pick one:
  1. Keep changes: carry them into the new branch.
  2. Stash: save them, then branch clean.
  3. Worktree: leave them where they are, create the branch in a separate folder.
- Several dirty repos: ask once whether to apply one choice to all or choose per repo.
- Never decide for a DIRTY repo. If the user says "stop", abort everything.
- Cross-repo: this screen is merged with Gate 1. Scan the proposed repo set after discovery, then show repo-set confirmation and branch actions together.
- Browser gate: the table becomes the Gate 1 page. Put each repo in `repos[]` (`name`, `path`, `branch`, `dirty`, `dirtyFiles`, `risk`, the discovery reason) and read the user's choices from `state.repos[<id>].include` and `.dirtyChoice` (`keep`, `stash`, `worktree`). The rules above do not change: BLOCKED is reported in the console and stops the run, a DIRTY repo is never decided for the user, and nothing executes (section 5) before the page is approved.

## 5. Execute (only after confirmation)
START is `<remote>/<base>`, or the local base if the user chose that.
- CLEAN, or Keep: `git -C <repo> switch -c <branch> --no-track <START>`. If git aborts with "would be overwritten", nothing has changed. Then run `git -C <repo> stash push -u -m "orchestrator: <branch> <time>"`, then `switch -c`, then `git -C <repo> stash pop`. If the pop conflicts: stop, list the conflicted files, keep the stash entry, wait for the user.
- Stash: `git -C <repo> stash push -u -m "orchestrator: <branch> <time>"`, then `switch -c --no-track <branch> <START>`. Remember the stash name.
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