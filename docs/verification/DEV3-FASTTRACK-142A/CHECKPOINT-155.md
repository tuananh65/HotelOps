# FASTTRACK-142A checkpoint 155 — canonical checklist lifecycle

TASK_ID=DEV3-FASTTRACK-142A
INITIAL_REMOTE_SHA=299f140a86845518176aeac4fa1d709af40329e3
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
NEW_COMPLETE_VERIFIED=0
TOTAL_COMPLETE_VERIFIED=24/144

Connected the existing FE05 checklist workflow to the canonical application. Home now links to Inspection checklists; the empty route asks for an explicit branch before any protected read. Invalid/duplicate branch context remains rejected. A successful branch-scoped CHECKLIST_MANAGE collection response is still required before forms mount, and every subsequent API operation independently authorizes. Session generation replacement, cancellation and unauthorized response handling remain in the existing host. No permission inference from navigation or client flags; no schema or generated-entity change.

The disposable baseline already contains the reviewed Checklist V1 capability. New verification runs only through the existing identity-checked clone runner. Synthetic supervisor role/grant and a bootstrap RoomType are fixture setup, not UC017 acceptance or catalog deployment. Public HTTPS verifies denied manager, CSRF, Draft creation, required If-Match, stale transition rejection, Publish, new Draft, preserved old criterion, replacement publication, retirement, five audit records and revoked-role read denial. Browser uses the canonical Home link, branch admission and checklist forms for the same lifecycle.

Direct validation: solution build PASS, zero warnings/errors; explicit regression project 817 PASS/14 SKIP; solution test exit0 (separate project not discovered by solution command). Frontend typecheck and production build PASS via npm.cmd; initial npm.ps1 invocation was blocked by local execution policy, without changing machine policy. Existing Vite use-client/large-chunk warnings remain. Checklist auth/route33 PASS; focused checklist model/API/route51 PASS. ModelProbe build PASS with three existing EF1002 warnings. No npm ci or full frontend suite rerun.

Trial1 failed after143 records: API lifecycle passed, browser waited for the Home link on the post-login Dashboard. Browser now explicitly navigates to Home after confirmed login. Timeout/security assertions unchanged. [Final trial2 PASS149](checkpoint155-runtime.json): canonical browser branch admission, creation, publication, next Draft, replacement publication and retirement all passed; revoked role returned403. Both clones/backups removed. Independent SQL master query found zero databases with either exact name, both backups absent, HTTPS listeners7142/7143 count zero. Shared/production DB untouched.

UC039/040 remain PARTIAL. Required remaining acceptance includes an already assigned inspection keeping its exact version after publication through this API, audit-fault rollback and concurrent edits/transitions, and SRS MSG08/MSG33 wording. This checkpoint integrates and exercises the actual normal lifecycle, without certifying those missing criteria. UC006/010 deactivation/revoke gates remain in place pending their cross-module writer acceptance. Sprint remains active.
