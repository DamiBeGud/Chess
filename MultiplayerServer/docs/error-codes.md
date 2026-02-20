# Error Codes

All HTTP errors return:

```json
{
  "code": "error_code",
  "message": "human-readable message"
}
```

Hub method failures throw `HubException` and also emit `match.error` with:

```json
{
  "eventType": "match.error",
  "metadata": {
    "matchId": "58b02d5d9d5c43cc9a0314a8f1f4a14c",
    "eventId": "4d8f9b3131a74642a3d9fd9c0a0f6a1f",
    "sequence": 7,
    "occurredUtc": "2026-02-20T00:12:14.0000000+00:00"
  },
  "code": "error_code",
  "message": "human-readable message"
}
```

## HTTP Mapping

### Join endpoint (`POST /api/v1/matches/join`)

- `400`: `join_code_required` (and other unmapped join failures)
- `404`: `match_not_found`
- `409`: `match_full`

### Moves endpoint (`POST /api/v1/matches/moves`)

- `400`: `match_id_required`, `player_token_required`, `move_coordinates_required`, `invalid_promotion`
- `403`: `invalid_player_token`, `unauthorized_resume`
- `404`: `match_not_found`
- `409`: `match_not_ready`, `out_of_turn`, `illegal_move`, `seat_not_reconnectable`, `grace_expired`, `match_already_ended`

### Snapshot endpoint (`POST /api/v1/matches/snapshot`)

- `400`: `match_id_required`, `player_token_required` (plus unmapped snapshot failures)
- `403`: `invalid_player_token`, `unauthorized_resume`
- `404`: `match_not_found`
- `409`: `match_already_ended`, `seat_not_reconnectable`, `grace_expired`

## Code Reference

### Validation/input

- `join_code_required`
- `match_id_required`
- `invalid_match_id_format`
- `player_token_required`
- `invalid_player_token_format`
- `move_coordinates_required`

### Authorization/session

- `invalid_player_token`
- `unauthorized_resume`
- `seat_not_reconnectable`
- `grace_expired`

### Business/state

- `match_not_found`
- `match_not_ready`
- `match_full`
- `out_of_turn`
- `illegal_move`
- `invalid_promotion`
- `match_already_ended`

### Realtime transport

- `transport_not_subscribed`
- `transport_forbidden` (defined constant; currently reserved)
