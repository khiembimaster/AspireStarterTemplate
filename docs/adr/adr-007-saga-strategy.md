# ADR-007: Saga Strategy — Choreography vs Orchestration

**Status:** Accepted

## Context

Two multi-step workflows require coordination across aggregates or services:

1. **`BacklogItemCommitted` → Sprint update**: When a `BacklogItem` is committed to a
   `Sprint`, the `Sprint` aggregate must record that commitment.
2. **Tenant onboarding**: Creating a `Tenant` must atomically provision a `User` in
   `IdentityAccess`, a default `Product` in `AgileProjectManagement`, and a welcome
   `Forum` in `Collaboration`, compensating on failure at each step.

## Decision

- Use **choreography** (MassTransit consumer reacting to a domain event) for the
  `BacklogItemCommitted` → Sprint update. No central coordinator.
- Use **orchestration** (MassTransit saga state machine) for tenant onboarding. Central
  state machine in `IdentityAccess` issues commands and handles compensation.

## Rationale

### Choreography for `BacklogItemCommitted`

The flow involves two aggregates within the same bounded context (`AgileProjectManagement`)
and has no compensation requirement — committing a `BacklogItem` to a `Sprint` is not
rolled back if the `Sprint` update fails (the domain enforces re-try). A MassTransit
consumer in `Sprints/CommitBacklogItem/BacklogItemCommittedConsumer.cs` reacting to a
published domain event is sufficient and avoids over-engineering a simple reactive flow.

### Orchestration for Tenant Onboarding

Tenant onboarding crosses three bounded contexts and requires compensation at each step
(e.g., if `AgileProjectManagement` fails to provision a `Product`, the previously created
`Tenant` and `User` must be rolled back or marked as failed). A MassTransit saga state
machine provides:
- Durable state between steps (persisted in the `IdentityAccess` database)
- Built-in retry and timeout handling
- Explicit compensation transitions (e.g., `TenantOnboardingFailed` state)

## File Locations

| Saga | Type | Location |
|---|---|---|
| BacklogItem → Sprint | Choreography consumer | `AgileProjectManagement/Sprints/CommitBacklogItem/BacklogItemCommittedConsumer.cs` |
| Tenant onboarding | MassTransit state machine | `IdentityAccess/Tenants/Onboard/TenantOnboardingSaga.cs` |

## Consequences

- The choreography approach means there is no single place to observe the
  `BacklogItemCommitted` → Sprint flow. Aspire dashboard distributed traces are the
  primary observability tool.
- The orchestration saga state is persisted in the `IdentityAccess` PostgreSQL database.
  Schema migrations for the saga state table are managed in that context's `Migrations/`.
- Adding a fourth step to tenant onboarding requires modifying only the state machine —
  not every participant.
