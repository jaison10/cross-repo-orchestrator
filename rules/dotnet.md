---
name: rules-dotnet
description: Apply when the change touches C#/.NET code. Loaded conditionally by implementer, code-reviewer, test-author.
---

# .NET rules
.NET-specific companions to general.md and coding.md.

## Idioms
- `nameof()` instead of hardcoded member names.
- Structured logging with parameters, not string interpolation.
- `IOptions<T>` for configuration binding, not ad hoc `IConfiguration` reads.
- Correct DI lifetimes; never inject a scoped service into a singleton (captive dependency).
- `TimeProvider` or a clock abstraction instead of `DateTime.Now` in logic.
- `is null` / `is not null` over `== null`.
- Remove unused `using` directives.
- Prefer `record` types and read-only properties for DTOs and value objects.
- Tuple deconstruction instead of `.Item1` / `.Item2`.
- Static mappers where they improve performance.

## API and errors
- Consistent exception types and handling pattern across the codebase.
- Rethrow with `throw;`, not `throw ex;`; no empty `catch {}`.
- Use `IHttpClientFactory` (typed/named clients); never `new HttpClient()` per call.
- Use a resilience policy (e.g. Polly) for retries with backoff on transient errors.

## Performance
- Never block on `.Result` / `.Wait()`.
- Prefer `IEnumerable<T>` streaming for single-pass large sequences.
- `Span<T>` / `Memory<T>` only where it genuinely matters in hot paths.
- `StringBuilder` for repeated concatenation in loops.

## Async and concurrency
- Independent I/O: start tasks, then `await Task.WhenAll(...)`; don't `await` inside a loop of independent iterations.
- CPU-bound parallelism: `Parallel.ForEachAsync` or PLINQ with `MaxDegreeOfParallelism`, not hand-rolled threads.
- Shared state under parallelism: `ConcurrentDictionary` / `ConcurrentBag` or locking; never mutate a shared `List<T>`.
- No `Task.Run` around blocking work on ASP.NET request paths.
- Accept and pass `CancellationToken` through async and parallel calls.
- No `async void` except event handlers.
- `ConfigureAwait(false)` in shared library code; not required in ASP.NET Core request handling.

## Null safety and dates
- Nullable reference types enabled; honor annotations, don't sprinkle `!`.
- `DateTimeOffset` or UTC `DateTime` for storage and transport.

## Package management
- Release package versions, not pre-release, unless explicitly required.
- Consistent package versions across projects in the solution.
- Keep lock files updated.

## Testing
- Test names follow `Method_WhenScenario_ExpectedOutcome` (e.g. `DeleteCard_WhenCardNotFound_ReturnsNotFound`), unless the repo uses another pattern.
- Use the repo's existing test framework and mocking library; don't add another.
- Cover `CancellationToken` cancellation and timeout paths for async I/O.
