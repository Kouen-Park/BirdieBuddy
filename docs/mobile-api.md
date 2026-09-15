# Native mobile API contract

This is the initial contract for the SwiftUI client. It is additive: browser cookie authentication and existing `/api/*` routes remain supported.

## Authentication

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

The existing endpoints are used for feature data:

| Capability | Routes |
|---|---|
| Courses | `GET /api/courses`, `GET /api/courses/page`, `GET /api/courses/{id}` |
| Rounds | `GET/POST /api/rounds`, `POST /api/rounds/drafts`, `PUT /api/rounds/{id}/holes/{holeNumber}`, `POST /api/rounds/{id}/complete`, `POST /api/rounds/{id}/abandon` |
| Statistics | `GET /api/statistics/overview`, `GET /api/statistics/rounds/{roundId}` |
| Practice | `GET/POST /api/practice/sessions`, `POST /api/practice/sessions/{id}/complete` |
| Account | Existing `/api/auth/profile`, `/api/auth/change-password`, `/api/auth/export`, and `/api/auth/delete-account` |

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
