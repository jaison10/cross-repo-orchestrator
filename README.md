# cross-repo-orchestrator

A Claude Code plugin that analyses, fixes and builds features across one or many repos. It detects single vs cross-repo, picks only the stages the task needs, prepares the git branch, and stops at gates where you decide. Gates 1 and 2 can be answered in a **browser page** instead of the console.

## Install

In Claude Code:

```
/plugin marketplace add jaison10/cross-repo-orchestrator
/plugin install repo-orchestrator@repo-orchestrator-marketplace
/reload-plugins
```

Nothing else to install. The browser page is served by a small binary that ships inside the plugin (Windows x64, macOS Apple silicon, Linux x64 including WSL) and carries its own runtime.

## Use

```
/repo-orchestrator:orchestrate <what you want>
/repo-orchestrator:code-review <repo or path>
/repo-orchestrator:jira-story
```

The orchestrator picks a route (analysis, bug-fix or feature) and lets you override it.

## Browser gates

At Gate 1 (repos and branches) and Gate 2 (plan) the orchestrator opens a local page showing:

- what Claude understood, with its assumptions
- the effective repos, why each is in scope, and what to do with uncommitted changes (keep, stash, worktree)
- the plan: what changes, decisions, what is not being done
- the open questions, as cards with answer options and a recommended pick
- a box to ask Claude your own questions (answered on the same page) and a box for concerns

Choose **Approve & continue**, **Request changes** (Claude revises and shows a new round) or **Answer in console instead**. The terminal resumes when you submit; closing the tab changes nothing and the gate stays open at the same URL. Approve stays disabled until every question is answered and every uncommitted-changes repo has a decision. After 30 idle minutes the terminal asks whether to keep waiting.

The page falls back to the console on its own when the browser UI is off, you are on an SSH session, or the server cannot start. Gates 3 and 4 are always in the console.

## Where things live

`~/.orchestrator/` (`%USERPROFILE%\.orchestrator\` on Windows):

- `config.json`: optional, created the first time it is needed (a setup form opens in the browser, or the terminal asks). Keys: `username` and `worktree-root` (branch prep), `ui` (`browser` or `console`), `port` (default 4402), `browser` (set `false` to not auto-open), `idleMinutes` (default 30).
- `<repo-key>/tasks/<task>/`: `analysis-report.md`, `plan.md`, `verification-report.md`, and `gates/` with `gate-N.json` (what Claude showed) and `gate-N.state.json` (what you answered).
- `server.log`: requests and errors of the page server.

Per-repo knowledge (`ARCHITECTURE.md`, `FEATURES.md`, `decisions.md`) stays in each repo under `.claude/orchestrator/`, git-excluded.

## Privacy

The page is served on `127.0.0.1` only and answers only pages from itself (Host and Origin are checked). It shows the same summaries you would read in the console, never source files. Nothing leaves your machine.

## Troubleshooting

- **Port 4402 is in use by another program**: set another `port` in `config.json`.
- **The page did not open**: the URL is printed in the terminal; open it yourself.
- **Stale page after an update**: the server restarts itself on the new version the next time a gate opens.
- **Want the terminal only**: set `"ui": "console"` in `config.json`.

## Developing

The page server is `src/OrchestratorGate` (.NET 10, ASP.NET Core; the page is `src/OrchestratorGate/page/index.html`, embedded in the binary), tests are in `src/OrchestratorGate.Tests`.

```
dotnet test src/OrchestratorGate.Tests
./build        # tests, then publishes the three committed binaries to skills/browser-gate/dist/
```

`./build` needs the .NET 10 SDK. It stamps the plugin version and commit into the binary, so the running server restarts itself on a new build, and it marks the macOS and Linux binaries executable in git. Bump `version` in `.claude-plugin/plugin.json` when releasing so Claude Code sees the update.

The protocol between the orchestrator and the page (gate file contract, actions, rounds) is in `skills/browser-gate/SKILL.md`.
