# Remaining boundaries and decisions

The additive membership policy is approved. Department replacement, primary Branch changes, transfers, removal, regrant and Deactivation remain outside this endpoint. User has one primary Department; this task adds no multidepartment model.

DEV2 coordination remains necessary for workflows that remove/move responsibilities or deactivate users: exact open-work predicates, assignment/approval/handover writers and shared transaction ordering must be reviewed separately. Add does not remove responsibilities or cancel work. DEV2 should account for fresh SQL Branch membership when evaluating eligibility on subsequent requests; no DEV2 approval or source modification is claimed here.

Destination UserRole rows are conservatively rejected even if currently dormant. Enabling/reconciling them requires a separately approved role-management workflow. Targets with an active, nonexpired OPERATIONS_MANAGER assignment are rejected because safe expansion cannot be proved in this slice. Later role-assignment writers must enforce their own approved scope/cardinality rules; the Add endpoint does not authorize those operations.

Real network response-loss recovery is NOT VERIFIED. Injected postcommit failure verifies stale retry and the absence of duplicate writes, not end-to-end network delivery. No idempotency/recovery or credential reset contract is introduced.

Existing SQL rowversion deployment remains a prerequisite; no migration or production deployment is authorized. Local trial fixtures and receipts are retained. No Commit, Push, Merge, RBAC Catalog edit, generated entity edit or HotelOpsDB write is part of Task 38.
