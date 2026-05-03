# ADR-008: SharedKernel Minimalism

**Status:** Accepted

## Context

A `SharedKernel` project is referenced by all three context services. As Vernon warns in
*DDD Distilled*, Shared Kernel coupling is costly: any change to the shared project
requires coordinated deployment of all consumers.

EventFlow (the event-sourcing framework) already provides all DDD base types:
- `AggregateRoot<TAggregate, TIdentity>`
- `ValueObject` and `SingleValueObject<T>`
- `Identity<TIdentity>`
- `AggregateEvent<TAggregate, TIdentity>` / `IDomainEvent`

These must not be duplicated in `SharedKernel`.

## Decision

`SharedKernel` contains **only** `TenantId`, which extends EventFlow's `SingleValueObject<>`.

No other types may be added to `SharedKernel`.

## Rationale

- All DDD base types are provided by EventFlow — duplicating them in `SharedKernel` would
  create a maintenance burden and risk divergence from the framework.
- `Entity<TId>` is unnecessary in an event-sourced system: aggregate internals are
  reconstructed from events and expressed as value objects, not as tracked entities.
- `TenantId` is the single concrete type that genuinely crosses all context boundaries:
  every aggregate root carries one, the RLS middleware reads one, and the JWT ACL
  translates one. It belongs in `SharedKernel`.
- Everything else is context-specific. A `UserId` in `AgileProjectManagement` and a
  `UserId` in `IdentityAccess` may look identical but carry different invariants and
  lifecycle rules. Sharing them introduces invisible coupling.

## Enforcing the Constraint

- Any pull request that adds a type to `SharedKernel` beyond the five listed above must
  include a new ADR justifying the addition.
- The first addition is the signal that a coordination cost is being taken on. It should
  be a deliberate decision, not a convenience.

## Consequences

- Context-specific ID types (`ProductId`, `BacklogItemId`, `ForumId`, etc.) are defined
  in their own context project — either in the aggregate file or in `Shared/` within
  that context.
- Cross-context references (e.g., `AgileProjectManagement` needing a `ForumId` from
  `Collaboration`) are expressed as plain `string` or `Guid` in integration events, not
  as shared value object types.
