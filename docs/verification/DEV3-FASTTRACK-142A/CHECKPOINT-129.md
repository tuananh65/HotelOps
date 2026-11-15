# FASTTRACK-142A checkpoint129 — notification source revocation acceptance

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=e45ee07feb51ef48d2a67f0612cd2b1a2781884b
NEW_COMPLETE_VERIFIED=[]
SHARED_DB_MUTATION=NO

Extended the real FE13 completion scenario to verify the UC125/GBR157 exception: after the requester's IR_VIEW grant is revoked, the delivered notification remains visible in the recipient's center, while opening the source is denied. This is required historical notification behavior, not permission to access the underlying request. Existing checks restore the grant, verify source opening succeeds, and ensure delivery rechecks current authority.

Direct evidence: checkpoint129-notification-source-runtime.json success=true. The full existing browser close/result/worker/source workflow passed again, plus the new HTTP/SQL notification-list retention assertions. The new revoked-grant list checks are API assertions, not browser interactions. Its runtime cleanup=false delegates to whole-clone deletion; checkpoint129-notification-source-cleanup.json proves DROPPED_AND_BACKUP_REMOVED. No shared database mutation.

Solution build PASS with zero warnings/errors. ModelProbe build PASS with three existing EF1002 credential-fixture warnings. Explicit notification/read-access regression: 116 PASS, zero failures/skips. Solution test exits zero without discovering tests.

Corrected UC125-128 matrix entries that still claimed the canonical notification center did not exist. They now identify the actual shared center and existing producer families, distinguish historical C55/C66/C93 evidence from current C127/C129 execution, and retain PARTIAL for missing material, incident, correction, overdue and low-stock producer acceptance. Verified exactly 144 unique UC IDs remain; no completion promotion or historical test result represented as a new run.

Full frontend release retry remains live (session81908), without a terminal result. No production source changes were required by this checkpoint. Next implementation remains missing producer/source workflow coverage and FE13 lifecycle contracts; this receipt does not certify all UC125 notification families.
