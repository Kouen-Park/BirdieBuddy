# Current app status

Reviewed on **2026-10-01** against `master` at `bc3113d`, after [PR #15](https://github.com/Kouen-Park/BirdieBuddy/pull/15) merged the shared branding and iOS design changes. This page describes checked-in implementation and recorded verification, not an assertion that Render or an installed iPhone build already contains this commit.

## Implemented features and client differences

| Area | Native iPhone app | Web client |
| --- | --- | --- |
| Accounts | Registration, rotating bearer sessions in Keychain, profile/password changes, verification/reset, export, deletion, sign out | Cookie + CSRF sessions and the same account tools |
| Courses | Search, tee selection, custom creation with 18 holes, name/location editing, deletion where permitted | Public catalogue, custom-course management, draft creation |
| Live scoring | Tee-derived par, score/putts/GIR/fairway/penalties, explicit save, resume, complete | Input autosave, resume, complete, abandon |
| Durability | User/round-scoped SwiftData revisions and conflicts, cached drafts, lifecycle-triggered synchronization | Scoped browser outbox, Web Locks where available, cached service-worker shell |
| History | Cursor paging, course/date/length/tee filters, detailed scorecards, date/tee edits, deletion | History/filtering plus completed-hole editing |
| Statistics | Overview, 9/18-hole comparisons, score/putts/GIR charts with data tables, insights, recent rounds, per-round/par-type summaries | Overview, richer trend comparisons, par-type analysis, linked practice guidance |
| Practice | Start a session and view recent sessions; persistent field labels and an empty state | Guided drill steps, session start, completion, result/notes entry |
| Diagnostics | MetricKit reports stored locally, latest ten retained, manual sharing/deletion in Account | Product events for draft resume and hole-entry timing |
| Appearance | Light theme, five tabs, outlined navigation/login logo, Dynamic Type adaptations, English/Korean catalog | Matching SVG branding, favicon/Apple touch/PWA icons, responsive navigation |

Course holes and tees cannot be edited after creation through the current course-update contract on either client. Native draft abandonment, completed-hole editing, practice completion/results, and guided practice UI remain gaps even where backend endpoints exist. Runtime error messages and some dynamically constructed native labels still need localization work.

Native verification/reset UI and URL handlers exist, but code inspection found that their anonymous `/api/auth` requests omit the CSRF state required by the server. This [contract gap](mobile-api.md#anonymous-account-flow-gap) must be resolved and tested before describing these flows as release-ready; no deployed-service reproduction was performed during this documentation review.

## Persistence boundary

In iOS, **Save hole**, **Next hole**, and **Finish round** durably enqueue the current input before sync. Changing a control without using a save action does not guarantee recovery after force-quit. Creating a new draft needs the API; an existing cached draft can reopen offline for the same restored account. Logout preserves that account's pending writes, while successful account deletion removes them. Other account, course, and practice mutations do not use the hole-write outbox.

The browser caches an application shell and remembered draft state, not the API. Offline reopening depends on prior online caching and the remembered account identity. Neither client promises a first launch or a new round while fully offline.

## Recorded verification

| Evidence | Result |
| --- | --- |
| [PR #15 CI](https://github.com/Kouen-Park/BirdieBuddy/actions/runs/36714842015) | `build-and-test`, `postgres-integration`, `browser-e2e`, and `ios-tests` all passed |
| [Native CI test job](https://github.com/Kouen-Park/BirdieBuddy/actions/runs/36714842015/job/109885117758) | Xcode 26.5 on macOS 26; 23 model/session/outbox/journey tests and four layout tests, 27 total, zero failures |
| Local web checks | Browser unit tests 25/25; final mobile fixture rerun 5/5 |
| Local native build | Xcode 27.0 simulator build succeeded; local app launch/XCTest stalled, then the simulator was shut down |
| Brand exports | Outlined SVG/PDF and PNG previews inspected; native app-screen placement still needs a complete visual pass |

CI layout tests render the scorecard at all Dynamic Type sizes and test a narrow counter and stacked round row. They do not verify every screen, physical safe areas, keyboard behavior, VoiceOver, or installed icon appearance. The optional fixture capture harness in `design/ios-audit/CaptureTests.swift` is outside the test target and has not produced a completed screen-capture review.

## Configuration and remaining release work

The native target is iPhone-only, portrait, iOS 17+, bundle identifier `com.birdiebuddy.mobile`. The project and both property lists currently use version `1.0`, build `1`. Debug and Release default to `https://birdiebuddy.onrender.com`; Debug also permits localhost HTTP. The checked-in backend profiles use HTTP port 5205 and HTTPS port 7205. See [the native guide](../ios/README.md) for overrides and force-quit testing.

The installed app icon is a flat 1024px PNG. Icon Composer layers are source artwork for future import; layered rendering is not active. Native and web logos already use the same bird-on-tee identity.

Before TestFlight/App Store release, recheck the current design on physical iPhones, offline/relaunch and account isolation, Korean text, VoiceOver and keyboard behavior. Validate the production API, email delivery, monitoring, backup restore, signing, privacy declarations, version numbers, store metadata, and native screenshots. Existing device observations apply to their recorded builds and do not certify the latest branding/layout changes.

- [iOS guide](../ios/README.md)
- [Testing and CI](testing.md)
- [iOS release checklist](ios-release.md)
- [Beta operations](beta-operations.md)
- [Brand assets](../design/brand/README.md)
- [Design audit](../design/ios-audit/README.md)
