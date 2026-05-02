# ADR-001: Aggregate-Slice Folder Convention

**Status:** Accepted

## Context

The PRD (User Story 22) specifies `src/<ContextName>/Features/<FeatureName>/` with
aggregates under a separate `Domain/` folder. The reference implementation
([assignment-flow](https://github.com/minhngkh/grading-system/tree/main/apps/assignment-flow))
uses a different layout: the aggregate root, its write model, value objects, and
all feature slice subfolders live together in one folder named after the aggregate —
no `Features/` or `Domain/` nesting.

These two conventions contradict each other. The PRD style separates "what the aggregate
is" from "what you can do with it". The reference style keeps both co-located because
every feature slice is inseparable from its aggregate.

## Decision

Use the **aggregate-slice** convention from the reference implementation:

```
<AggregateName>/
├── <AggregateName>Aggregate.cs
├── <AggregateName>WriteModel.cs
├── <FeatureName>/
│   ├── Command.cs
│   ├── EndpointHandler.cs
│   └── <EventName>Event.cs
```

## Rationale

- A `Domain/` folder implies a second layer of nesting that adds no information — every
  file in that folder is already domain code by definition.
- A `Features/` folder separates commands and events from the aggregate that emits them.
  Navigating a feature slice then requires jumping between two sibling folders.
- The reference demonstrates this at production scale across two aggregates with many
  slices; it works. The PRD convention has no working reference.
- Discoverability: `git grep ProductCreatedEvent` leads directly to
  `Products/Create/ProductCreatedEvent.cs` — the feature, event, and aggregate are all
  in the same path segment.

## Consequences

- New bounded context contributors must read this ADR before adding their first feature.
  The worked example in `CONTEXT.md` is the canonical guide.
- The PRD's folder description in User Story 22 is superseded by this ADR. The intent
  (one folder per use case, co-located files) is preserved; only the nesting changes.
