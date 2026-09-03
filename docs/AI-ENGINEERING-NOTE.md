# AI Engineering Note

## Tools used

GitHub Copilot was used for repository exploration, targeted code changes, test scaffolding, migration review, and documentation drafting. Local build/test commands and Docker PostgreSQL were used for verification.

## Prompt patterns

Prompts were scoped to one remediation step at a time, required evidence from nearby code before editing, and required focused validation after each change. Concurrency prompts explicitly required independent database sessions, overlap coordination, and non-skippable tests.

## AI-generated or modified work

Copilot helped modify service validation, PostgreSQL configuration, migration scripts, hosted processing registration, inbox consumer code, API query handling, observability wiring, concurrency tests, and the submission documentation.

## Verification

The solution was repeatedly compiled with `dotnet build`. Unit tests were run after each focused change. PostgreSQL migrations were applied to clean Docker databases. The final concurrency tests ran with separate contexts and a real PostgreSQL instance. Duplicate delivery was verified with a durable inbox and downstream projection test.

## Rejected suggestion

An in-memory idempotency cache and Redis distributed lock were rejected. Neither can be the correctness boundary for capacity or durable command identity; PostgreSQL transactions and uniqueness constraints are authoritative.

## Defect discovered

An initial concurrency test version silently returned when PostgreSQL was unavailable and used assertions that allowed both requests to succeed. This was corrected to fail explicitly, coordinate genuine overlap, and require exactly one winner. A separate generated migration script also recreated the schema; it was corrected to contain only incremental operations.
