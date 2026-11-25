---
name: rules-api
description: Apply when the change touches API endpoints, contracts, or client/server boundaries. Loaded conditionally by implementer and code-reviewer.
---

# API rules
- Thin controllers/handlers: validate input, delegate to a service, shape the response; no business logic or data access.
- Clear model names that state purpose (e.g. `SaveXInput` over `XInput`).
- Consistent error handling with appropriate error types, status codes and messages.
- Return explicit empty results (empty list), not null or ambiguous defaults.
- Contract changes are breaking unless purely additive; verify existing callers still work.
- Version or feature-flag breaking changes; document them and any new return types.
- Paginate unbounded lists.
- Depend on abstractions for services worth mocking or swapping; don't abstract plain data holders.
