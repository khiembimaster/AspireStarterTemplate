# Development Workflow

How a requirement moves from vague idea to passing test in this scaffold.
Based on the six-phase BDD flow from *BDD in Action 2e* (Smart & Molak), with each phase
mapped to the specific tools and artefacts in this repo.

---

## DDD and BDD — a Refinement Loop

These are not competing approaches, and they are not strictly sequential either.

**DDD** sketches the skeleton first: Aggregate boundaries, Ubiquitous Language, Commands,
and Domain Events emerge from Event Storming. This must come before any story is written.
Stories written without a domain sketch each implicitly define part of the model in
isolation — contradictions only surface when both are implemented and they conflict, at
which point the aggregate boundary is expensive to fix.

**BDD scenarios then refine the domain model** — not just verify it. Vernon's *DDD
Distilled* Ch. 2 makes this explicit:

> "Write concrete scenarios: natural language descriptions of how model elements
> interact. These surface ambiguities and lead to deeper insights. Use Given/When/Then
> specifications to validate the language against the model."

A `.feature` file is not a test bolted onto a finished model. It is the Ubiquitous
Language made executable. When a scenario reads naturally in business language, the model
is right. When a scenario feels forced — when you have to translate between what the
business says and what the step text says — the model has an ambiguity or a wrong
boundary. That friction is the signal to refine the domain, not to work around it in the
step definition.

The loop is:

```
Event Storming  →  initial sketch (Aggregates, Commands, Events in CONTEXT.md)
      ↑                    ↓
 model refined      Example Mapping + .feature files
      ↑                    ↓
CONTEXT.md updated ← awkward scenario = wrong model
```

**The gate into the loop is `CONTEXT.md`.** A use case may not proceed to Phase 2
(Illustrate) until its Aggregate, Command, and Domain Event names are present and absent
from the `Flagged Ambiguities` section. The gate out of each loop iteration is a
`.feature` file that reads naturally to a domain expert without explanation.

---

## The Pipeline

```
Speculate ──► Illustrate ──► Formulate ──► Automate ──► Demonstrate ──► Validate
    │              │              │              │              │              │
Event         Example        Reqnroll       Reqnroll       Passing        Aspire
Storming      Mapping        .feature       Steps.cs       tests =        dashboard
(DDD)         (BDD)          file           HttpClient     living docs    + usage
```

---

## Phase 1 — Speculate

**Goal:** identify what to build and why, at the domain level.

Run an **Event Storming** session (Big Picture, then Software Design level) with domain
experts and developers. Output: Aggregates, Commands, and Domain Events named in the
Ubiquitous Language.

Artefacts produced:
- Updated `CONTEXT.md` — Ubiquitous Language table, Aggregates table, resolved Flagged Ambiguities
- New `docs/adr/` entries if a structural decision is locked during the session

The Event Storming session is the prerequisite for writing tests, not the prerequisite
for writing code. You cannot write a meaningful `.feature` file without a settled
Ubiquitous Language.

---

## Phase 2 — Illustrate

**Goal:** build deep understanding of one specific use case through concrete examples.

Run an **Example Mapping** session (Three Amigos: business, dev, tester). Take one
Command/Event pair from the Event Storming output and surface:
- The happy path
- Business rules that constrain it (rule cards)
- Concrete examples for each rule (example cards)
- Open questions (red cards — resolve before Formulate)

The Ubiquitous Language from `CONTEXT.md` must be spoken verbatim in this session.
Any new term that emerges goes into `CONTEXT.md` before the session ends.

---

## Phase 3 — Formulate

**Goal:** turn concrete examples into an executable specification readable by domain experts.

Write a **Reqnroll `.feature` file** for the use case. Location follows the
aggregate-slice convention:

```
src/<Context>/<Context>.Tests/<Aggregate>/<UseCase>/<UseCase>.feature
```

Example: `src/AgileProjectManagement/AgileProjectManagement.Tests/BacklogItems/CommitToSprint/CommitToSprint.feature`

```gherkin
Feature: Commit BacklogItem to Sprint

  Background:
    Given a Tenant "Acme" is registered
    And a Product "Inventory System" exists for Tenant "Acme"
    And a BacklogItem "Add barcode scanning" is planned on the Product
    And a Sprint "Sprint 1" is scheduled on the Product

  Scenario: successfully committing a BacklogItem to a Sprint
    When I commit BacklogItem "Add barcode scanning" to Sprint "Sprint 1"
    Then Sprint "Sprint 1" reflects BacklogItem "Add barcode scanning" as committed

  Scenario: committing an already-committed BacklogItem
    Given BacklogItem "Add barcode scanning" is already committed to Sprint "Sprint 1"
    When I commit BacklogItem "Add barcode scanning" to Sprint "Sprint 1"
    Then the request is rejected with a conflict error
```

Rules:
- Step text uses the **exact terms** from `CONTEXT.md` — no synonyms, no abbreviations
- Scenarios describe observable behaviour, not HTTP verbs or JSON fields
- `Background` steps seed the world via the same HTTP endpoints `When` steps will call

---

## Phase 4 — Automate

**Goal:** make the `.feature` file executable by wiring step definitions to the real system.

Write `Steps.cs` and `StepContext.cs` in the same slice folder as the `.feature` file.
Step definitions call real HTTP endpoints via `HttpClient` against the running Aspire host.
No mocks. No `EventFlow.TestHelpers`. Full stack.

```
src/AgileProjectManagement/AgileProjectManagement.Tests/BacklogItems/CommitToSprint/
├── CommitToSprint.feature
├── Steps.cs
└── StepContext.cs
```

**`StepContext.cs`** — scenario-scoped shared state, injected by Reqnroll:

```csharp
namespace AgileProjectManagement.Tests.BacklogItems.CommitToSprint;

public class StepContext
{
    public string ProductId      { get; set; } = string.Empty;
    public string BacklogItemId  { get; set; } = string.Empty;
    public string SprintId       { get; set; } = string.Empty;
    public HttpResponseMessage? LastResponse { get; set; }
}
```

**`Steps.cs`** — bindings call the real API:

```csharp
namespace AgileProjectManagement.Tests.BacklogItems.CommitToSprint;

[Binding]
public class Steps(StepContext ctx, ApiClient api)
{
    [When(@"I commit BacklogItem ""(.*)"" to Sprint ""(.*)""")]
    public async Task WhenICommitToSprint(string summary, string sprintName)
    {
        ctx.LastResponse = await api.PostAsync(
            $"/api/agile/backlog-items/{ctx.BacklogItemId}/commit",
            new { SprintId = ctx.SprintId });
    }

    [Then(@"Sprint ""(.*)"" reflects BacklogItem ""(.*)"" as committed")]
    public async Task ThenSprintReflectsCommit(string sprintName, string summary)
    {
        var sprint = await api.GetAsync<SprintReadModel>(
            $"/api/agile/sprints/{ctx.SprintId}");

        sprint.CommittedBacklogItems
            .Should().ContainSingle(b => b.Summary == summary);
    }
}
```

Shared infrastructure lives in `Shared/`:

```
Shared/
├── ApiClient.cs       # typed HttpClient wrapper; injects Tenant JWT per scenario
└── AspireFixture.cs   # starts the full Aspire host once per test run
```

The Aspire host starts all services with real infrastructure (PostgreSQL, RabbitMQ,
Keycloak) before the test run begins. Each scenario gets a fresh Tenant JWT so RLS
isolation is exercised automatically.

---

## Phase 5 — Demonstrate

**Goal:** passing tests act as evidence and living documentation.

When all step definitions pass, the `.feature` file is living documentation of that use
case's behaviour. It cannot go stale — if the domain model drifts from the Ubiquitous
Language, the step text breaks before any human notices.

The Reqnroll HTML report is the primary handoff artefact to non-technical stakeholders.

---

## Phase 6 — Validate

**Goal:** verify the feature delivers real business value in the running system.

Use the **Aspire dashboard** to observe the full event flow:
- Structured logs show commands received and domain events emitted per service
- Distributed traces show choreography paths (e.g. `BacklogItemCommitted` →
  `BacklogItemCommittedConsumer` → `RecordCommittedBacklogItem` → `BacklogItemAddedToSprint`)
- Read model projections confirm eventual consistency resolved within expected latency

---

## DDD ↔ BDD Mapping

| DDD concept | BDD phase | Artefact |
|---|---|---|
| Event Storming session | Speculate | Updated `CONTEXT.md` |
| Ubiquitous Language term | Formulate | Gherkin step text |
| Command (blue sticky) | Formulate → Automate | `When` step → `api.PostAsync(...)` |
| Domain Event (orange sticky) | Formulate → Automate | `Then` step → read model assertion |
| Aggregate past state | Formulate → Automate | `Given` step → seed HTTP calls |
| Bounded Context | — | `.Tests` project boundary |

---

## Adding a New Use Case

1. **Speculate / Illustrate**: run Example Mapping; resolve all red cards; update `CONTEXT.md`
2. **Formulate**: create `<UseCase>.feature` in `<Context>.Tests/<Aggregate>/<UseCase>/`
3. **Automate**: add `Steps.cs` and `StepContext.cs`; run — watch it fail (red)
4. Implement `Command.cs`, `EndpointHandler.cs`, domain event, and read model projection in `.Application`
5. Run again — watch it pass (green)
6. **Demonstrate**: the passing scenario is now living documentation of the domain behaviour
