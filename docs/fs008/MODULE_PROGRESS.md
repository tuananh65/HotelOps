# Module progress — historical ledger vs current local acceptance

Source: official75-row accepted historical FULL_UC_LEDGER.csv at28f0a5e4f582bc56ba101b48915d33baadeb7b95 plus current FS008 evidence. This ledger is a selected75-UC set, not a complete percentage denominator for all144 SRS UCs/modules. Do not treat code count or mocked tests as credits. Overall historical26/75 and60/256 remain unchanged; no new UC credit assigned in FS008.

| Module | Historical ledger subset | FS008 actual work | Merge/readiness |
| --- | --- | --- | --- |
| FE03 |1row,0credited of5weight | Create retains legacy Room status; current-cycle/pending readiness sidecars prepared/verified with UC028, no full readiness/check-in/block reducer | Partial, not80–90% established |
| FE04 |11rows,2previously credited UCs,4/36weighted points | UC028 VERIFIED_LOCAL real Create; UC029 fail-closed + independent preparation/source-read/B01 proposal, mandatory delivery still missing; UC032/037 regression | Partial; no full module PR or new credit |
| FE05 |10rows,5historically credited,13/34weighted points | No new inspection/conclusion/rework writer; exact current-cycle/SoD future dependencies documented | Historical evidence only; no new completion claim |
| FE11 |5rows,0credited,0/22weight | No task-assignment module implementation in FS008 | Not assessed complete; don't relabel cleaning as FE11 |
| FE13 |10rows,0credited,0/38weight | No Internal Requests writer in FS008 | Not assessed complete |

Separate reports under modules/FE03.md,FE04.md,FE05.md,FE11.md,FE13.md. FE04's new local proof must be reviewed against all UC028 acceptance and shared history/readiness semantics before changing ledger. UC029 cannot receive credit while accepted publisher/delivery/runtime guard/replay missing. No module demonstrated80–90% integration readiness, so no automatic PR into develop.
