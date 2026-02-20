# Recovery and Reconnect

This document covers client recovery using snapshot fetch + realtime reconnect.

## 1) Cold Start Recovery (HTTP Snapshot)

Use snapshot endpoint to hydrate UI when app starts or resumes.

Request:

```http
POST /api/v1/matches/snapshot
Content-Type: application/json
```

```json
{
  "matchId": "58b02d5d9d5c43cc9a0314a8f1f4a14c",
  "playerToken": "e2ef01fdb8517a608fcf4862ef35f6a1"
}
```

Success:

- `200` + authoritative `MatchSnapshotResponse`

Failure:

- `400`: required fields missing
- `403`: token is not valid for this match seat
- `404`: match not found

## 2) Realtime Reconnect Flow (SignalR)

1. Connect to `/hubs/v1/matches` with token auth.
2. Call `SubscribeMatch(matchId, playerToken)`.
3. Process `match.snapshot` to reset local state.
4. Process subsequent `match.updated` and `match.presenceChanged`.
5. Optionally call `RequestResync(matchId, playerToken)` to force a fresh snapshot.

Important:

- Connection token and `playerToken` method arg must match.
- If mismatched/unauthorized, server emits `match.error` and rejects the call.

## 3) Presence and Grace Window

Snapshot `presence` tells whether each seat is connected and in reconnect grace:

- `isConnected = false` with `graceExpiresUtc != null`: disconnected within grace window.
- `isConnected = true` with null disconnect/grace timestamps: actively connected.
- `isReserved = true`: seat remains bound to its token owner.

During grace, seat is reserved. Unauthorized clients cannot claim it.

## 4) Grace Timeout Resolution

Disconnect policy options:

- `DisconnectGracePeriodSeconds` default: `60`
- `AbandonmentResolution` default: `Forfeit` (`Draw` supported)
- `RequireBothPlayersConnectedToStart` default: `false`

When grace expires and the seat is still disconnected:

- Match ends deterministically.
- Resolution:
  - `forfeit`: disconnected seat loses, other seat wins.
  - `draw`: `winnerSeat = null`.
- Terminal state is visible in snapshot (`status = ended`).
- Realtime clients receive `match.ended`.

## 5) Boundary and Race Behavior

Near grace deadline, timeout and reconnect races are serialized by per-match dispatch gating.

Observed behavior in integration coverage:

- If timeout resolves first at boundary, reconnect fails and terminal event is emitted.
- Sequence numbers in realtime metadata remain monotonic per match.
- Duplicate event IDs are suppressed.

## 6) Recommended Client Pattern

1. Persist `matchId` + `playerToken` securely.
2. On app launch/foreground: call snapshot endpoint first.
3. Connect hub and subscribe.
4. If connection drops:
   - reconnect hub,
   - call `SubscribeMatch`,
   - call `RequestResync` if needed.
5. On terminal snapshot/event, stop sending moves and show end-state UI.

## 7) Security Notes

- Treat `playerToken` as a secret credential.
- Do not put tokens in URLs for HTTP APIs.
- Avoid logging raw tokens on client and server.
