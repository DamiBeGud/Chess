# Newman Setup

This folder runs the collection at `./MultiplayerServer.v1.postman_collection.json`.

## Run

1. Start the API server locally.
2. Install dependencies:

```bash
cd MultiplayerServer/newman
npm install
```

3. Execute the collection:

```bash
npm test
```

## Base URL

- Default is `http://localhost:8080` in `local.postman_environment.json`.
- Update that value if your API runs on a different port.

## Optional JUnit Output

```bash
npm run test:junit
```

This writes test results to `./reports/newman-results.xml`.

## Coverage

The collection covers all HTTP endpoints currently exposed:

- `GET /health`
- `GET /api/v1`
- `POST /api/v1/matches`
- `POST /api/v1/matches/join`
- `POST /api/v1/matches/moves`

It includes positive flow plus negative contract checks for:

- `join_code_required`
- `match_not_found` (join + moves)
- `match_full`
- `match_not_ready`
- `invalid_player_token`
- `out_of_turn`
- `invalid_promotion`
- `match_id_required`
- `player_token_required`
- `move_coordinates_required`
- `illegal_move`

MS-005 realtime-only paths (`grace_expired`, `seat_not_reconnectable`, `unauthorized_resume`, `match_already_ended` after disconnect timeout) are validated by integration tests via SignalR, not by Newman HTTP requests.
