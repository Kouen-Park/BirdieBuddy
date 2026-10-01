# BirdieBuddy

<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="design/brand/birdiebuddy-logo-light.svg" />
    <img src="design/brand/birdiebuddy-logo.svg" alt="BirdieBuddy" width="345" />
  </picture>
</p>

<p align="center">
  <strong>Reliable golf scoring, from the first tee to the final sync.</strong>
</p>

<p align="center">
  Record 9- and 18-hole rounds, recover safely from poor connectivity, understand your game, and turn results into focused practice.
</p>

<p align="center">
  <a href="https://github.com/Kouen-Park/BirdieBuddy/actions/workflows/ci.yml"><img src="https://github.com/Kouen-Park/BirdieBuddy/actions/workflows/ci.yml/badge.svg" alt="CI status" /></a>
  <img src="https://img.shields.io/badge/iOS-17%2B-0B2723" alt="iOS 17 or later" />
  <img src="https://img.shields.io/badge/.NET-10-512BD4" alt=".NET 10" />
  <img src="https://img.shields.io/badge/PostgreSQL-16-336791" alt="PostgreSQL 16" />
</p>

BirdieBuddy is a golf scorecard platform with a native SwiftUI client, a mobile-first web experience, and an ASP.NET Core API. Its defining principle is simple: a golfer's input must not disappear just because the connection does.

## Current status

The product is ready for active iOS development and internal testing, but it is not yet App Store ready.

Reviewed on **2026-10-01** against `master` after [PR #15](https://github.com/Kouen-Park/BirdieBuddy/pull/15). All four PR CI checks passed, including 27 iOS tests. The [current feature and verification status](docs/current-status.md) separates implemented features from remaining device, design, and release checks; CI success does not confirm the deployed Render version.

| Surface | Status | Current coverage |
|---|---|---|
| iOS app | Active development | Token authentication, custom courses, live scoring, durable outbox, conflict review, paged history, filtered statistics and charts, practice creation/history, account tools, and local diagnostics |
| Mobile web | Beta-capable | Full round lifecycle, offline recovery, detailed statistics, practice guidance, course management, and account flows |
| Backend API | Operational foundation | ASP.NET Core 10, PostgreSQL, ownership checks, mobile and browser authentication, rate limits, health checks, and telemetry |
| Release operations | In progress | Physical-device verification, TestFlight, production SMTP, backup rehearsal, monitoring, privacy, and store metadata remain |

## Product experience

### Play the round

- Start a 9- or 18-hole draft from a course and tee.
- Record score, putts, GIR, fairway, and penalties with fast per-hole controls.
- Resume and complete drafts, review completed scorecards, and edit round date/tee metadata.
- The web client also supports abandoning drafts and editing completed holes; these actions are not yet exposed in the native UI.

### Keep every change safe

- Persist input locally before reporting it as device-saved.
- Isolate pending writes by user and round.
- Synchronize queued hole revisions in order when connectivity returns.
- Stop on a `409` and let the golfer compare device and server values instead of silently overwriting either one.

The web scorecard queues input changes automatically. In iOS, **Save hole**, **Next hole**, and **Finish round** durably queue the current input before synchronization; values not submitted through those actions are not promised to survive a force-quit. Starting a new native draft requires the API, while an already cached draft can reopen offline.

### Learn and improve

- Compare score-to-par across 9- and 18-hole rounds.
- Review GIR, fairway, putting, par-type, and trend data.
- Turn recent completed rounds into evidence-linked practice recommendations and sessions.

The native Statistics tab shows recommendations and chart data tables. The native Practice tab starts sessions and displays recent history; guided drill steps and session completion/result entry remain web features.

## Screenshots

<table>
  <tr>
    <td align="center"><strong>Live scorecard</strong></td>
    <td align="center"><strong>Round review</strong></td>
    <td align="center"><strong>Conflict recovery</strong></td>
  </tr>
  <tr>
    <td><img src="docs/screenshots/live-round.png" alt="BirdieBuddy live scorecard" /></td>
    <td><img src="docs/screenshots/round-details.png" alt="BirdieBuddy completed round details" /></td>
    <td><img src="docs/screenshots/conflict-recovery.png" alt="BirdieBuddy conflict comparison" /></td>
  </tr>
</table>

> These web screenshots use deterministic local fixtures and predate the latest logo refresh. They illustrate the scoring/recovery flows, not a current visual acceptance pass. Native iPhone screenshots will complement them after the next device design review.

## Brand and app icon

The outlined lowercase wordmark above shares the bird-on-tee symbol with the iOS app icon. iOS navigation, login, and session restoration use a vector PDF; web navigation and authentication pages use SVG variants. Matching SVG/32px favicons, a 180px Apple touch icon, and 192/512px PWA icons are included. [Brand assets and regeneration instructions](design/brand/README.md) include a transparent PNG for reuse.

The bird-on-tee artwork is also available as [four 1024 × 1024 SVG layers](design/app-icon/icon-composer/) for Apple Icon Composer, with a [flat preview and import notes](design/app-icon/README.md). It uses the app's `night`, `paper`, and `mint` color tokens. Liquid Glass effects and appearance variants are left for Icon Composer.

The iOS target now bundles a flat 1024 × 1024 export of this design in [`AppIcon.appiconset`](ios/BirdieBuddyApp/Assets.xcassets/AppIcon.appiconset/). The separate SVG layers have not yet been imported into Icon Composer, so the installed icon does not use layered Liquid Glass rendering yet.

The native app uses a light theme, shared semantic status colors, and layouts that stack counters and round scores at accessibility text sizes. See the [design audit](design/ios-audit/README.md) for changes and the remaining screen-review scope.

## Architecture

The backend remains a modular monolith. Both clients share the same PostgreSQL source of truth while using authentication and local persistence appropriate to their platform.

```mermaid
flowchart LR
    subgraph IOS["SwiftUI app"]
        IUI["Native features"]
        Keychain["Keychain session"]
        IOutbox["User + round outbox"]
        IUI --> Keychain
        IUI --> IOutbox
    end

    subgraph WEB["Mobile web"]
        WUI["HTML · CSS · JavaScript"]
        SW["Service worker"]
        WOutbox["Scoped browser outbox"]
        SW --> WUI
        WUI --> WOutbox
    end

    subgraph API["ASP.NET Core 10"]
        Auth["Bearer tokens · cookies · CSRF"]
        Controllers["Controller API"]
        Services["Feature services"]
        EF["EF Core · Npgsql"]
        Auth --> Controllers --> Services --> EF
    end

    IUI -->|HTTPS + access token| Auth
    IOutbox -->|ordered revision sync| Controllers
    WUI -->|HTTPS + cookie| Auth
    WOutbox -->|ordered revision sync| Controllers
    EF --> DB[(PostgreSQL 16)]
    API -.-> Ops["Health · logs · metrics · traces"]
```

### Mobile session lifecycle

The iOS app receives a short-lived access token and a rotating refresh token. Tokens are stored in Keychain, refresh is single-use, and logout or account security changes revoke the mobile session. Browser cookie authentication remains available without breaking existing web clients.

### Live-save lifecycle

```text
input changes
  -> persist a user-and-round-scoped local revision
  -> show the device-saved state
  -> send the expected server snapshot and new value
  -> acknowledge only the matching local revision
     or preserve it and open explicit conflict review
```

## Technology

| Layer | Technology |
|---|---|
| Native client | SwiftUI, Swift concurrency, URLSession, Keychain, SwiftData-backed actor outbox |
| Web client | Semantic HTML, CSS, vanilla JavaScript, Service Worker, Web Locks, Chart.js |
| Backend | ASP.NET Core 10, C# controllers and scoped services |
| Data | EF Core 10, Npgsql, PostgreSQL 16 |
| Security | Cookie + CSRF browser sessions, rotating bearer-token mobile sessions, ownership-aware queries, rate limiting |
| Observability | OpenTelemetry, structured logs, liveness and readiness checks |
| Tests | XCTest, xUnit, PostgreSQL integration tests, Node test runner, Playwright |
| Delivery | GitHub Actions, Docker, Render |

## Run the backend and web app

### Prerequisites

- .NET 10 SDK
- PostgreSQL 16
- Node.js 22+ and npm for browser tests

Configure a local database with .NET Secret Manager:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=localhost;Port=5432;Database=birdiebuddy;Username=YOUR_USER;Password=YOUR_PASSWORD"
dotnet restore
dotnet run --launch-profile https
```

The checked-in HTTPS profile serves `https://localhost:7205` and `http://localhost:5205`. Open the HTTPS URL printed by ASP.NET Core. Swagger is available at `/swagger` in Development. Never commit working credentials to `appsettings*.json`.

## Run the iOS app

### Prerequisites

- Full Xcode with an installed iOS simulator runtime (CI uses Xcode 26.5; the latest local build used Xcode 27.0)
- An iOS 17+ simulator or device
- A running BirdieBuddy API

Open `ios/BirdieBuddyApp.xcodeproj`. Both Debug and Release builds currently point `API_BASE_URL` at `https://birdiebuddy.onrender.com`. A scheme environment variable with the same name can override it for an Xcode-launched local or staging run; for a build that must keep its endpoint after a force-quit and relaunch, change the target's `API_BASE_URL` build setting. For a local simulator, use `http://localhost:5205` with the checked-in backend profile, or explicitly run the API on another port. Debug allows HTTP only for localhost; Release requires a non-local HTTPS origin. A physical iPhone needs a reachable HTTPS API instead of the Mac's localhost. The native client has no third-party dependencies.

Run its test suite from Xcode or the command line:

```bash
./scripts/test-ios.sh
```

The script selects an iPhone from the newest installed simulator runtime. Set `BIRDIEBUDDY_IOS_SIMULATOR_ID` to use a specific simulator UDID. A connected phone is not required. See [the testing guide](docs/testing.md#native-ios-tests) for Xcode selection and simulator cleanup; the current script leaves its simulator running after tests.

## Test the platform

```bash
# .NET unit, API, and service tests
dotnet test tests/BirdieBuddy.Tests/BirdieBuddy.Tests.csproj

# Browser-module tests
npm ci
npm run test:browser

# Deterministic mobile-browser journey
npx playwright install chromium
npm run test:e2e:fixture

# Native iOS tests
./scripts/test-ios.sh
```

CI additionally runs PostgreSQL integration tests, full browser E2E, migration SQL generation, publish and Docker validation, and the SwiftUI test target on Xcode 26.5.

## Repository map

| Path | Purpose |
|---|---|
| `ios/BirdieBuddyApp/` | SwiftUI application, networking, authentication, local persistence, and features |
| `ios/BirdieBuddyAppTests/` | Native model, session, outbox, offline journey, and Dynamic Type layout tests |
| `design/app-icon/` | Layered Icon Composer source artwork and flat preview for the bundled iOS icon |
| `design/brand/` | Outlined wordmarks, reusable PNG, preview, and native/web export script |
| `design/ios-audit/` | Design findings, validation limits, and an optional fixture capture harness |
| `Controllers/` | HTTP endpoints and status-code mapping |
| `Services/` | Authentication, courses, rounds, statistics, practice, and imports |
| `DTOs/` | Validated public request and response contracts |
| `Infrastructure/` | Errors, security, observability, and deployment validation |
| `Data/`, `Models/`, `Migrations/` | EF Core persistence and PostgreSQL schema history |
| `wwwroot/` | Mobile web UI, service worker, local outbox, and static assets |
| `tests/` | .NET, browser-module, fixture, and Playwright coverage |
| `scripts/` | iOS test selection, deployment checks, SMTP probe, backup rehearsal, and source data tools |

## Roadmap

### 1. Harden the native core — implemented, awaiting device gate

- SwiftData now persists user-scoped pending writes and conflict review state across app termination.
- App-wide synchronization resumes after session restoration, reauthentication, network recovery, foreground entry, and live-round entry.
- Retryable failures remain queued, conflicts pause only their round, and permanent client failures require explicit attention.
- XCTest covers migration, revision de-duplication, account isolation/deletion, conflict restoration, network recovery, and permanent client errors.

### 2. Reach deliberate web parity

- Round pagination and filters, round date/tee editing, password change, email verification/reset, and custom course creation are implemented in iOS.
- Add editing for custom-course holes and tees, which the current course-update API does not support.
- Add native UI for draft abandonment, completed-hole editing, and practice completion/results; the backend and web already support these.
- Bring guided practice and richer trend comparisons to native, and finish runtime/error localization.
- Resolve the anonymous password-reset/email-verification CSRF contract gap documented in [the mobile API guide](docs/mobile-api.md#anonymous-account-flow-gap).
- Compare web and native results against shared API contract fixtures.

### 3. Validate on real iPhones

- Test an entire round through airplane mode, relaunch, backgrounding, and Wi-Fi/cellular transitions.
- Verify iPhone SE-sized layouts, keyboard avoidance, safe areas, Dynamic Type, VoiceOver, and low-power mode.
- Run internal TestFlight with instrumentation for starts, completions, sync failures, and conflict rates.

### 4. Prepare production and App Store release

- Separate development, staging, and production API configuration.
- Import the layered icon artwork into Icon Composer, check its appearance variants, and replace the current flat icon asset for release.
- Finish SMTP verification, backup restore rehearsal, monitoring, and migration rollout procedures; validate the existing on-device MetricKit diagnostics and review any future automatic crash-reporting service.
- Prepare privacy policy, terms, App Privacy answers, screenshots, reviewer credentials, and store metadata.
- Complete external TestFlight before App Store submission.

Shot-by-shot tracking, social features, coach sharing, and payments remain outside the first native release.

## Documentation

- [Current feature and verification status](docs/current-status.md)
- [Architecture](docs/architecture.md)
- [Native mobile API contract](docs/mobile-api.md)
- [iOS client guide](ios/README.md)
- [Testing and CI](docs/testing.md)
- [iOS release checklist](docs/ios-release.md)
- [Beta operations](docs/beta-operations.md)
- [Physical iPhone Safari checklist](tests/browser/iphone-safari-checklist.md)
- [Brand assets](design/brand/README.md) and [iOS design audit](design/ios-audit/README.md)
