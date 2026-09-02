# Birdie Buddy

Birdie Buddy is a mobile-first golf round tracker and performance notebook. It uses ASP.NET Core 8, EF Core 8, PostgreSQL, cookie authentication, and a vanilla HTML/CSS/JavaScript frontend served from `wwwroot`.

## Features

- Private member accounts with secure password hashing and profile/password management
- Shared Golf New Zealand courses plus member-owned custom courses
- On-course live scorecard with draft, auto-save, offline queue, resume, complete, and abandon flows
- Completed-scorecard entry, round history, edit/delete, and per-hole breakdowns
- 9/18-hole aware statistics, score/GIR/putting trends, and evidence-based practice priorities
- CSRF protection, login rate limiting, security headers, ownership checks, and admin-protected imports
- Liveness/readiness checks, migration locking, automated tests, and GitHub Actions CI

## Local development

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

Run the tests with:

```bash
dotnet test tests/BirdieBuddy.Tests/BirdieBuddy.Tests.csproj
```

## Render deployment

The included Dockerfile listens on port `10000`. Configure these Render environment variables:

- `ASPNETCORE_ENVIRONMENT=Production`
- `ConnectionStrings__DefaultConnection=<Render PostgreSQL internal connection string>`
- `Administration__ImportKey=<long random secret>` when the Golf NZ admin import endpoint is required

Do not put production credentials in an appsettings file. Confirm `/health/ready` after each deploy and retain automated PostgreSQL backups. Before a schema deployment, test the migration against a recent database copy and document the restore point.

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
