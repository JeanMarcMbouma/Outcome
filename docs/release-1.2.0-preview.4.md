# Outcome 1.2.0-preview.4

This preview adds explicit, dependency-free retries to BbQ.Outcome (#78).

- Typed and heterogeneous `Outcome.RetryAsync` APIs retry operation factories.
- `RetryOptions` controls maximum attempts and caller-defined delays/backoff.
- Retry predicates inspect the complete error list. Exhaustion returns the final failure.
- Cancellation and unexpected exceptions propagate; use `Outcome.TryAsync` when exception conversion is needed.
- Expanded retry documentation covers backoff, cancellation, multi-error behavior, and exception-boundary composition.

All five Outcome packages share version 1.2.0-preview.4: BbQ.Outcome,
BbQ.Outcome.SourceGenerators, BbQ.Outcome.AspNetCore,
BbQ.Outcome.SystemTextJson, and BbQ.Outcome.Diagnostics.

See [async and error guidance](async-and-error-catalogs.md) for usage.
