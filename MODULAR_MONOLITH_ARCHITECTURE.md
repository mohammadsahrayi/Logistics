# Logistics - Modular Monolith Architecture

## Overview

This application implements a **Modular Monolith** architecture pattern - a single deployable application with clear business modules that can be independently tested, maintained, and potentially extracted into separate services if needed.

This approach aligns with the architectural principle: *"Implement as a single deployable application unless you have a concrete reason not to. A well-structured Modular Monolith/Worker is fully acceptable."*

## Why Modular Monolith?

### Advantages:
- ✅ **Simple deployment**: Single executable/container
- ✅ **Easy to develop & debug**: No distributed system complexity
- ✅ **Testable**: Modules can be tested in isolation via interfaces
- ✅ **Maintainable**: Clear separation of concerns and module boundaries
- ✅ **Flexible scaling path**: Can extract modules into separate services later
- ✅ **Consistent transactions**: Shared database for atomic operations across modules
- ✅ **No infrastructure overhead**: No need for service discovery, inter-process communication, etc.

### Avoids:
- ❌ Unnecessary network latency and complexity
- ❌ Distributed transaction challenges
- ❌ Service coordination overhead
- ❌ Deployment synchronization issues

## Project Structure

```
src/
├── Logistics.Modules.Capacity/       ← Capacity Management Module
│   ├── Domain/                       (Core business logic)
│   │   ├── Aggregates.cs             (VoyageCapacity, CapacityHold)
│   │   └── Repositories.cs           (Domain interfaces)
│   ├── Application/                  (Use cases)
│   │   └── CapacityApplicationService.cs  (ICapacityModule implementation)
│   ├── Infrastructure/               (Technical implementation)
│   │   └── Repositories.cs           (Repository implementations)
│   ├── Contracts/                    (Public API - only exports)
│   │   └── ICapacityModule.cs        (Public interface & DTOs)
│   └── CapacityModuleExtensions.cs   (DI registration)
│
├── Logistics.Modules.Booking/        ← Booking Module
│   ├── Domain/
│   │   └── Booking.cs                (Booking aggregate)
│   ├── Application/                  
│   ├── Infrastructure/               
│   ├── Contracts/
│   │   └── IBookingModule.cs         
│   └── BookingModuleExtensions.cs    
│
├── Logistics.Shared/                 ← Shared Infrastructure
│   ├── Persistence/                  (Shared entities & DbContext)
│   │   └── Entities.cs               (All database entities)
│   ├── Contracts/
│   │   └── IModules.cs               (Module interfaces)
│   ├── Observability/
│   │   └── LogisticsMetrics.cs       (Telemetry)
│   ├── Messaging/
│   │   └── IMessageSender.cs         (Event publishing)
│   ├── IClock.cs                     (Time abstraction)
│   └── DbClock.cs                    (Database-backed clock)
│
└── Logistics.Api/                    ← Entry Point
    ├── Program.cs                    (Module registration & bootstrap)
    ├── Controllers/                  (HTTP endpoints)
    │   └── BookingsController.cs     (Uses ICapacityModule facade)
    ├── Infrastructure/
    │   └── Middleware, HealthChecks, etc.
    └── Logistics.Api.csproj          (Depends on all modules & shared)
```

## Core Concepts

### 1. **Modules**

Each module represents a business capability:

#### Capacity Module (`ICapacityModule`)
- Manages voyage capacity (total, held, confirmed)
- Creates and manages capacity holds
- Expiry handling for expired holds
- Confirmation of bookings
- **Public API**: `ICapacityModule` in `Contracts/ICapacityModule.cs`

#### Booking Module (`IBookingModule`)
- Manages booking lifecycle
- Currently integrated with Capacity module
- Ready for independent growth
- **Public API**: `IBookingModule` in `Contracts/IBookingModule.cs`

### 2. **Module Boundaries**

Each module exposes a **public interface** (facade) that other modules use:

```csharp
// Only this is visible to other modules
namespace Logistics.Modules.Capacity.Contracts
{
    public interface ICapacityModule
    {
        Task<CreateHoldResult> CreateHoldAsync(...);
        Task<(bool Success, string? Reason)> ConfirmBookingAsync(...);
        // ...
    }
}
```

**Internal classes are NOT accessible** from outside the module:
- Domain aggregates (for reference only)
- Application services (implementation detail)
- Infrastructure repositories (implementation detail)

### 3. **Dependency Direction**

```
API Layer ──depends on──> Module Contracts ──implemented by──> Module Application
                              ^                                       |
                              |                                       |
                              └───────────── Module Domain ◄──────────┘
                                         └──> Shared Layer
```

**Key rule**: No circular dependencies. Booking module can depend on Capacity module, but not vice versa.

### 4. **Data Consistency**

- **Shared Database**: All modules use the same database via one `DbContext`
- **Atomic Transactions**: Changes across modules are atomic
- **No Distributed Transactions**: No need for complex 2-phase commit
- **Event Sourcing**: Outbox pattern for async event processing

### 5. **Communication Patterns**

#### Synchronous (within transaction)
```csharp
// Controller calls module facade
var result = await _capacityModule.CreateHoldAsync(...);
```

#### Asynchronous (via outbox)
```csharp
// Module publishes events to outbox
// Background worker processes and publishes externally
var outbox = new OutboxMessageEntity { MessageType = "CapacityHoldCreated", ... };
_db.OutboxMessages.Add(outbox);
```

## Detailed Module Walkthrough

### Capacity Module Structure

#### Domain Layer (`Domain/`)
```csharp
// Business rules in aggregates
public class VoyageCapacity
{
    public bool TryReserve(int units) { /* domain logic */ }
    public void ConfirmReserved(int units) { /* domain logic */ }
    // No persistence concerns - pure logic
}
```

#### Application Layer (`Application/`)
```csharp
// Use cases - orchestrates domain & infrastructure
public class CapacityApplicationService : ICapacityModule
{
    public async Task<CreateHoldResult> CreateHoldAsync(...)
    {
        // Transaction management
        // Idempotency handling
        // Outbox publishing
        // Error handling
    }
}
```

#### Infrastructure Layer (`Infrastructure/`)
```csharp
// Technical implementations
public class VoyageCapacityRepository : IVoyageCapacityRepository
{
    public async Task<bool> TryReserveAtomic(Guid voyageId, int units)
    {
        // Database-level atomic operation
        var sql = @"UPDATE voyage_capacity SET ... WHERE ...";
        return await _db.Database.ExecuteSqlRawAsync(sql, ...) == 1;
    }
}
```

#### Contracts Layer (`Contracts/`)
```csharp
// Public API - only thing other modules see
public interface ICapacityModule
{
    Task<CreateHoldResult> CreateHoldAsync(...);
    // ...
}

// DTOs exposed by module
public record CreateHoldResult(bool Success, Guid? HoldId, string? Reason);
```

### API Layer Integration

The API layer is the **entry point** that:
1. Registers all modules in DI
2. Configures shared infrastructure
3. Exposes HTTP endpoints using module facades

```csharp
// Program.cs - Module registration
builder.Services.AddCapacityModule(null!);    // Dependency injection
builder.Services.AddBookingModule();
builder.Services.AddScoped<IClock, DbClock>();
```

```csharp
// Controllers use module interfaces only
public class BookingsController : ControllerBase
{
    private readonly ICapacityModule _capacityModule;
    
    public BookingsController(ICapacityModule capacityModule)
    {
        _capacityModule = capacityModule;  // Injected facade
    }
    
    [HttpPost("/api/bookings/{bookingId:guid}/capacity-holds")]
    public async Task<IActionResult> CreateHold(...)
    {
        var result = await _capacityModule.CreateHoldAsync(...);
        // ...
    }
}
```

## Key Design Patterns

### 1. **Facade Pattern**
- `ICapacityModule` is the facade for the Capacity module
- Hides internal complexity from consumers
- Single point of module entry

### 2. **Repository Pattern**
- Each aggregate has repository
- Repositories persist changes and retrieve state
- Example: `IVoyageCapacityRepository`

### 3. **Outbox Pattern**
- Ensures event durability
- Integrates with application transactions
- Background worker processes events

### 4. **Idempotency Pattern**
- Request fingerprint + result caching
- Safe to retry operations
- Handles duplicate requests

### 5. **Dependency Injection**
- Constructor-based injection
- Interface-driven design
- Easy mocking for tests

## Adding New Features

### To extend Capacity Module:
1. Add domain logic to aggregates
2. Add repository methods if needed
3. Add application methods to service
4. Update `ICapacityModule` interface if public API changes
5. Update DI registration if new services added

### To add to Booking Module:
1. Implement `IBookingModule` in application
2. Create domain aggregates (already done: `Booking`)
3. Create repositories
4. Register in `BookingModuleExtensions`
5. Use in controllers

### To add shared infrastructure:
1. Add to `Logistics.Shared` project
2. Register in Program.cs
3. Inject where needed

## Migration Path to Microservices

If performance or scaling demands require it, **any module can become a separate service**:

1. Extract module to separate solution
2. Expose via REST/gRPC instead of interface
3. Replace interface with HTTP client
4. Share database initially, then split if needed

**No refactoring needed** because modules already have clear boundaries!

## Development Guidelines

### ✅ DO:
- Depend on other modules via their **Contracts only**
- Keep modules focused on one business capability
- Use `ICapacityModule` not `CapacityApplicationService`
- Test modules in isolation
- Document module responsibilities

### ❌ DON'T:
- Import internal module classes (Application, Infrastructure)
- Create circular dependencies between modules
- Share domain aggregates between modules
- Mix concerns in a single aggregate
- Access database directly, use repository pattern

## Testing Strategy

### Unit Tests
- Test aggregates without dependencies
- Test repositories with in-memory database
- Mock external dependencies

### Integration Tests
- Test full module via facade interface
- Use real database
- Verify transactions and events

### End-to-End Tests
- Test HTTP endpoints
- Verify full request/response lifecycle
- Test module interaction

## Deployment

**Single deployment artifact**:
```bash
# Build
dotnet build

# Test
dotnet test

# Publish
dotnet publish -c Release

# Deploy (single container/binary)
docker run myapp:latest
```

## Future Considerations

### If scaling demands justify it:
- Extract Capacity module to separate service
- Use message broker for async communication
- Implement API gateway pattern
- Add database per service

### Current state is optimal for:
- MVP and early growth phases
- Reduced operational complexity
- Simpler deployment pipeline
- Shared transaction semantics

## References

- **Modular Monolith Patterns**: Sam Newman, "Building Microservices" (Chapter on Modular Monoliths)
- **Outbox Pattern**: Chris Richardson, "Microservices Patterns"
- **Clean Architecture**: Robert C. Martin, "Clean Code"
- **Domain-Driven Design**: Eric Evans

---

**Last Updated**: September 2026
**Architecture Decision**: ADR-003-Modular-Monolith-Architecture
