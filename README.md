# Birdie Buddy

Birdie Buddy is a mobile-first golf round tracker and performance notebook. It uses ASP.NET Core 8, EF Core 8, PostgreSQL, cookie authentication, and a vanilla HTML/CSS/JavaScript frontend served from `wwwroot`.

## Features

- Private member accounts with secure password hashing, profile/password management, data export, and account deletion
- Shared Golf New Zealand courses plus member-owned custom courses
- On-course live scorecard with draft, auto-save, offline queue, resume, complete, and abandon flows
- Completed-scorecard entry, round history, edit/delete, and per-hole breakdowns
- 9/18-hole aware statistics, score/GIR/putting trends, and evidence-based practice priorities
- CSRF protection, login rate limiting, security headers, ownership checks, and admin-protected imports
- Liveness/readiness checks, migration locking, automated tests, and GitHub Actions CI

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
- To run the PostgreSQL test locally, create a disposable database whose name starts with `birdiebuddy_test_` on localhost and set `BIRDIEBUDDY_TEST_POSTGRES` to its Npgsql connection string. Then run `dotnet test tests/BirdieBuddy.Tests/BirdieBuddy.Tests.csproj --filter Category=PostgreSQL`. Never use a production connection. Tests apply migrations and insert test records; they do not delete or recreate databases. Without the environment variable the PostgreSQL test is explicitly skipped; other tests still run.

### Record editing and comparable statistics

- Completed scorecards provide an **Edit** action per hole for score, putts, GIR, fairway and penalties. Par/course snapshots remain unchanged. The web editor sends the original hole snapshot; stale edits return HTTP 409 and retain the form input. Legacy clients that omit `checkExpected` retain their existing update behavior.
- Dashboard and Statistics compare raw scores only within the same recorded round length. The summary shows average/best scores, average score to par and last-5/last-10 score-to-par averages. Each recent window requires that many rounds of the same length; these are recent-window summaries, not a full rolling-average chart.
- Overall GIR and fairway rates are hole-weighted; historical par-3 fairways are excluded. Putting and score trend charts use per-hole units. Empty completed rounds are excluded. Historical partial completed rounds remain visible under their actual recorded length.
- Statistics supports course, tee, 9/18-hole and inclusive date filters. Course/tee filtering does not change the independent individual-round selector below it.
- Existing total-based API fields remain for compatibility; new clients should use `byRoundLength`, `averagePuttsPerHole`, `scoreToParPerHoleTrend` and `puttsPerHoleTrend` for comparisons. No schema migration is required for these changes.
- The local browser fixture also provides `/round-details.html?id=2` for manual hole-edit testing. It is an in-memory mock, not a PostgreSQL end-to-end test.

Requirements: .NET 8 SDK and PostgreSQL.

The default development connection is:

```text
Host=localhost;Port=5432;Database=birdiebuddy;Username=postgres;Password=postgres
```

Override it without committing credentials:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=birdiebuddy;Username=YOUR_USER;Password=YOUR_PASSWORD"
dotnet restore
dotnet run
```

Pending migrations run at startup under a PostgreSQL advisory lock. Swagger is available at `/swagger` in Development. Health endpoints are `/health/live` and `/health/ready`.

Every API response includes `X-Trace-Id`. Structured completion logs record the HTTP method, route template, status code, duration, and trace ID without query strings or member identifiers. Authenticated administrators can inspect process-local request counts, HTTP 5xx failure rates, and average/maximum latency with `GET /api/admin/operations` and the existing `X-Admin-Key`. These counters reset whenever the Render instance restarts and complement, rather than replace, durable external monitoring.

The live scorecard records two minimal beta events: a draft being reopened and the elapsed time from rendering a hole to pressing its save action. Events contain a random idempotency ID, event type, owned round ID, optional duration, and server timestamp; they contain no score values, email, device fingerprint, or free text. `GET /api/admin/operations/beta` (authenticated plus `X-Admin-Key`) reports the last 30 days by default and accepts an optional UTC `from` value up to one year ago. Completion rate uses terminal rounds (`Completed / (Completed + Abandoned)`); resume completion rate is the percentage of distinct resumed rounds that are currently completed. Product events are included in member data exports and permanently removed with the account.

Run the tests with:

```bash
dotnet test tests/BirdieBuddy.Tests/BirdieBuddy.Tests.csproj
node --test tests/browser/*.test.cjs
```

## Render deployment

The included Dockerfile listens on port `10000`. Configure these Render environment variables:

- `ASPNETCORE_ENVIRONMENT=Production`
- `ConnectionStrings__DefaultConnection=<Render PostgreSQL internal connection string>`
- `Administration__ImportKey=<long random secret>` when the Golf NZ admin import endpoint is required
- `Application__PublicBaseUrl=https://your-service.onrender.com`
- `Email__From`, `Email__Smtp__Host`, `Email__Smtp__Port`, `Email__Smtp__Username`, and `Email__Smtp__Password` for verification and password-reset delivery

Do not put production credentials in an appsettings file. SMTP defaults to TLS on port 587; without host/from configuration, account creation still succeeds but the server logs a warning and no email is sent. Confirm `/health/ready` after each deploy and retain automated PostgreSQL backups. Before a schema deployment, test the migration against a recent database copy and document the restore point.

## Live-round API

All endpoints except registration, login, CSRF initialization, and health checks require authentication. Browser mutation requests must first obtain `/api/security/csrf` and send its token in `X-CSRF-TOKEN`.

```text
POST /api/rounds/drafts
PUT  /api/rounds/{roundId}/holes/by-number/{holeNumber}
POST /api/rounds/{roundId}/complete
POST /api/rounds/{roundId}/abandon
GET  /api/rounds/page?status=Draft&limit=20&courseId=&from=&to=&holeCount=
```

The hole upsert route is idempotent for `(roundId, holeNumber)`. A round is one of `Draft`, `Completed`, or `Abandoned`; only drafts accept live hole updates. The legacy all-at-once `POST /api/rounds` remains supported.

### Live entry reliability

- Each input change is stored locally immediately, then synchronized after a 600ms debounce. Previous/next navigation preserves pending changes.
- Device drafts are scoped by user and round. Requests are serialized; newer edits made during a save remain queued. Web Locks coordinate synchronization between supporting browser tabs.
- Replays check the signed-in user and submit `checkExpected: true` with the previously read `expectedHole` (or `null` for an unrecorded hole). Conflicts return HTTP 409 and keep local data instead of overwriting the server.
- EF uses the existing `UpdatedAt` column as a concurrency token. This mapping change does not require a new database column. Clients that omit `checkExpected` keep the older unconditional update contract; migrate them to the guarded contract.
- Completion and abandonment require an empty successfully synchronized outbox. Actual tee hole numbers are used, including back-nine layouts numbered 10–18; completed scorecards must match the whole selected layout.
- The old unscoped `birdiebuddy.liveQueue` is deliberately not replayed or deleted because its owning account is unknown. Preserve it for manual recovery if it contains unsynced historical input.
- Local drafts are not a backup or a full offline-installable application. The page/assets must already be available, and reopening an offline draft uses the current tab's previously verified identity. Conflict comparison/resolution UI and PostgreSQL-backed race testing remain follow-up work.

For a manual UI smoke check without real accounts or a database, run `node tests/browser/smoke-server.cjs` and open `http://127.0.0.1:4173/live-round.html?id=1`. This fixture uses in-memory mock API responses; it does not replace the real HTTP/backend tests.

## Golf NZ catalogue administration

The import is idempotent and reads `scripts/golf_nz_courses.json`, which is included in published output. Import controls are intentionally absent from the member UI. An authenticated administrator may call the protected endpoints with `X-Admin-Key` matching `Administration__ImportKey`:

```text
POST /api/courses/import-golf-nz
GET  /api/courses/import-golf-nz/status
```

The job state remains process-local; run only one import at a time and consult structured server logs after a restart.

## Data model notes

- `Course → CourseTee → CourseHole` stores tee-specific rating, slope, distance, par, and stroke index.
- `Round → Hole` snapshots par so later course edits cannot change historical scoring.
- `Round.UserId` and member-owned `Course.UserId` enforce tenant ownership in every service query.
- `Round.Status`, timestamps, current hole, and the unique `(RoundId, HoleNumber)` index support resumable live entry.
- Fairway percentage excludes holes where fairway is not applicable; draft and abandoned rounds are excluded from career statistics.

## Current product boundary

This release targets a small English-language beta and validates mobile web scoring before an iOS client is started. Shot-by-shot tracking, social features, coach sharing, payments, token-based iOS authentication, email verification, and password-reset delivery remain future work.
