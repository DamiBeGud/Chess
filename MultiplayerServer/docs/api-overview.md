# API Overview (v1)

## Base URLs

- HTTP: `http://localhost:8080`
- API prefix: `/api/v1`
- Realtime hub: `/hubs/v1/matches`

## High-Level Lifecycle

1. Create match: `POST /api/v1/matches`
2. Join match: `POST /api/v1/matches/join`
3. Submit moves: `POST /api/v1/matches/moves`
4. Recover state any time: `POST /api/v1/matches/snapshot`
5. Realtime sync/resync: SignalR `SubscribeMatch` and `RequestResync`

## Seats and Turn Model

- Creator seat: `White`
- Joiner seat: `Black`
- `sideToMove` is always one of `White` or `Black`
- `moveNumber` starts at `1` and increments after each accepted move

## Snapshot Contract (Canonical State)

`MatchSnapshotResponse`:

- `matchId`: string
- `sideToMove`: `White` or `Black`
- `moveNumber`: integer (`>= 1`)
- `board`: 8 strings, each length 8
- `status`: `in_progress` or `ended`
- `resolution`: `forfeit` or `draw` when ended, otherwise `null`
- `winnerSeat`: `White`/`Black` for forfeit, otherwise `null`
- `presence`: creator/joiner seat presence details

## Board Encoding

- `board` has 8 rows, top-to-bottom from Black side to White side.
- Initial board rows:
  - `rnbqkbnr`
  - `pppppppp`
  - `........`
  - `........`
  - `........`
  - `........`
  - `PPPPPPPP`
  - `RNBQKBNR`
- Piece symbols:
  - White: `PRNBQK`
  - Black: `prnbqk`
  - Empty: `.`

## Presence Model

Per seat (`creator`, `joiner`):

- `seat`: `White` or `Black`
- `isConnected`: realtime connectivity state
- `isReserved`: seat reserved for that player token
- `disconnectedUtc`: UTC timestamp when disconnected
- `graceExpiresUtc`: UTC timeout for reconnect grace

## Realtime Event Model

Events include deterministic metadata:

- `matchId`
- `eventId`
- `sequence` (monotonic per match)
- `occurredUtc`

Event names:

- `match.snapshot`
- `match.updated`
- `match.presenceChanged`
- `match.ended`
- `match.error`

See `endpoints.md` and `recovery-and-reconnect.md` for usage and payload examples.
