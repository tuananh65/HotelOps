# Remaining limits

- Proof state is process-local. Restart/recycle, capacity eviction or another instance fails closed and may require another password confirmation. Transparent multi-node proof reuse is not implemented; persistent/distributed storage would require a separate design, concurrency and expiry review. No schema change is hidden in this task.
- Proof is a reusable <=5 minute window bound to actor/session/user rowversion, not a one-time token. Any user-row write may conservatively invalidate proof, including login/profile/counter changes. Session touch alone cannot extend it.
- Employee identity verification is an administrator's offline attestation. Backend records the approved method/receipt time/outcome and verifies the administrator's password independently; it does not claim direct document/employee verification.
- Expiry is tested with controlled backend time plus actual SQL workflow. The production HTTPS child is tested with ordinary real time; no debug time-setting endpoint is added.
- Actual lost HTTP response or commit acknowledgement loss is NOT VERIFIED. Post-commit exception/stale retry is tested separately. No plaintext replay/idempotency cache or automatic recovery rotation.
- Task 34 now retains HOLDLOCK authorization rows/key ranges through reset commit; six actual SQL blocking races verify assignment/role/grant/membership/session/credential serialization. This can increase contention; timeout/deadlock victims fail closed. An operation that serialized before revocation is not retroactively cancelled. See ../CredentialResetRelease/SECURITY_REVIEW.md.
- New proof/service/endpoint behavior is tested on disposable trials. Production TLS/DataProtection, request/response-body telemetry exclusion, least-privilege SQL, old-binary coexistence and broad DEV2 workflows are not certified. Current default Information-log checks pass; do not enable credential body logging.
- Legacy verification builds retain environmental NU1900. New solution/reauth harness builds pass without warnings. Managed secret strings cannot guarantee zeroization.
- The raw legacy AdminReset and conditional primitive remain low-level APIs for backward compatibility. HTTP calls only the internal gated workflow; never wire those primitives directly to an endpoint.
- No commit/push/merge or local artifact deletion. Trial fixtures/databases remain retained; generated TLS test certificate/child process and owned temporary fault/role fixtures are cleaned.

Final functional verdict: REAUTH IMPLEMENTED / RESET IMPLEMENTED for the approved scoped contract, with the operational and unverified transport limits above.
