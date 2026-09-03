# ADR-001: Aggregate, Capacity Concurrency & Hold Expiry Semantics

## Status

Accepted

## Decision

Keep the capability in one modular monolith. `VoyageCapacity` owns the capacity invariant and is the database correctness boundary. `Booking` owns booking confirmation and its active hold reference. `CapacityHold` owns its lifecycle. They remain separate domain aggregates because they have distinct lifecycle concerns, while create, confirm, and expiry application services coordinate the required atomic database transaction.

Capacity reservation uses a PostgreSQL conditional `UPDATE` that requires an Open voyage and sufficient remaining capacity. The affected-row count determines the winner under contention. `Version` is mapped as an EF concurrency token for all three persistence entities.

PostgreSQL UTC time is authoritative for expiry. Confirmation requires `Active` and `dbNow < ExpiresAt`. Expiry requires `Active` and `dbNow >= ExpiresAt`, then conditionally changes the row to `Expired` before releasing capacity. Confirm and expire therefore produce one terminal state; retries are safe because terminal state transitions and capacity conversion/release are guarded.

## Consequences

The voyage row is a hot row for popular voyages and may serialize contention, but this is intentional because it protects correctness. Read availability can be slightly stale only when used for display; reservation always rechecks PostgreSQL. The expiry worker is durable polling rather than an in-memory timer, so restart recovery is automatic.
