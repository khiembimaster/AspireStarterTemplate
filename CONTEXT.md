# Domain Context

## Problem Domain

This scaffold demonstrates a production-grade microservices SaaS architecture using the
Scrum-based agile project management domain from *DDD Distilled* (Vaughn Vernon). The
domain is universally understood, rich enough to exercise all three subdomain types and
multiple context mapping relationships, and thin enough to read in one sitting.

---

## Bounded Contexts

| Context | Subdomain Type | Project |
|---|---|---|
| `AgileProjectManagement` | Core | `AgileProjectManagement/` |
| `Collaboration` | Supporting | `Collaboration/` |
| `IdentityAccess` | Generic | `IdentityAccess/` |

---

## Context Map

```
IdentityAccess ──── OHS/PL ────────────────────► AgileProjectManagement
                   (JWT claims as Published Language)  ACL translates claims
                                                        to TenantId / UserId

Collaboration ───── Customer/Supplier ──────────► AgileProjectManagement
                   (publishes integration events)       ACL translator subscribes
```

- **Open Host Service / Published Language**: `IdentityAccess` exposes Keycloak JWT
  claims as its Published Language. `AgileProjectManagement` contains an Anti-Corruption
  Layer that translates those claims into its own `TenantId` and `UserId` value objects.
  No `IdentityAccess` domain classes are referenced outside that ACL.

- **Customer–Supplier**: `Collaboration` (upstream) publishes integration events;
  `AgileProjectManagement` (downstream) subscribes through an ACL translator. Integration
  events cross context boundaries as plain JSON contracts — consumers never reference
  source domain classes.

---

## Aggregates

### AgileProjectManagement

Four separate aggregate roots per Vernon's Rule 2 (*DDD Distilled* Ch. 5). Each holds
only identity references (`ProductId`, `SprintId`, etc.) to the others — never object
references.

**Factory pattern (Vernon's model)**: `Product` is the factory for `BacklogItem`, `Sprint`,
and `Release`. The Application Service supplies a pre-generated ID, calls the factory method
on `Product`, and saves both the updated `Product` (carrying the factory event) and the newly
created child aggregate in two sequential EventFlow commands.

**Idempotency contract (Option C)**: Both commands in a factory use case are idempotent.
`Product.PlanBacklogItem()` checks `State` and skips the emit if the `BacklogItemId` is already
recorded. The `BacklogItemAggregate` CommandHandler returns early if `!aggregate.IsNew`. This
makes the entire endpoint safely retryable end-to-end without saga or retry infrastructure.

| Aggregate | Key Commands | Key Domain Events |
|---|---|---|
| `Product` | `CreateProduct`, `PlanBacklogItem`, `ScheduleSprint`, `ScheduleRelease` | `ProductCreated`, `BacklogItemPlanned`, `SprintScheduled`, `ReleaseScheduled` |
| `BacklogItem` | `CommitToSprint`, `ReorderBacklogItem`, `UpdateStatus` | `BacklogItemCommitted`, `BacklogItemReordered`, `BacklogItemStatusUpdated` |
| `Sprint` | `Start`, `End`, `RecordCommittedBacklogItem` | `SprintStarted`, `SprintEnded`, `BacklogItemAddedToSprint` |
| `Release` | `AddBacklogItem`, `Publish` | `BacklogItemAddedToRelease`, `ReleasePublished` |

**Choreography note**: `Sprint` does not emit `BacklogItemCommitted`. It *reacts* to
`BacklogItemCommitted` (emitted by `BacklogItem`) via a MassTransit consumer that issues
`RecordCommittedBacklogItem` to the `Sprint` aggregate, which then emits `BacklogItemAddedToSprint`.
This is Vernon's Rule 4 canonical example.

### Collaboration

| Aggregate | Key Commands | Key Domain Events |
|---|---|---|
| `Forum` | `CreateForum`, `ModeratePost` | `ForumCreated`, `PostModerated` |
| `Discussion` | `StartDiscussion`, `PostMessage` | `DiscussionStarted`, `MessagePosted` |

### IdentityAccess

| Aggregate | Key Commands | Key Domain Events |
|---|---|---|
| `Tenant` | `RegisterTenant`, `ActivateTenant` | `TenantRegistered`, `TenantActivated` |
| `User` | `RegisterUser`, `AssignRole` | `UserRegistered`, `RoleAssigned` |

---

## Ubiquitous Language

| Term | Context | Meaning |
|---|---|---|
| `Product` | AgileProjectManagement | Software product under active development; top-level planning container |
| `BacklogItem` | AgileProjectManagement | Unit of planned work on a Product's backlog |
| `Sprint` | AgileProjectManagement | Time-boxed iteration within which BacklogItems are completed |
| `Release` | AgileProjectManagement | Delivery milestone grouping completed BacklogItems; separate aggregate root |
| `BacklogItemCommitted` | AgileProjectManagement | Event: a BacklogItem has been committed to a Sprint |
| `SprintScheduled` | AgileProjectManagement | Event: a Sprint has been scheduled on a Product |
| `Forum` | Collaboration | Discussion space provisioned when a Tenant's Product is created |
| `Discussion` | Collaboration | Threaded conversation thread within a Forum |
| `Tenant` | IdentityAccess | Organisation using the SaaS platform |
| `User` | IdentityAccess | Individual member belonging to a Tenant |
| `TenantId` | SharedKernel | Immutable value object identifying a Tenant across all contexts |

---

## Solution Structure

```
AspireStarterTemplate.sln
├── SharedKernel/
├── AgileProjectManagement/
│   ├── AgileProjectManagement.Application/
│   ├── AgileProjectManagement.Tests/
│   └── AgileProjectManagement.IntegrationEvents/
├── Collaboration/
│   ├── Collaboration.Application/
│   ├── Collaboration.Tests/
│   └── Collaboration.IntegrationEvents/
├── IdentityAccess/
│   ├── IdentityAccess.Application/
│   ├── IdentityAccess.Tests/
│   └── IdentityAccess.IntegrationEvents/
├── Gateway/                               # YARP gateway + SPA host
├── AppHost/
├── frontend/
└── docs/
    └── adr/
```

### SharedKernel

Contains only base DDD primitives — no domain-specific types. Adding anything
domain-specific to `SharedKernel` is the first sign of Shared Kernel coupling (see ADR-008).

```
SharedKernel/
├── AggregateRoot.cs      # abstract AggregateRoot<TAggregate, TId>
├── Entity.cs             # abstract Entity<TId>
├── ValueObject.cs        # abstract ValueObject
├── IDomainEvent.cs
└── TenantId.cs           # the one value object shared by all contexts
```

---

## File Layout Convention

Each context project follows the **aggregate-slice** pattern from the
[assignment-flow](https://github.com/minhngkh/grading-system/tree/main/apps/assignment-flow)
reference: aggregate types and their feature slices are co-located under a single named
folder. There is no `Features/` or `Domain/` nesting layer (see ADR-001).

```
<ContextName>/<ContextName>.Application/
├── <AggregateName>/
│   ├── <AggregateName>Aggregate.cs          # AggregateRoot<T,TId> — methods emit events
│   ├── <AggregateName>WriteModel.cs         # AggregateState<T,TId,TState> — applies events
│   ├── <AggregateName>StateMachine.cs       # Stateless lifecycle machine (when needed)
│   ├── <AggregateName>EntityTypeConfig.cs   # EF Core read-table column mapping
│   ├── <EntityOrValueObject>.cs             # Value objects / child entities for this aggregate
│   └── <FeatureName>/                       # One folder per use case (verb-noun)
│       ├── Command.cs                       # Command record + CommandHandler
│       ├── EndpointHandler.cs               # Minimal API extension method
│       ├── <PastTenseVerb>Event.cs          # AggregateEvent [EventVersion]
│       └── <ReadModelName>.cs               # IReadModel projection (query side, if needed)
├── Shared/
│   ├── <ContextName>DbContext.cs
│   ├── <ContextName>DbContextProvider.cs
│   └── ServiceCollectionExtensions.cs
├── Bootstrapping/
│   ├── ServiceCollectionExtensions.cs       # EventFlow, EF, MassTransit registration
│   ├── DbInitializer.cs                     # Schema + RLS policy setup
│   └── Worker.cs                            # Background startup worker
├── Migrations/
├── Program.cs
└── GlobalUsings.cs
```

Each context folder also contains a test project and an integration-events project:

```
<ContextName>/
├── <ContextName>.Application/              # domain + application code (above)
├── <ContextName>.Tests/                    # Reqnroll .feature files + step definitions
│   ├── Features/
│   │   └── <AggregateName>.feature
│   └── StepDefinitions/
│       └── <AggregateName>Steps.cs
└── <ContextName>.IntegrationEvents/
    └── <IntegrationEventName>.cs           # Plain C# record — no domain dependencies
```

---

## Worked Example — `Products/Create`

### `Products/ProductAggregate.cs`

```csharp
namespace AgileProjectManagement.Products;

public class ProductAggregate : AggregateRoot<ProductAggregate, ProductId>
{
    public readonly ProductWriteModel State;

    public ProductAggregate(ProductId id) : base(id)
    {
        State = new ProductWriteModel();
        Register(State);
    }

    public void CreateProduct(Create.Command command)
    {
        Emit(new Create.ProductCreatedEvent
        {
            TenantId = command.TenantId,
            Name     = command.Name,
            OwnerId  = command.OwnerId,
        });
    }

    public void PlanBacklogItem(PlanBacklogItem.Command command) { /* ... */ }
    public void ScheduleSprint(ScheduleSprint.Command command)   { /* ... */ }
}

public class ProductId(string id) : Identity<ProductId>(id) { }
```

### `Products/ProductWriteModel.cs`

```csharp
namespace AgileProjectManagement.Products;

public class ProductWriteModel
    : AggregateState<ProductAggregate, ProductId, ProductWriteModel>
{
    public TenantId TenantId { get; private set; } = TenantId.Empty;
    public string   Name     { get; private set; } = string.Empty;

    internal void Apply(Create.ProductCreatedEvent e)
    {
        TenantId = e.TenantId;
        Name     = e.Name;
    }
}
```

### `Products/Create/Command.cs`

```csharp
namespace AgileProjectManagement.Products.Create;

public class Command(ProductId id) : Command<ProductAggregate, ProductId>(id)
{
    public required TenantId TenantId { get; init; }
    public required string   Name     { get; init; }
    public required UserId   OwnerId  { get; init; }
}

public class CommandHandler : CommandHandler<ProductAggregate, ProductId, Command>
{
    public override Task ExecuteAsync(
        ProductAggregate aggregate, Command command, CancellationToken ct)
    {
        if (!aggregate.IsNew) return Task.CompletedTask;
        aggregate.CreateProduct(command);
        return Task.CompletedTask;
    }
}
```

### `Products/Create/ProductCreatedEvent.cs`

```csharp
namespace AgileProjectManagement.Products.Create;

[EventVersion("productCreated", 1)]
public class ProductCreatedEvent : AggregateEvent<ProductAggregate, ProductId>
{
    public required TenantId TenantId { get; init; }
    public required string   Name     { get; init; }
    public required UserId   OwnerId  { get; init; }
}
```

### `Products/Create/EndpointHandler.cs`

```csharp
namespace AgileProjectManagement.Products.Create;

public static partial class EndpointHandler
{
    public static IEndpointRouteBuilder MapCreateProduct(this IEndpointRouteBuilder app)
    {
        app.MapPost("/", CreateProduct)
            .WithName("CreateProduct")
            .Produces<ProductSummary>(StatusCodes.Status201Created);
        return app;
    }

    [Authorize]
    private static async Task<IResult> CreateProduct(
        [FromBody] CreateProductRequest request,
        ICommandBus commandBus,
        IQueryProcessor queryProcessor,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var tenantId  = TenantId.With(user.FindFirstValue("tenant_id")!);
        var productId = ProductId.NewComb();

        await commandBus.PublishAsync(new Command(productId)
        {
            TenantId = tenantId,
            Name     = request.Name,
            OwnerId  = UserId.With(user.FindFirstValue(ClaimTypes.NameIdentifier)!),
        }, ct);

        var product = await queryProcessor.ProcessAsync(
            new ReadModelByIdQuery<ProductSummary>(productId), ct);

        return TypedResults.CreatedAtRoute(product, "GetProductById", new { id = productId });
    }
}
```

### `Products/Create/ProductSummary.cs` (read model projection)

```csharp
namespace AgileProjectManagement.Products.Create;

public class ProductSummary
    : IReadModel,
      IAmReadModelFor<ProductAggregate, ProductId, ProductCreatedEvent>
{
    public string Id       { get; set; } = string.Empty;
    public string Name     { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;

    public Task ApplyAsync(
        IReadModelContext context,
        IDomainEvent<ProductAggregate, ProductId, ProductCreatedEvent> domainEvent,
        CancellationToken ct)
    {
        Id       = domainEvent.AggregateId.Value;
        Name     = domainEvent.AggregateEvent.Name;
        TenantId = domainEvent.AggregateEvent.TenantId.Value;
        return Task.CompletedTask;
    }
}
```

---

## Saga Disambiguation

Two distinct saga mechanisms appear in this codebase:

| Mechanism | Library | Scope | Location |
|---|---|---|---|
| **Aggregate state machine** | Stateless | Single aggregate's lifecycle (e.g., Draft → Active → Closed) | `<AggregateName>StateMachine.cs`, co-located with the aggregate |
| **Orchestration saga** | MassTransit | Multi-service workflow with compensation (tenant onboarding) | `Tenants/Onboard/TenantOnboardingSaga.cs` in `IdentityAccess` |
| **Choreography saga** | MassTransit consumer | Reaction to a cross-aggregate event (no central coordinator) | `Sprints/CommitBacklogItem/BacklogItemCommittedConsumer.cs` in `AgileProjectManagement` |

---

## Multi-Tenancy

Every aggregate root carries a `TenantId` (from `SharedKernel`). Two isolation layers:

1. **EF Core read models**: A `DbConnectionInterceptor` executes `SET app.tenant_id = '...'`
   on every connection borrow. PostgreSQL RLS policies filter all read-model table rows by
   this setting. The interceptor is automatic — no middleware or manual call sites required.
   (See ADR-006.)

2. **EventFlow event store**: Isolated by stream name prefix (`{tenantId}-`). No RLS is
   applied to EventFlow's internal tables — stream prefix is sufficient.

The ACL in `AgileProjectManagement/Shared/` translates the `tenant_id` JWT claim (issued
by Keycloak) into the domain `TenantId` value object and populates a scoped `ITenantContext`.
No other context reads `IdentityAccess` domain types directly.

---

## Testing Strategy

### BDD Pipeline

```
Speculate / Illustrate    →  Event Storming session
Formulate                 →  Reqnroll .feature files (Gherkin, Ubiquitous Language)
Automate                  →  Reqnroll step definitions via HttpClient (full stack)
Demonstrate               →  Passing tests = living documentation
Validate                  →  Aspire dashboard traces + real usage
```

### Automate: HttpClient against real Aspire host

Reqnroll step definitions call the real HTTP endpoints. No `EventFlow.TestHelpers`
Given/When/Then at the aggregate level — all tests go through the full stack:
YARP gateway → context service → EventFlow → PostgreSQL → read model projection.

```
[Given(@"a Product exists with a planned BacklogItem")]
public async Task GivenAProductWithBacklogItem()
{
    // POST /api/agile/products  →  seed Product
    // POST /api/agile/products/{id}/backlog-items  →  seed BacklogItem
}

[When(@"I commit the BacklogItem to a Sprint")]
public async Task WhenICommitToSprint()
{
    // POST /api/agile/backlog-items/{id}/commit
}

[Then(@"the Sprint reflects the committed BacklogItem")]
public async Task ThenSprintReflectsCommit()
{
    // GET /api/agile/sprints/{id}  →  assert read model
}
```

**Why full-stack only**: the scaffold is a distributed system — the interesting bugs
live in the wiring (RLS, MassTransit consumers, YARP routing, projection lag), not
inside individual aggregates. HttpClient tests catch those; aggregate unit tests do not.

### Seeding and Smoke Test

`IntegrationTests/TestSeedingScript.cs` runs as an Aspire resource after all services
pass health checks. It seeds one demo Tenant, one User per role, one Product with three
BacklogItems, one Sprint, and one Forum — via the same HttpClient pattern. It doubles
as a smoke test: if any HTTP call fails, the Aspire run fails.

### Feature File Location

Each context owns its tests. The `.Tests` project mirrors the aggregate-slice layout of
the `.Application` project: one folder per aggregate, one subfolder per use case. File
names are short — namespace provides the disambiguation (e.g.
`AgileProjectManagement.Tests.Products.Create`).

```
AgileProjectManagement/AgileProjectManagement.Tests/
├── Products/
│   ├── Create/
│   │   ├── Create.feature
│   │   ├── Steps.cs             # [Given]/[When]/[Then] bindings
│   │   └── StepContext.cs       # scenario-scoped shared state (IDs, responses)
│   ├── PlanBacklogItem/
│   │   ├── PlanBacklogItem.feature
│   │   ├── Steps.cs
│   │   └── StepContext.cs
│   └── ScheduleSprint/
│       ├── ScheduleSprint.feature
│       ├── Steps.cs
│       └── StepContext.cs
├── BacklogItems/
│   └── CommitToSprint/
│       ├── CommitToSprint.feature
│       ├── Steps.cs
│       └── StepContext.cs
├── Sprints/
│   └── ...
└── Shared/
    ├── ApiClient.cs             # typed HttpClient wrapper shared across slices
    └── AspireFixture.cs         # Aspire host setup / teardown

```

The same pattern applies to `Collaboration.Tests` and `IdentityAccess.Tests`.

`TestSeedingScript.cs` lives in `AppHost/` — shared seeding infrastructure, not part
of any single context's test suite.

---

## Flagged Ambiguities

These are open questions deferred to the upcoming Event Storming / Event Modeling session.
Do not write code against them until resolved.

- **BacklogItem creation event**: When the factory use case initialises the `BacklogItemAggregate`,
  what event does it emit — `BacklogItemPlanned` (same name as the Product factory event, different
  stream) or a distinct `BacklogItemInitialized`? Event Storming will settle the Ubiquitous Language
  term for this occurrence.

- **Sprint / Release factory events**: Same question applies for `SprintScheduled` and
  `ReleaseScheduled` — are these emitted on the Product stream, the child aggregate stream, or both?

- **Collaboration → AgileProjectManagement integration events**: Which business occurrences in
  `Collaboration` does `AgileProjectManagement` need to react to, and what are those event names?

- **Tenant onboarding saga steps and compensation**: Exact sequence of commands the
  `TenantOnboardingSaga` issues and which step is the pivot (point of no return).

---

## Infrastructure Summary

See `docs/adr/` for rationale behind each locked decision.

| Concern | Choice |
|---|---|
| Event store + read models | EventFlow + PostgreSQL (`EventFlow.PostgreSql`) |
| Messaging | RabbitMQ via MassTransit (swappable to Azure Service Bus in one line) |
| API gateway | YARP (`Gateway` project) with Aspire service discovery + per-tenant token bucket rate limiting |
| Auth | Keycloak (realm export committed, mounted at startup via Aspire bind mount) |
| Multi-tenancy isolation | PostgreSQL Row Level Security + `{tenantId}-` stream prefix |
| Frontend | React + Vite + TypeScript + TanStack Router/Query + Zustand |
| Primary deployment | Azure Container Apps via `azd up` |
| Secondary deployment | Docker Compose (generated from Aspire publish) |

### YARP Gateway Routes

| Route prefix | Upstream |
|---|---|
| `/api/agile/*` | `AgileProjectManagement` |
| `/api/collab/*` | `Collaboration` |
| `/api/identity/*` | `IdentityAccess` |
| `/auth/*` | Keycloak |
| `/` (fallback) | React SPA static files |

JWT validation against the Keycloak JWKS endpoint happens at the gateway layer; individual
services trust the validated token forwarded by YARP.

**Per-tenant rate limiting**: `RateLimiterMiddleware` with a `PartitionedRateLimiter` is
applied in the `Gateway` project, partitioned by the `tenant_id` JWT claim. Algorithm:
token bucket — allows short bursts (e.g., a page load firing several requests simultaneously)
while preventing sustained overuse by any single tenant. Unauthenticated requests fall back
to IP-based partitioning. This is the noisy-neighbour mitigation layer.
