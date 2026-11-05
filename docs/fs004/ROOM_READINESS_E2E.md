# Room Readiness live acceptance

Existing React board and GET /api/rooms use actual SQL fixtures. Evidence: rooms-live in [LIVE_RESULTS.json](LIVE_RESULTS.json), frontend/tests/fs004-live.spec.ts.

- Authenticated200, anonymous401, denied403, foreign branch403, invalid query400.
- Branch-one total25; disjoint pages20+5, no branch-two leakage.
- Real board renders fixture rooms, pagination and empty-search state.
- Actual browser offline mode produces an error without mock fallback.
- Browser → gateway → Vite proxy → ASP.NET Core → SQL → React is exercised.

Failure test is a real network failure; SQL outage/backend500/503 was not injected. This verifies the directory/readiness read slice, not assignment, Room Ready confirmation or pending lifecycle permissions.
