---
name: rules-security
description: Apply when the change touches auth, input handling, secrets, or sensitive data. Loaded conditionally by implementer and code-reviewer.
---

# Security rules

## Input validation
- Validate all user input at the boundary, before it reaches business logic.
- Parameterised queries / proper ORM use; never string-concatenated SQL or commands.
- Validate reflection or dynamic property/method access against an allow-list.

## Authorization
- Check authorization on every access: users reach only their own data; no cross-tenant or cross-department access.
- Least privilege for users, services and credentials.

## Secrets and sensitive data
- No secrets in code, config files or logs; use a secrets store, environment or key vault.
- Keep PII, credentials and tokens out of logs and error messages.

## Configuration security
- Validate user-configurable field names and settings.
- Allow-list any dynamic access driven by configuration.
