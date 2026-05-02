# ADR-002: EventFlow for CQRS/ES

**Status:** Accepted

## Context

The scaffold must demonstrate CQRS + Event Sourcing with PostgreSQL persistence. Options
considered: MediatR + manual event store, Marten, EventFlow.

## Decision

Use **EventFlow** (`EventFlow.PostgreSql`) for command dispatch, event store, and read
model projection.

## Rationale

- EventFlow provides first-class `AggregateRoot<T,TId>`, `AggregateState<T,TId,S>`,
  `IReadModel`, and `ICommandBus` abstractions that map directly to the DDD concepts
  the scaffold is teaching.
- `[EventVersion]` attribute on domain events forces explicit versioning, making
  upcasting visible as a future concern rather than an afterthought.
- `EventFlow.TestHelpers` provides a `Given / When / Then` API that lets Reqnroll
  feature files read as business specifications — a key goal in the PRD.
- PostgreSQL as the event store avoids introducing a separate infrastructure component
  (e.g., EventStoreDB) while keeping the full event-sourcing story intact.

## Consequences

- Each context project takes a dependency on `EventFlow.PostgreSql`.
- Event schema changes require upcasting via `IEventUpgrader<T,TId>` — teams must not
  rename event properties without a migration plan.
- EventFlow's `ICommandBus` is the only command dispatch mechanism. No MediatR.
