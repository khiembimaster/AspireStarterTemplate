# ADR-003: RabbitMQ via MassTransit for Cross-Context Messaging

**Status:** Accepted

## Context

Three bounded contexts must exchange integration events without coupling their domain
models. Options considered: direct HTTP calls, NServiceBus, MassTransit + RabbitMQ,
MassTransit + Azure Service Bus.

## Decision

Use **MassTransit** as the messaging abstraction with **RabbitMQ** as the local
development transport. The AppHost registers RabbitMQ as an Aspire resource.

## Rationale

- MassTransit abstracts the transport: swapping from RabbitMQ (`AddRabbitMQ`) to Azure
  Service Bus (`AddAzureServiceBus`) in the AppHost is a one-line change, with no changes
  to consumer or publisher code.
- MassTransit's saga state machine (`MassTransitStateMachine<T>`) is the right tool for
  the tenant onboarding orchestration saga, providing built-in persistence, retry, and
  compensation support.
- RabbitMQ is a single Aspire container resource — no external dependency during
  development.
- Integration events are plain C# records in `<Context>.IntegrationEvents` projects.
  Consumers reference the contract project, not the publishing context's domain assembly.

## Consequences

- Each context registers its consumers via `services.AddMassTransit(x => ...)` in its
  `Bootstrapping/ServiceCollectionExtensions.cs`.
- Integration events must be versioned by namespace or a version property — not by
  changing existing record properties.
- Kafka is out of scope. If a context needs ordered, replayable streams beyond what
  RabbitMQ provides, that is a separate architectural decision.
