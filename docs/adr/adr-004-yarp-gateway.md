# ADR-004: YARP as API Gateway

**Status:** Accepted

## Context

The frontend SPA needs a single origin for all API calls. Services must not be exposed
directly to the browser. JWT validation must happen in one place.

## Decision

Use **YARP** (Yet Another Reverse Proxy) in the `Server` project as the sole entry point
for the frontend. The `Server` project also serves the compiled React SPA static files.

## Rationale

- YARP is a first-class ASP.NET Core library — no separate process, no container, no
  Nginx config file to maintain.
- JWT validation against the Keycloak JWKS endpoint is configured once at the gateway;
  individual services receive a pre-validated token forwarded by YARP and do not re-verify.
- The `Server` project is already in the scaffold and already serves static files — adding
  YARP routing keeps the project count stable.
- Aspire's `PublishWithContainerFiles` bundles the compiled frontend into the `Server`
  container, making the SPA and the gateway a single deployable unit.

## Route Table

| Prefix | Upstream |
|---|---|
| `/api/agile/*` | `AgileProjectManagement` service |
| `/api/collab/*` | `Collaboration` service |
| `/api/identity/*` | `IdentityAccess` service |
| `/auth/*` | Keycloak container |
| `/` | React SPA static files (fallback) |

## Consequences

- Individual context services are not reachable from the browser directly; all traffic
  flows through `Server`.
- Adding a fourth context requires adding a YARP route entry — a one-line config change.
- gRPC inter-service calls (if ever introduced) bypass YARP; this ADR covers only the
  browser-facing gateway.
