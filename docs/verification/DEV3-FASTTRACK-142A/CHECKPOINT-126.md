# FASTTRACK-142A checkpoint126 — approved UC123 Sent/Received boundaries

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=962f56a5e4e1f4dd93411b6d1d510983c0f48d5e
NEW_COMPLETE_VERIFIED=[]
SHARED_DB_MUTATION=NO

The approved direction contract is already implemented by InternalRequestDirection over the IR_VIEW-authorized query in InternalRequestReadService. Sent uses RequesterId. Received uses current handler or an active Department Head's explicit current receiving-department mapping and eligible IR_PROCESS. Direction is a predicate, so overlapping responsibilities do not duplicate rows. Session/read scope and department authority are revalidated within the Serializable read transaction.

Extended SprintRequestDirectionScenario to verify that revoking IR_PROCESS removes the manager path but does not remove a current handler who retains IR_VIEW; Sent also remains requester-based. Revoking IR_VIEW denies both directions even when the same user is requester, current handler and mapped manager. The fixture restores grants and handler before continuing the existing incident-link scenario.

Direct validation in this checkpoint:

- Backend solution build PASS, zero warnings/errors.
- ModelProbe build PASS, three existing EF1002 warnings in SprintCredentialScenario (unchanged).
- Direction/read-access regression: 13 PASS, zero failures/skips. Solution test exits zero but discovers no tests; the explicit regression project is the test evidence.
- checkpoint126-direction-trial1-runtime.json: success=true. Real HTTPS API on an identity-guarded disposable clone verifies Sent/Received, cross-branch exclusion, receiving-department routing, handler replacement, revoked mapping, revoked processing/read grants and no duplicates. The existing incident-link browser/API scenario also passes. The new grant-boundary assertions are HTTP/SQL assertions, not new browser interactions.
- checkpoint126-direction-trial1-cleanup.json: DROPPED_AND_BACKUP_REMOVED. Runtime cleanup=false explicitly delegates cleanup to whole-clone removal; it is not a claim that synthetic rows were individually deleted.

No production source/schema or shared database changes were necessary for this decision. UC123 remains PARTIAL because full linked material visibility and legacy classification acceptance are not verified; zero whole UCs newly completed. The prior aggregate remains 24/144, not recounted here.

The broader frontend run started before this checkpoint remains pending with a previously recorded pre-fix failure; no full-suite PASS claim. Highest-impact follow-up remains FE13 completion prerequisites and linked-work acceptance, with unresolved material contracts kept fail-closed.
