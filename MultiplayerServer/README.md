# MultiplayerServer

`MultiplayerServer` is a server-authoritative .NET backend for multiplayer chess. It exposes HTTP v1 endpoints for match lifecycle and state recovery, plus a SignalR hub for realtime updates and reconnect/resync flows.

## Prerequisites

- .NET SDK 10.0
- Docker (optional, for container run)
- Node.js 18+ (for Newman contract tests)

## Run Locally (dotnet)

From `MultiplayerServer/`:

```bash
dotnet run --urls http://localhost:8080
```

Health check:

```bash
curl http://localhost:8080/health
```

## Run Locally (Docker)

From `MultiplayerServer/`:

```bash
docker build -t multiplayer-server:latest -f .docker/Dockerfile .
docker run --rm --name multiplayer-server -p 8080:8080 multiplayer-server:latest
```

From repo root (`Chess/`):

```bash
docker build -t multiplayer-server:latest -f MultiplayerServer/.docker/Dockerfile MultiplayerServer
docker run --rm --name multiplayer-server -p 8080:8080 multiplayer-server:latest
```

## Test

Unit + integration tests:

```bash
dotnet test tests/MultiplayerServer.Tests/MultiplayerServer.Tests.csproj
```

Newman API contract tests:

```bash
cd newman
npm install
npm test
```

## API Quick Reference

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/health` | Liveness + UTC time |
| `GET` | `/api/v1` | API service/version info |
| `POST` | `/api/v1/matches` | Create a match |
| `POST` | `/api/v1/matches/join` | Join as second player using `joinCode` |
| `POST` | `/api/v1/matches/moves` | Submit a move |
| `POST` | `/api/v1/matches/snapshot` | Fetch authoritative snapshot for cold start/recovery |
| SignalR | `/hubs/v1/matches` | Realtime subscribe, resync, presence, ended events |

## Token/Auth Notes

- HTTP endpoints use `playerToken` inside JSON request bodies where required.
- SignalR hub requires auth token (`Authorization: Bearer <token>` or `access_token` query string).
- For hub methods, the authenticated token must match the `playerToken` method argument.
- Treat `playerToken` as a secret; do not log or expose it in client telemetry.

## Configuration

Disconnect/reconnect policy is bound from `MatchDisconnectPolicy`:

- `DisconnectGracePeriodSeconds` (default `60`, valid range `1..600`)
- `AbandonmentResolution` (`Forfeit` by default; can be `Draw`)
- `RequireBothPlayersConnectedToStart` (default `false`)

Environment variable examples:

- `MatchDisconnectPolicy__DisconnectGracePeriodSeconds=60`
- `MatchDisconnectPolicy__AbandonmentResolution=Forfeit`
- `MatchDisconnectPolicy__RequireBothPlayersConnectedToStart=false`

## Detailed Docs

See `docs/`:

- `docs/api-overview.md`
- `docs/endpoints.md`
- `docs/error-codes.md`
- `docs/recovery-and-reconnect.md`
