# Native mobile API contract

This is the implemented controller/DTO contract reviewed on 2026-10-01. It is additive: browser cookie authentication and existing `/api/*` routes remain supported. API availability does not imply native UI parity; see [current-status.md](current-status.md).

## Authentication

### `POST /api/mobile/auth/register`

Request:

```json
{"email":"golfer@example.com","displayName":"Test Golfer","password":"BirdiePass123"}
```

The password must be 8-128 characters and contain at least one letter and one digit; the
display name must be 2-80 characters.

Response `200` is a session with the same shape as `POST /api/mobile/auth/session`, so a
new account is signed in without a second request. When the deployment sets
`Authentication:RequireVerifiedEmail`, the response is `202` and carries no tokens:

```json
{"requiresEmailVerification":true,"email":"golfer@example.com"}
```

A verification email is sent in both cases. A duplicate address returns the usual
`problem+json` error rather than a session.

### `POST /api/mobile/auth/session`

Request:

```json
{"email":"golfer@example.com","password":"BirdiePass123"}
```

Response `200`:

```json
{
  "accessToken":"<opaque-token>",
  "refreshToken":"<opaque-token>",
  "accessTokenExpiresAt":"2026-09-15T01:15:00Z",
  "refreshTokenExpiresAt":"2026-10-15T01:00:00Z",
  "user":{"id":7,"email":"golfer@example.com","displayName":"Test Golfer","emailVerified":true}
}
```

Access tokens expire after 15 minutes. Refresh tokens expire after 30 days and are single-use; a successful refresh invalidates the submitted refresh token.

### `POST /api/mobile/auth/refresh`

Request:

```json
{"refreshToken":"<opaque-token>"}
```

The response has the same shape as a successful session creation. Store the new pair atomically.

### `POST /api/mobile/auth/revoke`

Requires `Authorization: Bearer <accessToken>`. Invalidates all active mobile tokens for the authenticated user and returns `204`.

## Authenticated requests

Send the access token in the `Authorization` header. The native client refreshes once on `401` and retries the original request once. A second `401`, or a refresh `401`/`403`, invalidates the local session; network/transient refresh failures preserve it and pending writes. Hole-write revisions and expected snapshots protect live-save retries.

### Antiforgery

The current native client sends **no** `X-CSRF-TOKEN` header. Unsafe feature requests (`POST`, `PUT`,
`PATCH`, `DELETE`) authenticated by a valid mobile access token are exempt from
antiforgery validation, because a bearer credential is never attached ambiently by a
browser and therefore cannot be forged from another origin. Browser cookie sessions are
unaffected and still require the token — see `Infrastructure/MobileAwareAntiforgeryFilter.cs`.
A request with no valid bearer token still needs one unless its action/controller explicitly has `[IgnoreAntiforgeryToken]`, as `/api/mobile/auth` does. `[AllowAnonymous]` alone does not remove this requirement.

The existing endpoints are used for feature data:

| Capability | Routes |
|---|---|
| Courses | `GET /api/courses`, `GET /api/courses/page`, `GET /api/courses/{id}`, `POST /api/courses`, `PUT /api/courses/{id}`, `DELETE /api/courses/{id}` |
| Round lists/detail | `GET /api/rounds`, `GET /api/rounds/page`, `GET /api/rounds/options`, `GET /api/rounds/{id}`, `GET /api/rounds/{id}/holes` |
| Round lifecycle | `POST /api/rounds`, `POST /api/rounds/drafts`, `POST /api/rounds/{id}/complete`, `POST /api/rounds/{id}/abandon`, `PUT /api/rounds/{id}`, `DELETE /api/rounds/{id}` |
| Live hole upsert | `PUT /api/rounds/{roundId}/holes/by-number/{holeNumber}` |
| Legacy/completed hole edits | `POST /api/rounds/{roundId}/holes`, `PUT /api/rounds/{roundId}/holes/{holeId}` — the latter identifies a stored hole by ID, not hole number |
| Statistics | `GET /api/statistics/overview`, `GET /api/statistics/round/{roundId}` |
| Practice | `GET/POST /api/practice/sessions`, `POST /api/practice/sessions/{id}/complete` |
| Account | Existing `/api/auth/me`, `/api/auth/profile`, `/api/auth/change-password`, `/api/auth/forgot-password`, `/api/auth/reset-password`, `/api/auth/send-verification`, `/api/auth/verify-email`, `/api/auth/export`, and `/api/auth/delete-account` |

### Paging and filters

`GET /api/rounds/page` and `GET /api/statistics/overview` accept `courseId`, `from`, `to` (both `yyyy-MM-dd`), `holeCount`, and `courseTeeId`. Native filter controls offer 9 or 18 holes.

The round page additionally accepts `limit` (1-100, default 20), integer `cursor`, `status`, and `search`, returning `{ "items": [...], "nextCursor": <int|null> }`. Results are ordered by descending round ID; use `nextCursor` for the next page. A null cursor means the last page. The resume banner requests `status=Draft&limit=1`. Round DTO validation accepts hole counts 1-18; it does not use the statistics-specific date/filter error contract.

The statistics endpoint rejects a hole count other than 9/18 or a reversed explicit date range with `400 statistics.invalid_filter`. It includes completed rounds only, defaults to a 365-day lookback ending today (or the supplied `to`), and caps the range at 1,825 days.

`GET /api/courses/page` takes `search`, `limit` (1-100, default 50), and an integer `cursor`; it orders courses by ascending ID and returns `items`/`nextCursor`. Native course search currently filters the loaded catalogue rather than using this paged endpoint.

### Live save and concurrency

Use the `holes/by-number/{holeNumber}` route for draft saves. `HoleUpsertDto` carries `par`, `score`, `putts`, `gir`, nullable `fairwayHit`, `penalty`, `checkExpected`, and nullable `expectedHole`. The expected snapshot has the complete `HoleDto` shape (`id`, `holeNumber`, `par`, `score`, `putts`, `gir`, `fairwayHit`, `penalty`); null means the client expects no stored hole yet. A mismatching guarded write returns `409 round.save_conflict`. Preserve local input and require review before rebasing it.

Send `fairwayHit: null` for a par 3. Draft completion requires every expected tee hole to be valid and synchronized; it cannot finish entirely offline. The completion response is `RoundDetailDto`, which the native client also uses as its draft shape.

### Course ownership

`CourseSummaryDto` and `CourseDto` carry a `custom` boolean: `true` for a course
this user created, `false` for a shared imported course. Only a custom course can
be updated or deleted — the service scopes both by owner — so a client must use
this field rather than offering an action the server will refuse with `404`.

A custom course requires exactly 18 holes with unique numbers 1-18 on creation.
`PUT /api/courses/{id}` accepts name and location only; there is no endpoint to
add or edit tees, so imported tee data cannot be modified from a client.

Shared catalogue reads are public; member-owned course reads and mutations remain owner-scoped. A historical round can prevent deleting a referenced custom course.

### Password change

`POST /api/auth/change-password` ends the caller's session on success. A mobile
client must discard its stored tokens and sign in again with the new password
rather than reusing the access token it already holds.

### Anonymous account flow gap

`APIClient` currently calls `/api/auth/forgot-password`, `/api/auth/reset-password`, and `/api/auth/verify-email` with `authenticated: false` and no CSRF header. These actions allow anonymous users but do not have `[IgnoreAntiforgeryToken]`. The global `MobileAwareAntiforgeryFilter` therefore requires browser CSRF state for them. Code inspection identifies a native/server contract gap: these requests can be rejected with `400 security.csrf_invalid` before the action runs. This finding has not been reproduced against the deployed service during this documentation update; successful native reset/verification must be validated after resolving the contract.

The app implements `birdiebuddy://verify-email?token=…` and `birdiebuddy://reset-password?token=…` handlers. Current emails link to HTTPS web pages instead. Universal Links are not configured, and the custom-scheme handler does not bypass the CSRF gap.

## Error contract

Errors use `application/problem+json`:

```json
{
  "status":409,
  "title":"The record changed elsewhere.",
  "detail":"Review the server value before retrying.",
  "type":"urn:birdiebuddy:problem:round.save_conflict",
  "code":"round.save_conflict",
  "traceId":"..."
}
```

The `code` is stable client logic; `detail` is display text. Dates are UTC timestamps unless a DTO explicitly uses a date-only value. Draft hole writes retain the existing optimistic concurrency snapshot and must never silently overwrite a newer server value.
