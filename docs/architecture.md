# Birdie Buddy architecture

This document describes the current beta architecture and the boundaries that contributors should preserve. It is a description of the code as deployed today, not a promise that every component is ready for large-scale use.

## System shape

Birdie Buddy is one ASP.NET Core 10 application. Kestrel serves both the controller API and the static mobile web client from `wwwroot`. PostgreSQL is the durable source of truth. Browser storage and the service-worker cache improve live-round resilience but are not backups.

```text
Mobile browser
  ├─ static HTML/CSS/JavaScript
  ├─ service-worker application shell
  └─ user/round-scoped local outbox
             │ cookie + CSRF-protected HTTPS
             ▼
ASP.NET Core application
  ├─ authentication, rate limiting, security headers
  ├─ controller API and ProblemDetails error contract
  ├─ scoped domain/application services
  ├─ health checks, logs, metrics and traces
  └─ EF Core 10 / Npgsql
             │
             ▼
        PostgreSQL 16
```

There is no separate frontend build pipeline. The browser modules are committed source files and are served directly. Chart.js and fonts are self-hosted under `wwwroot/vendor` so production pages do not depend on a public CDN.

## Request pipeline

`Program.cs` uses the modern `WebApplicationBuilder` hosting model. The important request order is:

1. Forwarded headers are processed before redirects and authentication.
2. Security headers and centralized exception handling are enabled.
3. Production uses HSTS; all environments use HTTPS redirection where an HTTPS port is available.
4. Default and static files are served.
5. Routing and API observability identify route templates without logging query strings or member identifiers.
6. Rate limiting runs before authentication endpoints execute.
7. Cookie authentication and authorization protect private controllers.
8. Controllers and liveness/readiness endpoints are mapped.

The API uses controller classes with `[ApiController]`, attribute routing and authorization at the HTTP boundary. Invalid DTOs and domain failures use one `application/problem+json` contract. Clients should branch on the stable `code` field and treat the English `detail` as display text.

Native clients use the additive `/api/mobile/auth` contract. `POST /session` issues a 15-minute opaque access token and a 30-day refresh token; `POST /refresh` rotates the refresh token and issues a new pair; `POST /revoke` invalidates active mobile tokens. Access tokens are sent as `Authorization: Bearer <token>`. The token hashes reuse `AccountTokens`, and credential changes increment `SessionVersion` as they do for browser sessions. Browser cookie endpoints remain unchanged.

The landing page, authentication pages, and shared course catalogue are public. Round recording, statistics, practice history, account data, member-owned courses, and all mutation endpoints remain authenticated. The browser treats both `/` and `/index.html` as the same public landing surface so default-file rewriting cannot accidentally trigger a sign-in redirect.

## Authentication and session security

- Authentication uses an HTTP-only `birdiebuddy.auth` cookie with `SameSite=Lax`.
- Browser mutations require the antiforgery token returned by `GET /api/security/csrf` in the `X-CSRF-TOKEN` header.
- Authentication endpoints have a fixed-window per-IP rate limit.
- Passwords use PBKDF2-SHA256 with a random per-account salt and 120,000 iterations.
- Password reset and email verification tokens are stored as hashes, expire, and are single-use.
- Every authentication cookie contains the account `SessionVersion`. Password change and password reset increment that version, causing previously issued cookies to be rejected.
- Registration and product telemetry rely on database unique constraints as the final race-safe duplicate guard.

This is a deliberately small custom cookie-authentication system. Reassess ASP.NET Core Identity or an external identity provider before adding organizations, social login, native-app tokens, passkeys, or delegated account administration.

## Ownership and data boundaries

Every round, practice session and product event is owned by one user. Custom courses can also be user-owned; imported Golf NZ courses have no owner and are shared. Service queries apply ownership filters rather than trusting identifiers supplied by the browser.

Important relational guarantees include:

- unique normalized user email;
- unique telemetry event ID within a user;
- unique hole number within a tee and within a round;
- check constraints for hole number and non-negative score components;
- cascade deletion from users to account tokens, events, rounds and custom courses;
- restricted deletion from a course or tee while historical rounds reference it;
- indexes for user/status/date and user/course/date round queries.

Account deletion explicitly removes dependent score data before the account inside a relational transaction. This ordering preserves the restricted course relationship while keeping the operation atomic on PostgreSQL.

## Round service boundaries

`IRoundService` remains the controller-facing compatibility contract. `RoundService` is now a small facade that delegates to four scoped feature services sharing the request's `ApplicationDbContext` and `ICurrentUser`:

| Service | Responsibility |
|---|---|
| `RoundQueryService` | Round lists, selectors, pagination, detail and hole reads |
| `LiveRoundService` | Draft creation, hole-number upsert and legacy draft-hole insertion |
| `RoundLifecycleService` | Completed-scorecard creation, draft completion and abandonment |
| `CompletedRoundEditor` | Round metadata edits, completed-hole edits and deletion |
| `RoundRules` | Shared tee resolution, expected-hole rules, validation and DTO mapping |

Controllers should continue to depend on `IRoundService` until an endpoint group is deliberately migrated to a narrower feature interface. New round behavior belongs in the relevant feature service; do not grow the facade back into an implementation class.

## Live-round durability and concurrency

The PostgreSQL `Round` row is the server-side concurrency boundary. `UpdatedAt` is an EF Core concurrency token, and `(RoundId, HoleNumber)` is unique.

In the browser, `LiveDraftStore` keeps one local state document per user and round:

```text
input change
  → write local outbox revision
  → show "saved on this device"
  → verify signed-in user
  → send expected server snapshot + new value
  → server accepts, or returns 409 conflict
  → remove only the acknowledged local revision
```

New input typed while a request is in flight remains queued. Supporting browsers use Web Locks to coordinate flushes across tabs. A 409 response preserves the device value and opens an explicit comparison. The user can adopt the server record or rebase the local value onto the reviewed server snapshot.

Round completion and abandonment are terminal transitions. Repeating the same transition after a lost response is safe. Completion is rejected until every expected tee hole is valid and the browser outbox has synchronized.

## Offline boundary

The service worker caches the scorecard application shell and static assets, but never intercepts `/api` requests. A draft can reopen offline only after:

1. the browser opened the scorecard online;
2. the shell and draft state were cached successfully; and
3. the same locally remembered account identity is still available.

Clearing site data can remove unsynchronized input. Multi-device offline merging is not supported; conflicting server writes require the existing per-hole review flow.

## Background work and process-local state

The Golf NZ import coordinator and operational request counters are process-local singletons. The import reads the bundled JSON catalogue and is started through an authenticated admin endpoint protected by `X-Admin-Key`. Each run is also persisted in `GolfNzImportRuns` with a source SHA-256 version, counters, status and a safe failure message. Imported tees and holes are soft-retired with `IsActive=false` when they are absent from the full source snapshot; historical round snapshots are not deleted. A process restart still loses only the in-flight coordinator state and operational counters, not committed import history.

Before scaling horizontally:

- persist Data Protection keys on a shared volume or external store;
- replace process-local import coordination with durable job state and a distributed lock;
- use an external metrics backend for durable alerting;
- move import coordination to a distributed lock when more than one application instance is deployed;
- retain an external durable metrics backend for operational counters and alerting;
- add an operator UI over the admin import-history endpoints if imports need to be managed without API tooling.

## Startup, migrations and health

Startup validates required deployment configuration before building the app. Pending EF Core migrations run under a PostgreSQL advisory lock by default. Production refuses to disable startup migrations. Review the generated idempotent SQL artifact before schema deployments.

- `/health/live` checks that the process can answer HTTP requests and deliberately excludes dependencies.
- `/health/ready` includes the database check and should gate traffic after deployments.

Production secrets belong in Render environment variables or another secret store. They must not be added to `appsettings*.json`.

## Known scaling and product limits

- Statistics overview aggregates round metrics and par-type trends in SQL, applies a one-year default lookback and a five-year maximum, and only loads holes for the recent evidence rounds used by practice insights. Query plans and latency should be measured as beta data grows; indexes or result caching are the next optimization levers.
- Golf NZ source versions and import outcomes are durable. Stale imported tees and holes are deactivated rather than deleted, while import coordination remains process-local until horizontal scaling is required.
- Operational counters and import job state do not survive process restarts.
- Physical iPhone Safari and VoiceOver certification remains a manual release gate.
- The product intentionally excludes shot-by-shot tracking, social features, payments and native iOS token authentication until the mobile-web beta meets its completion and resume targets.

See [testing.md](testing.md) for verification layers and [beta-operations.md](beta-operations.md) for release procedures.
