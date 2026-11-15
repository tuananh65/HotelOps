# FASTTRACK-142A checkpoint130 — source opening records notification read

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=b3dc9b6cc9c6253895ebb67d55491b60152c5610
NEW_COMPLETE_VERIFIED=[]
SHARED_DB_MUTATION=NO

Fixed a concrete UC125 normal-flow gap: NotificationCenter previously resolved an authorized source and navigated without marking its recipient notification Read. openInboxSource now resolves the allowlisted current source, verifies the same session generation, obtains CSRF and posts the existing notification read endpoint, checks generation again, then returns the route for navigation. Denied/invalid source paths never trigger a read mutation. A failed read confirmation does not navigate or silently retry. This does not acknowledge a notification or update its business source; explicit acknowledgment remains separate.

Direct validation:

- Nine focused frontend tests PASS: canonical path allowlist, denial, selected read, CSRF sequence, no mutation after source denial, generation changes after source/CSRF/read completion, and no retry/navigation after read failure.
- TypeScript and production build PASS. Existing react-router use-client and >500kB bundle warnings remain.
- Backend solution build PASS, zero warnings/errors; ModelProbe build PASS with three existing credential-fixture EF1002 warnings. Solution test exits zero without discovering tests. No backend product code changed.
- checkpoint130-notification-open-runtime.json success=true: real browser closes request, receives hosted-worker notification, opens source, and SQL confirms ReadAt for the exact recipient. Existing once-only closure/delivery, CSRF/ETag, stale replay, revoked source access, retained notification text and restored access checks also pass.
- checkpoint130-notification-open-cleanup.json proves owned disposable clone and backup removal. Runtime cleanup=false delegates to whole-clone removal. No shared/production mutation or schema change.

The full frontend release retry remains live in session81908. It began before this frontend change, so its eventual result alone cannot certify this final revision; focused tests and direct browser evidence above cover this change. UC125 remains PARTIAL for producer families not yet implemented/certified. No whole UC promotion. Next: remaining FE13 and notification producer acceptance under approved contracts.
