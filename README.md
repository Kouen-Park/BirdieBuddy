# Birdie Buddy

Birdie Buddy is a mobile-first golf round tracker and performance notebook. It uses ASP.NET Core 8, EF Core 8, PostgreSQL, cookie authentication, and a vanilla HTML/CSS/JavaScript frontend served from `wwwroot`.

The repository currently targets a small English-language mobile-web beta. The priority is reliable round completion and recovery in poor-connectivity golf-course conditions; native iOS authentication, social features, payments and shot-by-shot tracking are intentionally outside the current scope.

## Documentation map

- [Architecture](docs/architecture.md): request pipeline, authentication, ownership, round-service boundaries, offline state and scaling limits
- [Testing and CI](docs/testing.md): every test layer, local PostgreSQL setup, Playwright artifacts and CI responsibilities
- [Beta operations](docs/beta-operations.md): release gates, SMTP rollout, backup rehearsal, monitoring and physical iPhone checks
- [Browser fixture guide](tests/browser/README.md): deterministic local fixture URLs and conflict scenarios
- [Physical iPhone Safari checklist](tests/browser/iphone-safari-checklist.md): required pre-release WebKit and VoiceOver checks

## Features

- Private member accounts with secure password hashing, profile/password management, data export, and account deletion
- Shared Golf New Zealand courses plus member-owned custom courses
- On-course live scorecard with draft, auto-save, offline queue, resume, complete, and abandon flows
- Completed-scorecard entry, round history, edit/delete, and per-hole breakdowns
- 9/18-hole aware statistics, score/GIR/putting trends, and evidence-based practice priorities
- CSRF protection, login rate limiting, security headers, ownership checks, and admin-protected imports
- Liveness/readiness checks, migration locking, automated tests, and GitHub Actions CI

## Architecture at a glance

The application is deployed as one ASP.NET Core process. It serves the static frontend and authenticated controller API, while EF Core/Npgsql persists data in PostgreSQL. Browser `localStorage` holds a user-and-round-scoped live-entry outbox, and the service worker caches only the application shell; PostgreSQL remains the durable source of truth.

The round domain is split behind the stable `IRoundService` controller contract:

| Component | Responsibility |
|---|---|
| `RoundService` | Thin compatibility facade used by controllers |
| `RoundQueryService` | Lists, selectors, pagination and round detail reads |
| `LiveRoundService` | Draft creation and live hole writes |
| `RoundLifecycleService` | Completed-scorecard creation, completion and abandonment |
| `CompletedRoundEditor` | Completed-round metadata/hole edits and deletion |
| `RoundRules` | Shared validation, tee resolution and DTO mapping |

All feature services are scoped with the request's `ApplicationDbContext` and current user. Ownership filters are applied in service queries, not inferred from browser-submitted IDs. See [the architecture document](docs/architecture.md) for the full request and persistence model.

### Practice rules

Practice uses completed rounds with exactly 9 or 18 recorded holes (minimum 3 rounds); historical partial rounds do not unlock recommendations. Priorities include hole-weighted putting, GIR, penalties per 18 holes, and recorded non-par-3 fairways. Each priority links its supporting scorecards and a short drill with a measurable target. These are product heuristics, not validated coaching diagnoses; practice outcomes are not stored yet.

Par-type GIR comparisons require 6 rounds: latest 3 versus previous 3, at least 12 holes of the par type in each window, and a decline of at least 15 percentage points. Dates, numerator/denominator and thresholds are shown. Different courses/tees can affect results; no causal claim is made. Insufficient data produces guidance rather than an inferred weakness.

iPhone Safari certification is pending; see `tests/browser/iphone-safari-checklist.md` for prerequisites and release checks. Mobile viewport tests do not certify iOS browser behavior.

### Account data and deletion

- **Download my data** exports the signed-in member's profile, custom courses, scorecards, and hole records as JSON. Password hashes and salts are never included.
- Account deletion requires the current password and the exact confirmation phrase `DELETE MY ACCOUNT`. The server signs out the session and permanently removes hole records, rounds, member-owned courses, and the account in one PostgreSQL transaction.
- After successful deletion, the browser removes only live-round drafts scoped to that member. The unscoped legacy queue remains untouched because its owner cannot be established safely.

## Local development

### Draft conflict recovery and PostgreSQL CI

- A live draft that receives HTTP 409 now shows **Review save conflict**. The comparison lists device and server values. **Use server record** discards only the reviewed local hole revision; **Keep my input & retry** preserves the input and retries with the reviewed server snapshot as its precondition. A further server change can conflict again. Cancel keeps the queue untouched.
- Account identity is checked before reviewing and applying a choice. A changed local revision or a terminal server round blocks resolution without discarding data. This comparison currently covers live drafts, not the completed-round editor or recovery of drafts already completed/abandoned elsewhere.
- CI has a separate `postgres-integration` job with a disposable PostgreSQL 16 service. It applies the real migrations and checks lifecycle, ownership, duplicate-hole constraints, stale snapshots, transaction rollback on concurrent updates, and a statistics query. Configure this job as a required check in repository branch protection separately; this workflow does not change repository settings or Render's deployment policy.
- The PostgreSQL job first stops at the pre-release migration, inserts an existing member, tee and historical scorecard, then applies the latest migration and verifies that the records and relationships survive. The build job also publishes an idempotent `postgres-migration-sql` artifact so schema changes can be reviewed before a Render deployment.
- To run the PostgreSQL test locally, create a disposable database whose name starts with `birdiebuddy_test_` on localhost and set `BIRDIEBUDDY_TEST_POSTGRES` to its Npgsql connection string. Then run `dotnet test tests/BirdieBuddy.Tests/BirdieBuddy.Tests.csproj --filter Category=PostgreSQL`. Never use a production connection. Tests apply migrations and insert test records; they do not delete or recreate databases. Without the environment variable the PostgreSQL test is explicitly skipped; other tests still run.

### Record editing and comparable statistics

- Completed scorecards provide an **Edit** action per hole for score, putts, GIR, fairway and penalties. Par/course snapshots remain unchanged. The web editor sends the original hole snapshot; stale edits return HTTP 409 and retain the form input. Legacy clients that omit `checkExpected` retain their existing update behavior.
- Dashboard and Statistics compare raw scores only within the same recorded round length. The summary shows average/best scores, average score to par and last-5/last-10 score-to-par averages. Each recent window requires that many rounds of the same length; these are recent-window summaries, not a full rolling-average chart.
- Overall GIR and fairway rates are hole-weighted; historical par-3 fairways are excluded. Putting and score trend charts use per-hole units. Empty completed rounds are excluded. Historical partial completed rounds remain visible under their actual recorded length.
- Statistics supports course, tee, 9/18-hole and inclusive date filters. Course/tee filtering does not change the independent individual-round selector below it.
- Statistics overview uses a SQL projection for round-level aggregates, defaults to the last 365 days, and caps the selectable range at five years. The detailed hole query is limited to the selected round and the recent evidence rounds used for practice insights.
- Existing total-based API fields remain for compatibility; new clients should use `byRoundLength`, `averagePuttsPerHole`, `scoreToParPerHoleTrend` and `puttsPerHoleTrend` for comparisons. No schema migration is required for these changes.
- The local browser fixture also provides `/round-details.html?id=2` for manual hole-edit testing. It is an in-memory mock, not a PostgreSQL end-to-end test.

Requirements:

- .NET 8 SDK
- PostgreSQL 16 for application development and relational integration tests
- Node.js 22 or newer plus npm for browser-module and Playwright tests

An example local development connection is:

```text
Host=localhost;Port=5432;Database=birdiebuddy;Username=postgres;Password=postgres
```

No connection string is committed in the shared settings. Configure it without committing credentials:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=birdiebuddy;Username=YOUR_USER;Password=YOUR_PASSWORD"
dotnet restore
dotnet run
```

The app validates that `ConnectionStrings:DefaultConnection` is present before startup. Use .NET Secret Manager locally and deployment environment variables in hosted environments; never commit working credentials to `appsettings*.json`.

Pending migrations run at startup under a PostgreSQL advisory lock. Swagger is available at `/swagger` in Development. Health endpoints are `/health/live` and `/health/ready`.

Chart.js 4.4.4 and the DM Mono, Outfit, and Space Grotesk fonts are served from `wwwroot/vendor`; production pages do not depend on a third-party CDN. Upstream license texts are kept beside those assets. The Content Security Policy permits scripts and fonts only from this application. Inline styles remain allowed because Chart.js applies responsive canvas dimensions at runtime.

Every API response includes `X-Trace-Id`. Structured completion logs record the HTTP method, route template, status code, duration, and trace ID without query strings or member identifiers. Authenticated administrators can inspect process-local request counts, HTTP 5xx failure rates, and average/maximum latency with `GET /api/admin/operations` and the existing `X-Admin-Key`. These counters reset whenever the Render instance restarts and complement, rather than replace, durable external monitoring.

The live scorecard records two minimal beta events: a draft being reopened and the elapsed time from rendering a hole to pressing its save action. Events contain a random idempotency ID, event type, owned round ID, optional duration, and server timestamp; they contain no score values, email, device fingerprint, or free text. `GET /api/admin/operations/beta` (authenticated plus `X-Admin-Key`) reports the last 30 days by default and accepts an optional UTC `from` value up to one year ago. Completion rate uses terminal rounds (`Completed / (Completed + Abandoned)`); resume completion rate is the percentage of distinct resumed rounds that are currently completed. Product events are included in member data exports and permanently removed with the account.

Run the tests with:

```bash
dotnet test tests/BirdieBuddy.Tests/BirdieBuddy.Tests.csproj
npm ci
npm run test:browser
npx playwright install chromium
npm run test:e2e:fixture
```

The HTTP integration suite uses `WebApplicationFactory<Program>` so authentication, CSRF,
rate limiting, security headers, readiness, and error responses run through the real application
pipeline. It replaces PostgreSQL with an isolated in-memory database and disables startup
migrations only in the Development test host; production configuration rejects that override.
The Playwright fixture project runs the mobile scorecard, offline recovery, conflict dialog, and
keyboard checks in a real Chromium browser. CI additionally sets `BIRDIEBUDDY_E2E_POSTGRES` and
runs the registration-to-round-completion journey against the real ASP.NET Core application and a
disposable PostgreSQL service. A local PostgreSQL-backed run can use `npm run test:e2e:app` with the
same environment variable.

Live counter controls are at least 56px for primary score/putt actions and 48px for compact actions.
The status region distinguishes device-saved, syncing, offline, server-saved, conflict and error
states. Dashboard Chart.js trends include expandable data tables containing the same date/value
series, and moving-average bars use CSS classes rather than per-element inline styles. Offline boot
failures explain how to reconnect and create a cached copy without deleting browser data.

Physical iPhone Safari and VoiceOver remain a manual release gate. Chromium emulation checks layout,
keyboard activation and browser behavior, but cannot certify iOS storage eviction, WebKit focus,
VoiceOver announcements or home-indicator clearance.

For the exact test matrix, disposable database safeguards, failure artifacts and full application E2E command, see [docs/testing.md](docs/testing.md).

## Repository layout

| Path | Purpose |
|---|---|
| `Controllers/` | Authenticated HTTP API endpoints and status-code mapping |
| `DTOs/` | Validated browser-facing request and response contracts |
| `Services/` | Authentication, courses, round slices, statistics, practice and import workflows |
| `Infrastructure/` | ProblemDetails, security headers, antiforgery, session validation, observability and deployment validation |
| `Data/`, `Models/`, `Migrations/` | EF Core context, persistence entities and PostgreSQL schema history |
| `wwwroot/` | Static mobile UI, service worker, local outbox and self-hosted assets |
| `tests/BirdieBuddy.Tests/` | Service, HTTP pipeline and PostgreSQL integration tests |
| `tests/browser/` | DOM-free JavaScript tests and deterministic mock API fixture |
| `tests/e2e/` | Playwright mobile fixture and real application journeys |
| `scripts/` | Deployment smoke check, SMTP probe, backup rehearsal and Golf NZ source data |

## Configuration reference

Environment variables use ASP.NET Core's double-underscore notation for nested keys.

| Variable | Required | Purpose |
|---|---:|---|
| `ConnectionStrings__DefaultConnection` | Yes | Npgsql connection string used by EF Core and readiness checks |
| `ASPNETCORE_ENVIRONMENT` | Hosted | Use `Production` for deployed instances |
| `Application__PublicBaseUrl` | Email flows | HTTPS origin used to generate verification/reset links |
| `Authentication__RequireVerifiedEmail` | No | Enforces verified-email login; defaults to `false` |
| `Email__From` | Email flows | Sender address; must be configured with SMTP host |
| `Email__Smtp__Host` | Email flows | SMTP server; required when verification enforcement is enabled |
| `Email__Smtp__Port` | No | SMTP port, normally `587` |
| `Email__Smtp__Username` / `Email__Smtp__Password` | Provider-specific | SMTP credentials stored as deployment secrets |
| `Administration__ImportKey` | Admin import | Secret sent as `X-Admin-Key` to import/operations endpoints |
| `DataProtection__KeyRingPath` | Multi-instance | Persistent shared key directory for authentication-cookie decryption |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Observability | HTTPS OTLP destination; loopback HTTP is allowed for a local collector |
| `OTEL_EXPORTER_OTLP_HEADERS` | Provider-specific | Secret exporter headers or API token |
| `Database__ApplyMigrationsOnStartup` | No | Defaults to `true`; production startup rejects `false` |
| `RateLimiting__AuthPermitLimit` | No | Authentication request allowance per fixed one-minute window; defaults to `8` |

Production validates the database setting, HTTPS URLs, SMTP pairs and migration policy before serving traffic.

Generate the same reviewable migration script locally with:

```bash
dotnet tool restore
dotnet ef migrations script --idempotent --configuration Release --output artifacts/migrations.sql
```

Review the SQL and test it only against a recent disposable copy before enabling a schema deployment. The command generates SQL; it does not connect to or modify a database.

After a Render deploy, run the public smoke checks against the service URL. The script verifies both health endpoints, the application-only CSP, and the self-hosted Chart.js asset:

```bash
./scripts/check-deployment.sh https://your-service.onrender.com
```

It performs read-only requests and does not require an authenticated account.

The complete release, SMTP rollout, backup rehearsal, monitoring, private-beta, and physical iPhone Safari procedure is documented in [`docs/beta-operations.md`](docs/beta-operations.md). Operational scripts deliberately refuse unsafe restore targets and never store credentials in the repository.

## Render deployment

The included Dockerfile listens on port `10000`. Configure these Render environment variables:

- `ASPNETCORE_ENVIRONMENT=Production`
- `ConnectionStrings__DefaultConnection=<Render PostgreSQL internal connection string>`
- `Administration__ImportKey=<long random secret>` when the Golf NZ admin import endpoint is required
- `Application__PublicBaseUrl=https://your-service.onrender.com`
- `Authentication__RequireVerifiedEmail=true` after SMTP delivery has been configured and tested
- `Email__From`, `Email__Smtp__Host`, `Email__Smtp__Port`, `Email__Smtp__Username`, and `Email__Smtp__Password` for verification and password-reset delivery
- `OTEL_EXPORTER_OTLP_ENDPOINT` to enable vendor-neutral external traces, metrics, and logs
- `OTEL_EXPORTER_OTLP_HEADERS` when the selected OTLP provider requires an API key or authorization header
- `DataProtection__KeyRingPath` pointing to a persistent shared volume before scaling beyond one app instance

Do not put production credentials in an appsettings file. SMTP defaults to TLS on port 587. Email verification enforcement defaults to off; production startup refuses to enable it unless SMTP host/from are both configured. When enabled, new accounts receive a verification link and remain signed out until verified; correct-password login requests for an unverified account send a fresh link and return `auth.email_unverified`. Existing accounts are marked verified by the rollout migration so they are not locked out. Confirm `/health/ready` after each deploy and retain automated PostgreSQL backups. Before a schema deployment, test the migration against a recent database copy and document the restore point.

OpenTelemetry export is disabled when `OTEL_EXPORTER_OTLP_ENDPOINT` is absent. It uses the standard OTLP environment variables, so the same build can send to Grafana Cloud, Honeycomb, an OpenTelemetry Collector, or another compatible provider. Only `/api` requests are traced; static reset and verification URLs are excluded so their query-string tokens are not exported. Keep `OTEL_EXPORTER_OTLP_HEADERS` in Render secrets rather than source control.

Production startup validates the database connection, HTTPS public/OTLP URLs, and complete SMTP host/from pairs before applying migrations. CI now publishes the Release artifact and builds the actual Docker image in addition to running server, PostgreSQL, and browser tests.

## Live-round API

All endpoints except registration, login, CSRF initialization, and health checks require authentication. Browser mutation requests must first obtain `/api/security/csrf` and send its token in `X-CSRF-TOKEN`.

API failures use `application/problem+json` with `type`, `title`, `status`, stable `code`, `detail`, `instance`, and `traceId`; automatic model-validation failures additionally include field-level `errors`. Clients should branch on `code`, not English error text.

```text
POST /api/rounds/drafts
PUT  /api/rounds/{roundId}/holes/by-number/{holeNumber}
POST /api/rounds/{roundId}/complete
POST /api/rounds/{roundId}/abandon
GET  /api/rounds/page?status=Draft&limit=20&courseId=&from=&to=&holeCount=
GET  /api/rounds/options?limit=100
```

The hole upsert route is idempotent for `(roundId, holeNumber)`. A round is one of `Draft`, `Completed`, or `Abandoned`; only drafts accept live hole updates. The legacy all-at-once `POST /api/rounds` remains supported.

### Live entry reliability

- Each input change is stored locally immediately, then synchronized after a 600ms debounce. Previous/next navigation preserves pending changes.
- Device drafts are scoped by user and round. Requests are serialized; newer edits made during a save remain queued. Web Locks coordinate synchronization between supporting browser tabs.
- Replays check the signed-in user and submit `checkExpected: true` with the previously read `expectedHole` (or `null` for an unrecorded hole). Conflicts return HTTP 409 and keep local data instead of overwriting the server.
- EF uses the existing `UpdatedAt` column as a concurrency token. This mapping change does not require a new database column. Clients that omit `checkExpected` keep the older unconditional update contract; migrate them to the guarded contract.
- Completion and abandonment require an empty successfully synchronized outbox. Actual tee hole numbers are used, including back-nine layouts numbered 10–18; completed scorecards must match the whole selected layout. Repeating completion or abandonment after a lost response is safe for the same terminal state.
- The old unscoped `birdiebuddy.liveQueue` is deliberately not replayed or deleted because its owning account is unknown. Preserve it for manual recovery if it contains unsynced historical input.
- The live scorecard registers a small PWA shell and service worker so the scorecard page, local outbox, and required assets can reopen when connectivity drops. It is still not a data backup: the browser must have opened the shell once while online, and the previously verified account identity is required to resume an offline draft.

For a manual UI smoke check without real accounts or a database, run `node tests/browser/smoke-server.cjs` and open `http://127.0.0.1:4173/live-round.html?id=1`. This fixture uses in-memory mock API responses; it does not replace the real HTTP/backend tests.

## Golf NZ catalogue administration

The import is idempotent and reads `scripts/golf_nz_courses.json`, which is included in published output. Import controls are intentionally absent from the member UI. An authenticated administrator may call the protected endpoints with `X-Admin-Key` matching `Administration__ImportKey`:

```text
POST /api/courses/import-golf-nz
GET  /api/courses/import-golf-nz/status
```

The import is recorded durably in `GolfNzImportRuns`, including the source SHA-256 version, counters, completion status and a safe failure message. Use the operations endpoints to inspect history:

```text
GET /api/admin/operations/imports?limit=20
GET /api/admin/operations/imports/{id}
```

Both operations endpoints require the same `X-Admin-Key`. Imported tees and holes that disappear from a full source snapshot are retained but marked inactive, so historical rounds remain intact while course selection excludes stale records. The import job's in-process lock still permits only one active import per application instance; distributed locking remains a pre-horizontal-scaling task.

## Data model notes

- `Course → CourseTee → CourseHole` stores tee-specific rating, slope, distance, par, and stroke index.
- `Round → Hole` snapshots par so later course edits cannot change historical scoring.
- `Round.UserId` and member-owned `Course.UserId` enforce tenant ownership in every service query.
- `Round.Status`, timestamps, current hole, and the unique `(RoundId, HoleNumber)` index support resumable live entry.
- Fairway percentage excludes holes where fairway is not applicable; draft and abandoned rounds are excluded from career statistics.

## Current product boundary

This release targets a small English-language beta and validates mobile web scoring before an iOS client is started. Email verification and password-reset delivery are implemented but require production SMTP configuration; verification enforcement is opt-in until delivery is confirmed. Shot-by-shot tracking, social features, coach sharing, payments, and token-based iOS authentication remain future work.
