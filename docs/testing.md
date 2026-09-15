# Testing and CI guide

Birdie Buddy uses layered tests because no single test runner covers browser durability, ASP.NET Core middleware and PostgreSQL behavior equally well.

## Prerequisites

- .NET 8 SDK
- Node.js 22 or newer
- npm
- Chromium installed through Playwright
- PostgreSQL 16 only for database integration and full application E2E runs

Install dependencies once:

```bash
dotnet restore BirdieBuddy.csproj
npm ci
npx playwright install chromium
```

On Linux CI, `npx playwright install --with-deps chromium` also installs required operating-system packages.

## Test layers

| Layer | Command | What it proves | Database |
|---|---|---|---|
| .NET service and HTTP tests | `dotnet test tests/BirdieBuddy.Tests/BirdieBuddy.Tests.csproj` | Domain behavior, DTO validation, ownership, auth/CSRF, session invalidation, rate limits, headers, health and ProblemDetails through `WebApplicationFactory<Program>` | Isolated EF in-memory substitute; PostgreSQL category skipped unless configured |
| Node browser-module tests | `npm run test:browser` | Durable outbox revisions, account/round isolation, conflict resolution, navigation focus and view rendering contracts | None |
| Playwright fixture E2E | `npm run test:e2e:fixture` | Real Chromium mobile viewport, offline/reconnect, conflict dialog, keyboard activation and horizontal fit | Loopback in-memory mock API |
| PostgreSQL integration | command below | Real migrations, constraints, ownership, concurrency rollback and query compatibility | Disposable local PostgreSQL |
| Full application Playwright E2E | command below | Registration, cookie/CSRF flow, custom course creation, draft start, reload/resume, 18-hole save and completion through the real app | Disposable local PostgreSQL |

The EF in-memory provider is retained for fast request-pipeline coverage. It does not emulate relational constraints or transactions; the PostgreSQL category is the authority for those behaviors.

## Fast local verification

Run the normal developer suite:

```bash
dotnet test tests/BirdieBuddy.Tests/BirdieBuddy.Tests.csproj
npm run test:browser
npm run test:e2e:fixture
```

`npm run test:e2e` runs both Playwright projects. Without `BIRDIEBUDDY_E2E_POSTGRES`, the full application project reports one intentional skip instead of silently substituting the fixture.

Playwright traces, screenshots and videos are written under `output/playwright/` only when configured or retained after failure. This directory is ignored by Git.

## PostgreSQL integration test

Create a disposable local database whose name starts with `birdiebuddy_test_`. The test applies migrations and inserts records, but does not delete or recreate the database.

```bash
export BIRDIEBUDDY_TEST_POSTGRES='Host=127.0.0.1;Port=5432;Database=birdiebuddy_test_local;Username=postgres;Password=YOUR_PASSWORD'
dotnet test tests/BirdieBuddy.Tests/BirdieBuddy.Tests.csproj \
  --configuration Release \
  --filter Category=PostgreSQL
```

Never point this variable at a shared development, staging or production database.

## Full application browser E2E

Create a separate empty disposable database. Playwright starts the Release application on `http://127.0.0.1:5187`; application startup applies pending migrations. The connection string is passed only to the child process.

```bash
export BIRDIEBUDDY_E2E_POSTGRES='Host=127.0.0.1;Port=5432;Database=birdiebuddy_e2e_local;Username=postgres;Password=YOUR_PASSWORD'
npm run test:e2e:app
```

The test creates uniquely named accounts, courses, rounds and holes. Use a disposable database so repeated local runs can be discarded through normal PostgreSQL administration.

Useful troubleshooting commands:

```bash
npx playwright test --project=app-mobile --trace=on
npx playwright show-report output/playwright/report
```

If port `4173` or `5187` is already occupied, stop the existing fixture/application process before retrying. If readiness times out, inspect the application output first; the usual causes are an invalid connection string, PostgreSQL not accepting connections, or migration failure.

## CI jobs

The GitHub Actions workflow has three independent required checks:

| Job | Responsibilities |
|---|---|
| `build-and-test` | Restore, Release build, full non-PostgreSQL .NET suite, migration SQL artifact, publish artifact, Node tests and Docker build |
| `postgres-integration` | PostgreSQL 16 service plus the real migration/concurrency/constraint test category |
| `browser-e2e` | PostgreSQL 16 service, Release app startup and both Playwright mobile projects |

The browser job uploads `output/playwright/` when it fails. The `postgres-migration-sql` artifact should be reviewed before a migration deployment. Repository branch protection must require all three checks; the workflow itself cannot enforce branch settings.

## Adding tests

- Put pure service and HTTP pipeline tests in `tests/BirdieBuddy.Tests`.
- Add PostgreSQL-only assertions to the `PostgreSQL` category and keep their database target safeguards intact.
- Put DOM-free JavaScript state/view tests in `tests/browser`.
- Put user-visible browser journeys in `tests/e2e` and use role, label or stable semantic locators where possible.
- Keep fixture tests deterministic. Do not make them appear to validate real authentication or PostgreSQL behavior.
- For regressions involving the request pipeline, reproduce through `WebApplicationFactory<Program>` instead of constructing controllers manually.
- For relational regressions, add a PostgreSQL assertion even when a fast EF in-memory test also exists.

## Manual mobile gate

Chromium device emulation does not reproduce Safari storage eviction, WebKit focus behavior, iOS installation or VoiceOver. Before a beta release, complete [the physical iPhone Safari checklist](../tests/browser/iphone-safari-checklist.md) and record the release commit with the evidence.
