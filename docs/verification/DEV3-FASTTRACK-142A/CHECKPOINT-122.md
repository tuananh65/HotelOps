# FASTTRACK-142A checkpoint122 — UC122 authorization prerequisite

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=15a91daa056201a08b8c6144f646ba8d7bd6ba02
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=24/144 (historical aggregate unchanged)
SHARED_DB_MUTATION=NO

Implemented transaction-bound InternalRequestEscalationAuthority for the SRS UC122 actor. Direct disposable catalog read confirms IR_OVERDUE_HANDLE on OPERATIONS_MANAGER with BRANCH scope. The reader requires an active actor, eligible Operations Manager grant on the exact request branch, and independent current IR_VIEW. It retains user-first and request locks under the caller's Serializable transaction. No grant is inferred from DEPT_MANAGER, IR_PROCESS, home department or global/OWN scope. No public endpoint, session bypass or escalation eligibility verdict is introduced.

Build HotelOps.slnx PASS0 warnings/errors. Solution test exits0 without discovery. New authorization unit tests2 PASS cover actor restriction, branch mismatch, read/process substitutions, global/OWN, expiry and inactive/revoked grant paths. Exact identity-guarded SQL test1 PASS executes the actual reader: Department Head denied, Operations Manager allowed, expired role denied, restored role allowed, removal of IR_VIEW denied. Synthetic catalog/user/request changes are in one Serializable transaction with unconditional rollback; post-rollback baseline user/branch/request emptiness checks pass. Existing role-revoke tests in the SQL scenario still execute; release fuses remain.

UC122 remains PARTIAL: public transaction, permitted action validation, current eligibility, atomic escalation/action/audit/outbox, recipient reauthorization, UI and E2E are not implemented. GBR155 uses SYS-PAR21 (80% allowed time elapsed), but the start of allowed time is not specified; an async question requests the time-origin contract. No guessed CreatedAt formula or mutation is enabled. Authority permits an actor to be evaluated even for closed requests; state/time eligibility belongs to the future service and is not claimed by this reader.

UC105 legacy source requester gap was inspected: existing TaskRequesterSource deliberately supports only MANUAL/INTERNAL_REQUEST, and no production task-creation writer establishes requester semantics for MAINTENANCE/INSPECTION/CLEANING. This guard was preserved. JOB06 archive design remains pending a finite data-owner retention policy and receipt-lifetime contract.

Files: InternalRequestEscalationAuthority.cs, InternalRequestEscalationAuthorityTests.cs, InternalRequestRoleSqlTests.cs, UC_STATUS_MATRIX.md and this receipt. Frontend unchanged; no frontend/browser verification claim in this checkpoint. Next: complete UC122 eligibility/intervention transaction once time-origin semantics are confirmed, and continue independent approved workflow gaps. Sprint remains active.
