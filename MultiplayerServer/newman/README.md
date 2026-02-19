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
