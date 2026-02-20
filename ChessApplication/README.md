# Chess Application

## Run

```bash
dotnet run --project ChessApplication/Chess.csproj
```

## Online Multiplayer (T-014)

The client integrates with `MultiplayerServer` HTTP + SignalR contracts (`/api/v1/*`, `/hubs/v1/matches`).

### Configure Server Endpoint

Set the environment variable before starting the client:

```bash
export CHESS_MULTIPLAYER_SERVER_URL=http://134.149.184.47:8080
```

If unset, the client defaults to `http://134.149.184.47:8080`.

To force a local server during development, override:

```bash
export CHESS_MULTIPLAYER_SERVER_URL=http://localhost:8080
```

### Local End-to-End

1. Start server:
   - `dotnet run --project MultiplayerServer/MultiplayerServer.csproj --urls http://localhost:8080`
2. Start one or two client instances.
3. Use the right panel:
   - `Create Match` on host client.
   - `Join Match` with join code on second client.
   - Play on the shared board (server-authoritative state).
4. Use `Resync` after connectivity interruptions.
