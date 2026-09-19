# Native mobile API contract

This is the initial contract for the SwiftUI client. It is additive: browser cookie authentication and existing `/api/*` routes remain supported.

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

Send the access token in the `Authorization` header. The native client should refresh once on `401`, retry the original idempotent or revision-guarded request once, and then transition to signed-out if refresh fails.

### Antiforgery

A mobile client sends **no** `X-CSRF-TOKEN` header. Unsafe requests (`POST`, `PUT`,
`PATCH`, `DELETE`) authenticated by a valid mobile access token are exempt from
antiforgery validation, because a bearer credential is never attached ambiently by a
browser and therefore cannot be forged from another origin. Browser cookie sessions are
unaffected and still require the token — see `Infrastructure/MobileAwareAntiforgeryFilter.cs`.
A request with no valid bearer token is treated as a browser request and still needs one.

The existing endpoints are used for feature data:

| Capability | Routes |
|---|---|
| Courses | `GET /api/courses`, `GET /api/courses/page`, `GET /api/courses/{id}`, `POST /api/courses`, `PUT /api/courses/{id}`, `DELETE /api/courses/{id}` |
| Rounds | `GET/POST /api/rounds`, `POST /api/rounds/drafts`, `PUT /api/rounds/{id}/holes/{holeNumber}`, `POST /api/rounds/{id}/complete`, `POST /api/rounds/{id}/abandon`, `PUT /api/rounds/{id}`, `DELETE /api/rounds/{id}` |
| Statistics | `GET /api/statistics/overview`, `GET /api/statistics/round/{roundId}` |
| Practice | `GET/POST /api/practice/sessions`, `POST /api/practice/sessions/{id}/complete` |
| Account | Existing `/api/auth/me`, `/api/auth/profile`, `/api/auth/change-password`, `/api/auth/forgot-password`, `/api/auth/reset-password`, `/api/auth/send-verification`, `/api/auth/verify-email`, `/api/auth/export`, and `/api/auth/delete-account` |

### Paging and filters

`GET /api/rounds/page` and `GET /api/statistics/overview` accept the same filter
query parameters: `courseId`, `from`, `to` (both `yyyy-MM-dd`), `holeCount` (9 or
18), and `courseTeeId`. The round page additionally takes `limit` (1-100) and
`cursor`, and answers `{ "items": [...], "nextCursor": <int|null> }`. A null
`nextCursor` means the last page. `holeCount` outside 9 or 18, or `from` after
`to`, is rejected with `400 statistics.invalid_filter`.

### Course ownership

`CourseSummaryDto` and `CourseDto` carry a `custom` boolean: `true` for a course
this user created, `false` for a shared imported course. Only a custom course can
be updated or deleted — the service scopes both by owner — so a client must use
this field rather than offering an action the server will refuse with `404`.

A custom course requires exactly 18 holes with unique numbers 1-18 on creation.
`PUT /api/courses/{id}` accepts name and location only; there is no endpoint to
add or edit tees, so imported tee data cannot be modified from a client.

### Password change

`POST /api/auth/change-password` ends the caller's session on success. A mobile
client must discard its stored tokens and sign in again with the new password
rather than reusing the access token it already holds.

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
