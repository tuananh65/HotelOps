# FASTTRACK-142A checkpoint110 — UC120 closure prerequisites

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=bed0e3bc3a4a9578b37631677ce419888b99b93c
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=24/144
PARTIAL=120/144
SHARED_DB_MUTATION=NO

Implemented `InternalRequestCompletionWork.ReadKnownBlockersAsync` as a server-side dependency of UC120. It requires the caller's Serializable closure transaction and locks the request using the established update/hold lock. It inspects all known task, cleaning, incident and handover records, independently of UI target visibility. It is neither an authorization service nor a complete closure verdict; no public endpoint or Complete action is enabled by this change.

Mandatory tasks require the existing authoritative task workflow to resolve CLOSED; physical COMPLETED alone, missing workflow state and CANCELLED do not satisfy this rule. Cleaning requires consistent COMPLETED workflow/physical status and completion timestamp. Incidents require CLOSED and closed_at. Optional work does not block merely because it remains open. Cross-branch links block regardless of the optional flag. Source-linked legacy operational tasks and related_incident_id records lacking explicit Required/Optional classification block instead of disappearing. Pending handover blocks independently. The result contains internal target IDs; future UI must independently authorize target disclosure.

[Final trial3](checkpoint110-trial3-runtime.json) directly exercises the SQL queries and asserts: required transaction; unclassified legacy task; explicit-link deduplication; absent/open/inconsistent task workflow rejection; optional work; unclassified legacy incident; missing closure timestamp; closed classified incident; pending handover; pending/completed cleaning; and rollback of the synthetic task, audit and links. Trials1/2 passed earlier subsets. All three owned clone/backup cleanup receipts are retained. These are service/SQL prerequisite tests, not UC120 public HTTP/browser completion evidence; the harness's auth HTTP checks do not change that scope. Existing C109 browser handover acceptance remains historical evidence.

Build: `dotnet build HotelOps.slnx --no-restore` PASS, zero warnings/errors. Actual InternalRequest regression suite72 PASS. Solution-level `dotnet test` exits0 without a test project and is not counted as coverage. ModelProbe builds with three existing EF1002 warnings in credential-fixture code. No frontend/dependency changes; frontend tests/build and npm ci were not repeated.

Read-only inspection of the verified disposable source confirmed that the generated schema has no internal-request escalation table or material-link table. This change does not invent their contracts, assume they are complete, or bypass those prerequisites. No generated entity, shared schema or production DB was modified.

WORKFLOWS_COMPLETED=[]
UI_API_SQL_E2E_VERIFIED=[]
EXACT_REMAINING_GAPS=[UC120 material-link inventory; result persistence; escalation storage and atomic closure; audit/outbox; mapped authority/session/ETag service/controller; canonical Complete UI and full transactional E2E]
NEW_BLOCKERS=[UC120 escalation persistence absent in current schema; material linkage still absent]
NEXT_BATCH=[prepare scoped isolated completion/escalation storage; implement closure service with complete prerequisite inventory; wire requester notification and canonical UI]

Two consecutive zero-new-UC checkpoints have delivered different necessary lifecycle pieces: C109 actual handover mutations and C110 server closure prerequisites. Neither is presented as whole-UC completion. The next implementation remains the closure transaction rather than more peripheral regression expansion. Sprint stays active with its original deadline.
