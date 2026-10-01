# Birdie Buddy architecture

This document describes the implementation in the repository as reviewed on 2026-10-01 and the boundaries contributors should preserve. Deployment verification is a separate gate. See [current-status.md](current-status.md) for feature and release status.

## System shape

BirdieBuddy has a SwiftUI iPhone client and one ASP.NET Core 10 application. Kestrel serves the controller API and the static mobile web client from `wwwroot`. Both clients share PostgreSQL as the server source of truth. Local outboxes and cached drafts improve live-round resilience but are not backups.

```text
Native iPhone app (SwiftUI, iOS 17+)
  ├─ Keychain access/refresh session
  ├─ user/round-scoped SwiftData outbox and conflict state
  ├─ Application Support draft snapshots
  └─ local MetricKit reports, shared only by user action
             │ bearer-authenticated HTTPS
             ▼
        shared controller API

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
5. Routing runs before `MobileBearerMiddleware`, which validates opaque access tokens and supplies the mobile principal.
6. API observability identifies route templates without logging query strings or member identifiers, then rate limiting runs.
7. Cookie authentication and authorization run before private controllers execute. `MobileAwareAntiforgeryFilter` exempts valid mobile bearer requests from browser CSRF validation.
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

This is a small custom cookie-and-token authentication system. Native-app tokens are implemented. Reassess ASP.NET Core Identity or an external identity provider before adding organizations, social login, passkeys, or delegated account administration.

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

Round completion and abandonment are terminal server transitions. Repeating the same transition after a lost response is safe. The server rejects completion until every expected tee hole is valid; clients also block completion while local writes or conflicts remain. The native UI supports completion, while draft abandonment is currently exposed only by the web client.

The native `RoundPersistenceStore` actor durably queues revisions in SwiftData before reporting a device save. `SyncCoordinator` processes them in revision order; a conflict pauses its round while other rounds can continue. Session restoration, sign-in, network recovery, foreground entry, live-round entry, and manual saves trigger synchronization. Cached draft snapshots are overlaid with pending local values when a round reopens. Successful account deletion removes only that user's local records; logout preserves them for later sign-in.

## Offline boundary

The service worker caches the scorecard application shell and static assets, but never intercepts `/api` requests. A draft can reopen offline only after:

1. the browser opened the scorecard online;
2. the shell and draft state were cached successfully; and
3. the same locally remembered account identity is still available.

Clearing site data can remove unsynchronized input. Multi-device offline merging is not supported; conflicting server writes require the existing per-hole review flow.

For iOS, draft creation requires the API. An already cached draft can reopen offline for the same restored account. **Save hole**, **Next hole**, or **Finish round** commits the current controls to the durable outbox; unsubmitted control changes have no force-quit persistence guarantee. Native offline storage is independent of the web service worker.

## Shared design and native scope

`BirdieTheme.swift` and `wwwroot/css/styles.css` define the shared palette and bundled Outfit, Space Grotesk, and DM Mono roles. The native app explicitly uses a light color scheme. The shared bird-on-tee identity is an outlined SVG on the web and a vector PDF in `BirdieLogo.imageset` on iOS; the app icon remains a flat PNG. Export sources and instructions live in [the brand guide](../design/brand/README.md).

The five native tabs are Courses, Rounds, Stats, Practice, and Account. Native statistics use Swift Charts with expandable data tables. Practice creation/history is implemented, but practice completion and guided drills, completed-hole editing, and draft abandonment remain native UI gaps. MetricKit diagnostics stay in Application Support (the latest ten reports) and leave the device only through user sharing; there is no automatic native crash-report upload or native product-event emitter.

## Background work and process-local state

The Golf NZ import coordinator and operational request counters are process-local singletons. The import reads the bundled JSON catalogue and is started through an authenticated admin endpoint protected by `X-Admin-Key`. Each run is also persisted in `GolfNzImportRuns` with a source SHA-256 version, counters, status and a safe failure message. Imported tees and holes are soft-retired with `IsActive=false` when they are absent from the full source snapshot; historical round snapshots are not deleted. A process restart still loses only the in-flight coordinator state and operational counters, not committed import history.

Before scaling horizontally:

- persist Data Protection keys on a shared volume or external store;
- replace process-local import coordination with durable job state and a distributed lock;
- use an external metrics backend for durable alerting;
- add an operator UI over the admin import-history endpoints if imports need to be managed without API tooling.

## Startup, migrations and health

Startup validates required deployment configuration before building the app. Pending EF Core migrations run under a PostgreSQL advisory lock by default. Production refuses to disable startup migrations. Review the generated idempotent SQL artifact before schema deployments.

- `/health/live` checks that the process can answer HTTP requests and deliberately excludes dependencies.
- `/health/ready` includes the database check and should gate traffic after deployments.

Production secrets belong in Render environment variables or another secret store. They must not be added to `appsettings*.json`.

## Known scaling and product limits

- Statistics overview aggregates round metrics and par-type trends in SQL, applies a one-year default lookback and a five-year maximum, and only loads holes for the recent evidence rounds used by practice insights. Query plans and latency should be measured as beta data grows; indexes or result caching are the next optimization levers.
- Golf NZ source versions and import outcomes are durable. Stale imported tees and holes are deactivated rather than deleted, while import coordination remains process-local until horizontal scaling is required.
- Operational counters and in-flight import coordinator state do not survive process restarts; committed import history does.
- Physical iPhone Safari and VoiceOver certification remains a manual release gate.
- Shot-by-shot tracking, social features, coach sharing, and payments remain outside the first native release. Native token authentication is already implemented.

See [testing.md](testing.md) for verification layers and [beta-operations.md](beta-operations.md) for release procedures.
