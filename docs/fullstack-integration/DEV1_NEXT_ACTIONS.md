# DEV1 next actions

Use feature/dev1-room-ops from the exact verified integration commit, not an integration/handoff branch for feature development. Start with source/API/UI mapping for FE03/04/05/11/13 and existing room directory,housekeeping assignment/progress,inspection contracts. Reuse existing UI, plan DTO/error/ETag adapters; this task does not authorize a new business UC.

First review recovered UC038/UC037 controller activation and legacy route/DTO differences with DEV3. UC037 still lacks full Cancel/lifecycle/notification. Approve room lifecycle dimensions/current-cycle and permissions before real transitions.

UC107 remains unauthorized until every approved contract arrives. FE13 history includes withdrawal, not just read-only browsing. Coordinate FE12/workload intervals with DEV3 and notification/source-read/transaction participation with DEV2. Assignment elapsed time is not execution effort. Never omit audit,concurrency,scope or durable notification to claim acceptance.
