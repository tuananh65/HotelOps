# FASTTRACK-142A checkpoint106 — scoped incident reads

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=623a3647916e407b671a92afcd71088a8cfccac7
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=23/144
PARTIAL=121/144
SHARED_DB_MUTATION=NO

Canonical Incidents now supports authenticated list and detail through source-owned contracts for A004A-022/023. It no longer depends on the machine-specific demo contract. Current same-role INCIDENT_VIEW and branch access are required. GBR165 limits Maintenance Staff to reported or assigned incidents even when their catalog grant is BRANCH; Maintenance Lead sees the granted branch. Filtering happens before count/pagination. Incoherent location/asset/assigned-work department branches fail closed. Serializable reads revalidate current session/authority before disclosure. No incident write contract is activated. OWN-only grants and global assignments without a branch are not enabled by this slice.

The canonical auth host mounts a read-only Incidents workspace, supports validated post-login list/detail returns, and hides mutation actions and unrelated modules. Existing raw mutation methods retain their guards. No generated entity, permission catalog, shared schema or production database changed. The inherited disposable fixture also verifies its existing department-mapping capability; that is fixture setup, not a new production migration.

## Direct execution evidence

[Trial4](checkpoint106-trial4-runtime.json) success=true: Edge login, scoped incident list and assigned detail through trusted HTTPS/SQL; unrelated incident absent. Lead branch pagination, missing/foreign branch denial, invalid pagination, empty filter, staff count narrowing, independent detail ownership, reassignment and revoked-role denial, and rejected incident mutation all passed. [Cleanup](checkpoint106-trial4-cleanup.json) confirms DROPPED_AND_BACKUP_REMOVED. Runtime cleanup=false delegates cleanup to the whole-clone receipt. Its inherited top-level scope label still names department-authority verification; the incidentReadCheck/incidentReadBrowser entries are the precise evidence, and this is a read workflow, not public mutation E2E.

Trials1/2 exposed a real missing caller-owned transaction during preliminary session validation (503); corrected in IncidentReadContract. Trial3 passed API scope checks but exposed missing canonical frontend routing; corrected in auth host and validated return URLs. All four trial cleanup receipts confirm clone/backup removal. Trial3 cleanup required a guarded retry after verifying no remaining clone sessions. No failed attempt is counted as PASS.

REGRESSION_STATUS=dotnet build HotelOps.slnx PASS0 warnings/errors; dotnet test HotelOps.slnx exits0 but discovers no test project; actual IncidentReadAccessTests3 PASS; frontend shared-auth-host/dev2-ui132 PASS1 pre-existing SSR SKIP; typecheck and production build PASS. Initial sandbox NuGet.Config access failed; authorized rerun succeeded. Vite use-client/large-chunk and Node module.register deprecation warnings remain. ModelProbe has3 inherited EF1002 fixture warnings. Legacy action offline harness1156 PASS with SQLconnections0; its fullyAcceptedBusinessActions=0 is not a whole-UC acceptance metric. npm ci not repeated; dependencies/lockfile unchanged.

WORKFLOWS_COMPLETED=[canonical Maintenance Staff incident list to assigned detail with current scoped access]
UI_API_SQL_E2E_VERIFIED=[incident list/detail; current assignment and role revocation denial]
EXACT_REMAINING_GAPS=[UC143 category/assignee/deadline/overdue/department/period/search filters, resolution-deadline ordering and full evidence/progress; FE13 linked incident/material visibility adapters; UC123 Received semantics awaiting decision; UC114/117/118/120 lifecycle acceptance]
NEW_BLOCKERS=[]
NEXT_BATCH=[close remaining independent FE13 lifecycle behavior; finish UC123 direction after approved contract; complete linked-work target authority without borrowing IR_VIEW]
SPRINT_TIME_REMAINING=approximately20h57 at2026-11-15T20:53Z; deadline2026-11-16T17:50Z unchanged.

This is another zero-new-UC batch with a concrete missing browser/API business path delivered, not a documentation checkpoint. UC143 remains PARTIAL. Existing23 COMPLETE_VERIFIED UCs are historical acceptance, not re-executed in this batch. Sprint remains active.
