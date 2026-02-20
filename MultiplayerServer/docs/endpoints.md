# Endpoints (v1)

## GET /health

Purpose: service health/liveness.

Request body: none.

Success (`200`):

```json
{
  "status": "ok",
  "utcTime": "2026-02-20T00:12:14.0000000+00:00"
}
```

## GET /api/v1

Purpose: API identity/version metadata.

Request body: none.

Success (`200`):

```json
{
  "service": "MultiplayerServer",
  "version": "v1"
}
```

## POST /api/v1/matches

Purpose: create a new match and creator token.

Request body: none.

Success (`200`):

```json
{
  "matchId": "58b02d5d9d5c43cc9a0314a8f1f4a14c",
  "joinCode": "8Q2KLM",
  "creatorToken": "e2ef01fdb8517a608fcf4862ef35f6a1"
}
```

## POST /api/v1/matches/join

Purpose: join an existing match as the second player.

Request body:

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `joinCode` | string | yes | 6-char join code from create response |

Example request:

```json
{
  "joinCode": "8Q2KLM"
}
```

Success (`200`):

```json
{
  "matchId": "58b02d5d9d5c43cc9a0314a8f1f4a14c",
  "seat": "Black",
  "playerToken": "a8dc535a026f085f0fc9d57fceab5902"
}
```

Failure responses:

- `400`:
  - `join_code_required`
  - other join failures that are not explicitly mapped
- `404`:
  - `match_not_found`
- `409`:
  - `match_full`

## POST /api/v1/matches/moves

Purpose: submit an authoritative chess move.

Request body:

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `matchId` | string | yes | match id from create/join |
| `playerToken` | string | yes | creator or joiner token |
| `from` | string | yes | source square, e.g. `e2` |
| `to` | string | yes | destination square, e.g. `e4` |
| `promotion` | string | no | one of `Q`,`R`,`B`,`N` when promoting |

Example request:

```json
{
  "matchId": "58b02d5d9d5c43cc9a0314a8f1f4a14c",
  "playerToken": "e2ef01fdb8517a608fcf4862ef35f6a1",
  "from": "e2",
  "to": "e4"
}
```

Success (`200`):

```json
{
  "accepted": true,
  "snapshot": {
    "matchId": "58b02d5d9d5c43cc9a0314a8f1f4a14c",
    "sideToMove": "Black",
    "moveNumber": 2,
    "board": [
      "rnbqkbnr",
      "pppppppp",
      "........",
      "........",
      "....P...",
      "........",
      "PPPP.PPP",
      "RNBQKBNR"
    ],
    "status": "in_progress",
    "resolution": null,
    "winnerSeat": null,
    "presence": {
      "creator": {
        "seat": "White",
        "isConnected": true,
        "isReserved": true,
        "disconnectedUtc": null,
        "graceExpiresUtc": null
      },
      "joiner": {
        "seat": "Black",
        "isConnected": true,
        "isReserved": true,
        "disconnectedUtc": null,
        "graceExpiresUtc": null
      }
    }
  }
}
```

Failure responses:

- `400`:
  - `match_id_required`
  - `player_token_required`
  - `move_coordinates_required`
  - `invalid_promotion`
- `403`:
  - `invalid_player_token`
  - `unauthorized_resume`
- `404`:
  - `match_not_found`
- `409`:
  - `match_not_ready`
  - `out_of_turn`
  - `illegal_move`
  - `seat_not_reconnectable`
  - `grace_expired`
  - `match_already_ended`

## POST /api/v1/matches/snapshot

Purpose: fetch authoritative state for cold start/recovery.

Request body:

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `matchId` | string | yes | match id |
| `playerToken` | string | yes | token for creator/joiner seat |

Example request:

```json
{
  "matchId": "58b02d5d9d5c43cc9a0314a8f1f4a14c",
  "playerToken": "e2ef01fdb8517a608fcf4862ef35f6a1"
}
```

Success (`200`): returns `MatchSnapshotResponse` (same shape as `snapshot` in move response).

Failure responses:

- `400`:
  - `match_id_required`
  - `player_token_required`
- `403`:
  - `invalid_player_token`
  - `unauthorized_resume`
- `404`:
  - `match_not_found`
- `409`:
  - `match_already_ended`
  - `seat_not_reconnectable`
  - `grace_expired`

Notes:

- Missing/empty body is handled as `400` with `match_id_required`.
- Current snapshot use case returns `400/403/404` in normal flows; `409` codes are supported by the HTTP mapper for compatibility with broader lifecycle failures.

## SignalR /hubs/v1/matches

Purpose: realtime updates, reconnect, and resync.

Auth:

- Required.
- Provide player token using:
  - `Authorization: Bearer <playerToken>` header, or
  - `access_token` query parameter.
- The authenticated token must match the `playerToken` argument passed to hub methods.

Client -> server hub methods:

- `SubscribeMatch(matchId, playerToken)`
- `RequestResync(matchId, playerToken)`
- `UnsubscribeMatch(matchId)`

Server -> client events:

- `match.snapshot`
- `match.updated`
- `match.presenceChanged`
- `match.ended`
- `match.error`

Event payload metadata shape:

```json
{
  "matchId": "58b02d5d9d5c43cc9a0314a8f1f4a14c",
  "eventId": "4d8f9b3131a74642a3d9fd9c0a0f6a1f",
  "sequence": 12,
  "occurredUtc": "2026-02-20T00:12:14.0000000+00:00"
}
```
