# ADR-005: Keycloak for Authentication and Identity

**Status:** Accepted

## Context

The scaffold must demonstrate a working auth flow from first `dotnet run` without manual
configuration steps. The `IdentityAccess` bounded context is the Generic Subdomain for
identity — it must not embed a custom auth server.

## Decision

Use **Keycloak** as the OIDC provider. A committed `realm-export.json` is mounted into
the Keycloak Aspire container at startup via `WithBindMount`.

## Rationale

- Keycloak is a well-understood, self-hostable OIDC provider that pairs with Azure
  Container Apps and Docker Compose without licensing concerns.
- A committed realm export satisfies User Story 2 ("pre-configured via a committed realm
  export") — cloners get a working auth setup from first checkout.
- The `IdentityAccess` context manages `Tenant` and `User` aggregates in its own event
  store but delegates authentication mechanics to Keycloak. This preserves the Generic
  Subdomain boundary: identity lifecycle events live in `IdentityAccess`; token issuance
  lives in Keycloak.
- YARP validates JWTs against the Keycloak JWKS endpoint; no JWT validation code is
  scattered across context services.

## Realm Export Contents

The committed `realm-export.json` includes:
- One realm
- One OIDC client (the React SPA, using PKCE)
- One demo tenant group
- Three demo users: `admin`, `product-owner`, `developer`
- A `tenant_id` custom claim mapped from the user's group attribute

## Consequences

- The `tenant_id` claim in the JWT is the sole channel through which `IdentityAccess`
  communicates a user's tenant context to other services. The ACL in
  `AgileProjectManagement` reads this claim, not any `IdentityAccess` API.
- Keycloak cold-start time must be accounted for in Aspire `WaitFor` health checks before
  the seeding script runs.
