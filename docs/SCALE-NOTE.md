# Scale Note

A burst of 20,000 hold requests against a small set of popular voyages will contend on the authoritative `voyage_capacity` row. PostgreSQL serializes conflicting updates and the conditional update rejects losers without overbooking. This is the intended correctness tradeoff.

Admission control should combine per-voyage rate limits, bounded request queues, and backpressure before requests consume database connections. Keep connection pools sized for the database, avoid unbounded retries, and use jittered retry policy only for transient conflicts. Retry storms should be visible and capped.

Expiry and outbox workers should use bounded batches, a small number of instances, and leases or row claiming before scaling horizontally. Indexes on `(status, expires_at)` and `(processed, occurred_at)` keep polling targeted. Capacity conflicts, rejected holds, expiry lag, and outbox backlog should be measured by voyage and outcome, without high-cardinality identifiers in metric labels.

A cache can make display availability cheaper, but it cannot decide reservation correctness. PostgreSQL remains authoritative, and cache invalidation is event-driven with expiry fallback. Redis being unavailable must degrade reads to PostgreSQL and must not block correctness writes.
