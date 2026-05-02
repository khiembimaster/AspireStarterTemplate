# ADR-008: SharedKernel Minimalism

**Status:** Accepted

## Context

A `SharedKernel` project is referenced by all three context services. As Vernon warns in
*DDD Distilled*, Shared Kernel coupling is costly: any change to the shared project
requires coordinated deployment of all consumers.

## Decision

`SharedKernel` contains **only** these five types:

```
AggregateRoot<TAggregate, TId>
Entity<TId>
ValueObject
IDomainEvent
TenantId
```

No other types may be added to `SharedKernel`.

## Rationale

- `TenantId` is the single value object that genuinely crosses all context boundaries:
  every aggregate root carries one, the RLS middleware reads one, and the JWT ACL
  translates one. It belongs in `SharedKernel`.
- Everything else is context-specific. A `UserId` in `AgileProjectManagement` and a
  `UserId` in `IdentityAccess` may look identical but carry different invariants and
  lifecycle rules. Sharing them introduces invisible coupling.
- Base DDD types (`AggregateRoot`, `Entity`, `ValueObject`) belong in `SharedKernel`
  because they are infrastructure-level abstractions, not domain concepts.

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
