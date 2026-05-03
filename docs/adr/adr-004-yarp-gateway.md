# ADR-004: YARP as API Gateway

**Status:** Accepted

## Context

The frontend SPA needs a single origin for all API calls. Services must not be exposed
directly to the browser. JWT validation must happen in one place.

## Decision

Use **YARP** (Yet Another Reverse Proxy) in a dedicated `Gateway` project as the sole
entry point for the frontend. The `Gateway` project also serves the compiled React SPA
static files.

Service discovery is handled by Aspire: each upstream is declared via `WithReference()`
in `AppHost`, and YARP destinations use the service name as the address (e.g.
`http://agile-pm`). No hardcoded URLs — the same config works across dev, staging, and
production.

```csharp
// AppHost/AppHost.cs
var gateway = builder.AddProject<Projects.Gateway>("gateway")
    .WithReference(agilePm)
    .WithReference(collaboration)
    .WithReference(identityAccess)
    .WithReference(keycloak);
```

```json
// Gateway/appsettings.json (YARP route table)
"ReverseProxy": {
  "Routes": {
    "agile-route":        { "ClusterId": "agile-pm",      "Match": { "Path": "/api/agile/{**catch-all}" } },
    "collab-route":       { "ClusterId": "collaboration",  "Match": { "Path": "/api/collab/{**catch-all}" } },
    "identity-route":     { "ClusterId": "identity-access","Match": { "Path": "/api/identity/{**catch-all}" } },
    "auth-route":         { "ClusterId": "keycloak",       "Match": { "Path": "/auth/{**catch-all}" } }
  },
  "Clusters": {
    "agile-pm":       { "Destinations": { "d1": { "Address": "http://agile-pm" } } },
    "collaboration":  { "Destinations": { "d1": { "Address": "http://collaboration" } } },
    "identity-access":{ "Destinations": { "d1": { "Address": "http://identity-access" } } },
    "keycloak":       { "Destinations": { "d1": { "Address": "http://keycloak" } } }
  }
}
```

## Rationale

- YARP is a first-class ASP.NET Core library — no separate process, no container, no
  Nginx config file to maintain.
- JWT validation against the Keycloak JWKS endpoint is configured once at the gateway;
  individual services receive a pre-validated token forwarded by YARP and do not re-verify.
- Aspire service discovery resolves destination addresses automatically — `http://agile-pm`
  maps to the correct host:port in every environment without environment-specific config.
- Keeping `Gateway` as a regular .NET project (rather than the `Aspire.Hosting.Yarp`
  container image) preserves full control over middleware: JWT validation, rate limiting,
  and SPA serving stay in one owned project.

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
  flows through `Gateway`.
- Adding a fourth context requires a new route + cluster entry in `appsettings.json` and
  a `WithReference()` call in `AppHost` — two lines total.
- gRPC inter-service calls (if ever introduced) bypass the gateway; this ADR covers only
  the browser-facing entry point.
