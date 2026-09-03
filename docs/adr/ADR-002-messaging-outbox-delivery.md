# ADR-002: Messaging, Outbox & Delivery Semantics

## Status

Accepted

## Decision

Business state changes and integration intent are committed in one PostgreSQL transaction. The outbox publisher polls pending rows, sends the stable outbox ID as the message ID, records attempts and failures, and retries later. The delivery contract is at-least-once: a crash after publish and before marking processed may produce a duplicate.

The consumer uses `inbox_entry.message_id` as a durable uniqueness gate and applies its downstream effect in the same transaction. Duplicate delivery is therefore a committed no-op, not an in-memory decision. End-to-end exactly-once delivery is neither required nor claimed.

RabbitMQ is the preferred production transport for `BookingConfirmed` business events because routed queues, acknowledgements, bounded work distribution, and dead-letter/retry topology fit command-like business consumers. Kafka is preferred for future high-volume operational streams because partitions, retention, consumer groups, replay, and sequential per-key processing fit event-stream workloads. The outbox and inbox semantics remain the same for either transport.

## Consequences

The publisher must tolerate transient failures, duplicate publication, and poison messages. Production deployments should add row claiming/leases, bounded retries, and dead-letter handling. RabbitMQ adds broker topology and operational management; Kafka adds partition-key design, retention management, and greater operational complexity.
