# UC004-only HTTPS runner — FASTTRACK-064

Stacked on PR90 (`25d85e2468b7dadcef440adaa9f1511ed5702208`). This is source-only; no runtime authorization or fixture approval is supplied.

## Commands

Offline: `dotnet run --project scripts/verification/UserAccountCreation/Http -- --self-test`.

Only after explicit target-bound approval:
`dotnet run --project scripts/verification/UserAccountCreation/Http -- --uc004 <canonical-disposable-database> <new-evidence-path>`.

The existing `UC004_EXECUTE`, receipt/hash, restricted connection, schema/fixture hashes, live GUID/SID/isolation, lease and exclusive connection guards still apply. Receipt and fixture mode must be `UC004_HTTP`. No principal, schema, certificate, fixture or host is created. The historical mixed runner remains rejected.

## Additional fixture contract (candidate, requires owner acceptance)

The hashed fixture's `http` object uses the exact case-sensitive `HttpFixture` record properties in `CreateOnlyRunner.cs`. It binds approved HTTPS `Origin`, SHA256 leaf `CertificateSha256`, `DeploymentApproval`, `DeployedApiSha256`, `DatabaseGuid`, `LeaseId`, a fresh synthetic `Prefix`, two branch/department pairs, and three distinct synthetic actors (`Allowed`, `Denied`, `BranchBound`). Each actor carries its real fixture `UserId`, `Username`, and the name of a local `UC004_SECRET_*` environment variable, never a password value. Manifest schema and seed/provisioning are not approved by this source change.

Runtime Owner must attest the deployed API hash and its database binding: the existing health API does not expose a trustworthy database GUID. The receipt hash pins that external evidence; it cannot independently prove which database a remote API uses. Require the exact deployed binaries, matching configuration, isolated host and exclusive lease before authorizing execution.

`AllowedOperations` must contain exactly `AUTH_LOGIN`, `UC004_CREATE`, `UC004_DUPLICATE_CONCURRENCY`, `SQL_READ_BACK`. Allowed actor requires accepted global USER_MANAGE + ROLE_MANAGE; denied actor must lack admission; branch-bound actor must be denied at the second branch. Provision both active HK departments and the existing HK_STAFF catalog role under approved fixture rules. Runner neither grants nor rewrites permissions. Real API responses are authoritative.

## Coverage and limits

- Real cookie/CSRF bootstrap and login, successful Create, malformed input, missing/invalid session, missing CSRF, permission/scope denial, deliberate duplicate and concurrent duplicate requests.
- SQL read-back checks user, credential hash, initial role/membership and two sanitized audits. Negative requests compare deterministic domain/audit fingerprints, excluding auth-session tables where legitimate login/session touches occur.
- TLS requires normal chain/hostname verification AND SHA256 pin; redirects and retries are disabled. Outcome-unknown transport errors stop execution, without replay. Lease cancellation bounds requests; in-flight commit uncertainty requires owner reconciliation before reuse.
- Evidence uses CreateNew and contains only named checks and artifact/lease bindings. No response bodies, credentials, hashes of passwords, tokens or raw exception details are emitted.
- Storage-fault rollback remains NOT_RUN: historical ALTER TABLE fault injection was deliberately not copied. It needs an approved external fault profile/mechanism, expected HTTP classification and persisted rollback proof. Negative-request unchanged-state checks are not proof of injected-fault rollback.
- Preexpired SQL session scenario needs an approved preexpired session fixture. Invalid-ticket and missing-session cases are executable now; neither proves session expiry timing.
- All live SQL, TLS handshake, HTTPS, transaction/concurrency and audit acceptance remains NOT_RUN until authorized. Main/shared databases are forbidden. No cleanup deletes are performed; runtime owner retains cleanup/rollback ownership.

## Integration scope

Allowlist: `TrialAdmission.cs`, `Http/Program.cs`, `Http/CreateOnlyRunner.cs`, `Http/CreateOnlyTests.cs`, `Http/CREATE_ONLY.md`. No production source or feature gates changed. Promote PR90 before this stacked increment, or perform a separately reviewed composition; do not duplicate PR90 commits on integration.
