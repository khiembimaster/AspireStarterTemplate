# ADR-006: Row Level Security for Multi-Tenancy

**Status:** Accepted

## Context

Every aggregate must be isolated per tenant. Options: schema-per-tenant, database-per-
tenant, shared-schema with application-layer filtering, shared-schema with PostgreSQL Row
Level Security (RLS).

## Decision

Use **PostgreSQL Row Level Security** on EF Core read-model tables, with `SET app.tenant_id`
executed automatically via an **EF Core `DbConnectionInterceptor`** on every connection borrow.
The EventFlow event store is isolated by stream name prefix (`{tenantId}-`) instead of RLS.

## Rationale

- RLS enforcement happens in the database engine — an application bug cannot leak
  cross-tenant data even if the application layer has a defect.
- A `DbConnectionInterceptor` (`ConnectionOpenedAsync`) fires on every EF Core pool borrow,
  not just physical connection creation. This makes the tenant set automatic and impossible
  to forget — middleware, raw `ExecuteSqlRaw`, or manual call sites are not required.
- `SET app.tenant_id` (session-scoped, not `SET LOCAL`) is correct here. `SET LOCAL` is
  transaction-scoped: for reads without an explicit transaction it resets the moment the
  statement completes. Session-scoped `SET` persists for the borrowed connection's lifetime,
  which is exactly one request.
- The interceptor fires again on the next borrow, so the previous request's tenant value is
  always overwritten before any query runs — no tenant bleed under pooling.
- EventFlow's event store uses its own `NpgsqlConnection` (not EF Core's `DbContext`), so
  the interceptor does not cover it. This is intentional: event store isolation is handled
  entirely by the `{tenantId}-` stream name prefix and does not require RLS.
- Shared schema avoids N × 3 database provisioning complexity.

## Implementation

1. `DbInitializer.cs` in each context creates the RLS policy:
   ```sql
   ALTER TABLE ... ENABLE ROW LEVEL SECURITY;
   CREATE POLICY tenant_isolation ON ...
     USING (tenant_id = current_setting('app.tenant_id'));
   ```

2. A scoped `ITenantContext` service holds the current request's `TenantId`, populated
   once from the JWT claim in an ASP.NET Core middleware.

3. A `DbConnectionInterceptor` registered per context sets the variable on every borrow:
   ```csharp
   public class TenantConnectionInterceptor(ITenantContext tenantContext)
       : DbConnectionInterceptor
   {
       public override async Task ConnectionOpenedAsync(
           DbConnection connection,
           ConnectionEndEventData eventData,
           CancellationToken ct = default)
       {
           if (tenantContext.TenantId is { } tenantId)
           {
               await using var cmd = connection.CreateCommand();
               cmd.CommandText = $"SET app.tenant_id = '{tenantId.Value}'";
               await cmd.ExecuteNonQueryAsync(ct);
           }
       }
   }
   ```

4. Event store stream names are prefixed `{tenantId}-` at the EventFlow level; no RLS
   policy is applied to EventFlow's internal tables.

## Consequences

- Schema-per-tenant and database-per-tenant strategies are explicitly out of scope.
- Any raw `NpgsqlConnection` usage outside EF Core (e.g., Dapper, ADO.NET) must manually
  set `app.tenant_id`, or run under a superuser role with an explicit migration context.
- Migration runs (`dotnet ef database update`) must set the variable or bypass RLS via a
  superuser connection string.
