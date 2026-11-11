# UC139 targeted guard review

Current tree:b3d8dfe61a528c68002ce6d725d47840604d2e27. Previous accepted contracts are unchanged; no generic audit or owner rewrite was performed.

| Scope | Targeted finding |
|---|---|
| Duplicate scheduled claim | New ScheduledWorkdayClaims.Unclaimed excludes any assignment with Workday history, including ENDED and inconsistent ownership. OwnShiftsAsync and locked Start use the same anti-join; no history deletion or assignment-status rewrite. Fresh ScheduledWorkday51 includes7 mutable-history behavior cases and1 offline EF translation case. |
| Transaction/authority | Existing caller Serializable transaction, User-first/time/assignment locks, SQL-session/resource scope and server SYSUTCDATETIME retained. Participant does not commit; Workday/audit remain in caller transaction. Offline tests do not prove actual SQL race/rollback/exactly-one-winner behavior. |
| Scoped reads | Actor/branch/assignment/department/template eligibility and SHIFT_VIEW vs ATTENDANCE_RECORD boundaries remain; query uses server date and filters claimed assignments. |
| Post-write receipt | ScheduledWorkdayPanel updates its typed receipt only for CONFIRMED data, clears on401/session/branch replacement and renders server start/end. Unknown outcome does not fabricate a new confirmed receipt. Host browser regression47PASS verifies these UI boundaries; all415 canonical frontend input blobs independently matched integration. |
| Production activation | WorkdaysController remains NonController; no Program registration change. UC138 Save/Assign immutable pre-SQL gates remain, unscheduled Start still503 UNSCHEDULED_NOTIFICATION_UNAVAILABLE, Pause All still503 PAUSE_ALL_UNAVAILABLE. No producer or participant activation. |
| DEV2 compatibility | Original24 blobs preserved:UC006 global/inactive/unscoped coverage, UC016 trusted authority/Userâ†’Branchâ†’resource coordination, UC012/022/026 fingerprints and CanDeactivate=false, FE12 Maintenance missing-cycle contract unchanged. NoSql92 exercises these on new assembly. |
| DI | Existing3 scoped DEV2 registrations and integration services preserved; actual registration-prefix probe16PASS on current tree. No source constructor/registration change in PR66. |

Fresh backend/DI409PASS plus host frontend47PASS =456PASS,0FAIL/0SKIP. Build exit0,2NU1900 warnings; DI exposes3 existing EF20602 RoomTypeBranchV1 index warnings, mappings unchanged.

ScheduledWorkdayAcceptance safety review:Program14-17 defaults to exit2 before IO; --prepare35 returns before connections; --execute36+ requires approved receipt/lease/mutation/fault authority, then SQL Open47/56, transaction77 and HTTPS91+. test-guards.mjs invokes synthetic --execute denial. None of these acceptance commands were executed here. See receipts/uc139-70e1d4ee/acceptance_safety_review.json.

No SQL/schema/migration, live HTTPS/mutations, Workday/notification activation or COMPLETE_VERIFIED credit. UC139_ACCEPTANCE_RUNTIME=NOT_RUN; SQL19NOT_RUN; business activation BLOCKED. New-tree frontend47PASS; source-only acceptance remains bounded. WORKDAY_SAVE_ASSIGN_ACTIVATION=OFF; NOTIFICATION_PRODUCER_ACTIVATION=OFF; PR51=INDEPENDENT_MERGE_HOLD.
