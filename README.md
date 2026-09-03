# Expiring Voyage Capacity Hold

A modular-monolith .NET 8 implementation of temporary voyage-capacity holds, booking confirmation, durable expiry processing, transactional outbox publication, and idempotent integration-event consumption.

## Scope

Included: Booking, VoyageCapacity, CapacityHold, PostgreSQL persistence, database-backed expiry polling, idempotency, transactional outbox, an inbox consumer, health reporting, and focused concurrency tests.

Excluded: pricing, payment, customer management, shipment execution, authentication, UI, Kubernetes, API gateway, event sourcing, and production Kafka/RabbitMQ infrastructure.

## Architecture

The application is a single deployable ASP.NET Core process with a modular domain/application/infrastructure structure.

- `Logistics.Domain` contains the aggregate rules and value-level state transitions.
- `Logistics.Application` contains command/query contracts.
- `Logistics.Infrastructure` contains EF Core PostgreSQL persistence, atomic capacity SQL, workers, outbox, inbox, and the consumer.
- `Logistics.Api` exposes task-oriented HTTP endpoints and hosts the workers.

The VoyageCapacity row owns the hot capacity invariant. Reservation uses one conditional PostgreSQL `UPDATE`; the affected-row count determines whether the request wins. Hold state and capacity changes are committed together with outbox intent.

See [Event Storming](docs/diagrams/event-storming.mmd), [context and aggregate boundaries](docs/diagrams/context-aggregate.mmd), and [failure sequence](docs/diagrams/failure-sequence.mmd).

## API

- `POST /api/bookings/{bookingId}/capacity-holds`
- `POST /api/bookings/{bookingId}/confirm`
- `GET /api/bookings/{bookingId}/capacity-hold`
- `GET /health`

Create and confirm requests may send an `Idempotency-Key` header. The key is persisted with a request fingerprint and stored result. Reusing it with a different payload is rejected.

## Prerequisites

- .NET 8 SDK
- Docker Desktop
- PostgreSQL 15, supplied by Docker Compose
- `psql` on the PATH for the manual SQL migration script

## Run locally

Start PostgreSQL:

```powershell
docker compose up -d db
```

Apply the checked-in SQL migration chain:

```powershell
$env:TEST_POSTGRES_CONN = "Host=localhost;Port=5432;Database=logistics_db;Username=logistics;Password=logistics_pwd"
powershell -ExecutionPolicy Bypass -File .\scripts\apply-migrations.ps1
```

Run the API:

```powershell
dotnet run --project .\src\Logistics.Api\Logistics.Api.csproj
```

The API requires `ConnectionStrings:LogisticsDb`. The default development settings target the Compose database. Do not use the EF InMemory provider for the real API path.

## Build and test

With PostgreSQL available and `TEST_POSTGRES_CONN` configured, the complete test suite can run with:

```powershell
dotnet build Logistics.slnx
dotnet test Logistics.slnx --no-restore
```

The two PostgreSQL race tests are intentionally non-skippable. Run the complete evidence suite with PostgreSQL available:

```powershell
$env:TEST_POSTGRES_CONN = "Host=localhost;Port=5432;Database=logistics_db;Username=logistics;Password=logistics_pwd"
dotnet test Logistics.slnx --no-restore
```

The two PostgreSQL race tests are intentionally non-skippable. They create unique voyage and booking IDs, use independent database contexts, coordinate overlap with a barrier, and apply migrations from the migration history. They prove one winner for final capacity and one terminal state for confirm-versus-expire.

## Persistence and migrations

The committed `InitialCreate` migration is preserved. Later EF migrations are incremental:

1. `20260901185816_InitialCreate`
2. `20260901191623_AddConstraintsAndIndexes`
3. `20260901191927_AddVoyageCapacitySumCheck`
4. `20260903160050_AddBookingConfirmationProjection`
5. `20260903163148_AddActiveHoldUniqueness`

The SQL files under `src/Logistics.Infrastructure/Migrations` follow the same order. The schema includes foreign keys for Booking and CapacityHold, capacity non-negative and sum checks, expiry and outbox indexes, and unique inbox/projection identities. `Version` is mapped as an EF concurrency token for Booking, CapacityHold, and VoyageCapacity; atomic SQL is used at the VoyageCapacity hot row.

## Failure and restart demonstrations

### Expiry after downtime

1. Start PostgreSQL and apply migrations.
2. Create an active hold with a short TTL.
3. Stop the API process before expiry.
4. Wait beyond `ExpiresAt`.
5. Start the API again.
6. The hosted expiry poller discovers the durable Active row, checks PostgreSQL time transactionally, marks it Expired, releases capacity, clears the booking reference, and writes `CapacityHoldExpired` to the outbox.

No in-memory timer is required. A delayed poll, process restart, or missed schedule is recovered by polling persisted due holds. Multiple workers race on a conditional Active-to-Expired update; only one can release capacity.

### Outbox publication failure

The command transaction commits business state and an outbox row before publication. The publisher sends pending rows and records attempts/errors. A sender failure leaves the row unprocessed; the next poll retries it. At-least-once delivery is assumed, so consumers use the stable message ID and inbox uniqueness. There is no end-to-end exactly-once claim.

### Duplicate integration delivery

`IntegrationEventConsumer` inserts the stable message ID into `inbox_entry` with `ON CONFLICT DO NOTHING`. Only the first delivery inserts the downstream `booking_confirmation_projection` effect. The duplicate-delivery test proves one inbox row and one projection row.

## Decisions

### CQRS

CQRS is useful at the boundary because commands and the capacity query have different concerns, but a separate database is unnecessary for this capability. The current read is a direct PostgreSQL query. A separate availability read model would add lag and invalidation complexity without reducing the VoyageCapacity hot-row contention. If introduced later for dashboards, eventual consistency is acceptable; reservation decisions must still use the authoritative capacity row.

### Redis

Redis may cache voyage availability for display, with short TTLs and invalidation after capacity events. It is not authoritative for reservation or expiry. If Redis is unavailable, reads fall back to PostgreSQL and writes continue. Redis is not used as the correctness lock because PostgreSQL already owns the invariant and the reservation update; a Redis lock would add failure modes without replacing the database condition.

### Kafka versus RabbitMQ

For `BookingConfirmed` business events, RabbitMQ is the pragmatic production choice when routing, acknowledgements, bounded work queues, and per-consumer retry/dead-letter behavior matter more than replay. For future high-volume operational streams, Kafka is preferable for partitioned throughput, retention, consumer groups, and replay. Kafka requires partition-key ordering decisions and more operational discipline. Neither changes the transactional outbox or at-least-once consumer semantics.

See [ADR-001](docs/adr/ADR-001-aggregate-capacity-expiry.md), [ADR-002](docs/adr/ADR-002-messaging-outbox-delivery.md), and the [scale note](docs/SCALE-NOTE.md).

## Observability

The API returns and logs `X-Correlation-ID`. Structured logs include BookingId, VoyageId, HoldId, MessageId, and message type where applicable. `GET /health` checks PostgreSQL reachability.

Metrics are emitted through `System.Diagnostics.Metrics`:

- `capacity_hold_created_total`
- `capacity_hold_expired_total`
- `capacity_hold_confirmed_total`
- `capacity_conflict_total`
- `expiry_lag_seconds`
- `outbox_backlog`
- `confirmation_duration`

## AI engineering note

See [AI Engineering Note](docs/AI-ENGINEERING-NOTE.md).
