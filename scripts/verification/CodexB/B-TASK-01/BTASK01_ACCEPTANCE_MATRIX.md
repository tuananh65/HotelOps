# B-TASK-01 — Acceptance matrix

| Check | Result | Evidence / limitation |
|---|---|---|
| B root / branch | Verified | D:/SEP490/Projects/HotelOps-Codex-B; ai/codex-b |
| Source baseline | Verified | HEAD 4a187320643d0c82f232c6b9dd5a667b8401c280 |
| Current ledger / A reservations | Blocked | Not available in B; historical totals not proof |
| UC-013 full flow | Not Run | Permission mapping unresolved |
| UC-113 full flow | Not Run | Work interval persistence missing |
| UC-123 full flow | Not Run | Notification publisher and read mapping missing |
| Cookie / SQL sessions / permissions / branch scope | Source reviewed only | No runtime acceptance |
| Validation / negative cases / source-read isolation | Not Run | No implemented endpoint |
| Mutation concurrency / transaction / audit / rollback | Not Run | No mutation implemented |
| Build / framework tests / regression | Not Run | No C# change; 0 cases executed |
| HTTPS / SQL | Not Run | No authorized B trial DB supplied; port probe inconclusive |
| Other worktree state | Not independently verified | Only worktree metadata inspected; no external source read |
| Protected databases | No connections attempted | No database commands issued |
| Git diff --check | Passed for tracked diff | New artifacts reviewed separately; tracked diff empty |

No UC receives IMPLEMENTED_VERIFIED_IN_B. No shared ledger update.
