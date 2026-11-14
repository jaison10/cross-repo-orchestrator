---
name: rules-coding
description: Language-agnostic coding rules — performance pitfalls (e.g. N+1 queries), error handling, naming. Always loaded by implementer and code-reviewer.
---

# Coding rules
- Avoid N+1 queries; batch/eager-load where appropriate.
- Handle errors explicitly; no silent catches.
<!-- TODO: expand -->