---
name: rules-coding
description: Language-agnostic coding rules: performance, data handling, concurrency, resilience and error handling, null safety, dates, naming, testing. Always loaded by implementer and code-reviewer; test-author reads the Testing section.
---

# Coding rules
- Avoid N+1 queries; batch or eager-load where appropriate.
- Handle errors explicitly; no silent catches.
- Name things for intent, not implementation.

## Performance
- Don't cache user-specific data in shared caches.
- Dispose or close resources and subscriptions deterministically.
- Never block on async work; use the language's async I/O end to end.
- Stream large sequences you only iterate once instead of materializing them.
- Avoid unnecessary allocations in hot paths; a plain loop beats a clever chain when it is clearer and cheaper.
- Use a builder or buffer for repeated string concatenation in loops.

## Data handling
- Pass only the data a function actually needs.
- Prefer immutable data for DTOs and value objects; avoid public mutable state when not needed.
- Use named fields or destructuring over positional access (`item1`, `[0]`) for multi-value results.
- Return explicit empty results (empty list) rather than null or ambiguous defaults.

## Concurrency and parallelism
- Run genuinely independent work concurrently; start the operations, then await them together.
- Keep dependent or ordered work sequential.
- Bound the degree of parallelism when hitting shared or rate-limited resources.
- State touched by concurrent work must be thread-safe: concurrent collections or proper synchronization.
- Don't wrap blocking work in a background thread to fake async on request paths; use real async APIs.
- Flow cancellation through async and parallel operations.
- No fire-and-forget: every started task is awaited or its failure is observed.

## Resilience and error handling
- Don't swallow errors; catch specifically, then handle or rethrow while preserving the original stack/cause.
- Fail fast on invalid state; validate preconditions early with meaningful errors.
- Retry only transient failures, with exponential backoff and a retry cap.
- Set explicit timeouts on every external call.
- Reuse pooled clients/connections; never create a new network client per call.

## Null safety and validation
- Null-check values that can be null, including configuration and dynamic data.
- Use the language's null-safety features and honor them; don't suppress warnings reflexively.

## Date handling
- Store and transport timestamps in UTC or with an explicit offset; convert to local time only at the edges.
- Make time zones explicit in comparisons.
- Parse varied date formats robustly without runtime throws; validate date ranges across fields.
- Sort on timestamps, not locale strings.
- Inject a clock where testable time matters instead of reading the system clock in logic.

## Testing
- First update or fix existing tests that cover the modified code.
- Add a new test only for new or changed behaviour; include the necessary edge and error cases, not every permutation.
- No coverage chasing and no per-function suites.
- Arrange-Act-Assert structure; one behaviour per test.
- Test names state the scenario and expected outcome.
- Mock infrastructure (DB, HTTP, message bus), never the unit under test; unit tests don't hit real infrastructure.
- Assert on outcomes and observable effects, not internal call sequences.
- When the change adds or alters I/O or parallel work, cover its failure, timeout and cancellation paths.
- Reuse fixtures and builders already in the test project.
