# UC004 trial admission successor (FASTTRACK-063)

Source-only. No database, fixture, certificate, principal or lease is provisioned by this change.

Offline validation, without SQL or HTTPS:

```text
dotnet run --project scripts/verification/UserAccountCreation -- --self-test
dotnet run --project scripts/verification/UserAccountCreation/Http -- --self-test
```

Both entrypoints share the same guard tests; their counts overlap. Certificate error tests exercise the rejection predicate offline, not a real provider TLS handshake.

## Admission contract

Existing positional participant arguments remain `<canonical-trial-database> <new-evidence-path>`. `--entity-reads <canonical-trial-database>` remains a read-only mode. Old argument/name/Task17/schema checks are retained. Every mode now requires external target-bound authorization before connecting:

- `UC004_EXECUTE=APPROVED`, only after runtime owner authorization.
- `UC004_RECEIPT` and `UC004_RECEIPT_SHA256`: path and externally reviewed exact binary SHA256 of a receipt matching the public `Receipt` type in `TrialAdmission.cs`.
- `UC004_TRIAL_SQL`: process-local connection string from the authorized operator; do not persist it in Git or logs. Encryption is mandatory, certificate verification cannot be bypassed, hostname/certificate overrides and failover/attach targets are rejected. Connection retry is disabled.
- Receipt binds actual machine/server/database GUID, login/SID, disposable isolation, owned fixtures, rollback requirement, exclusive lease/start/end, mode, source assembly hashes, and separately recorded schema/fixture/principal approvals.
- Schema and fixture artifacts must exist with exact approved hashes. Fixture JSON must bind `approved`, `databaseGuid`, `owner`, `leaseId`, and `mode`. The caller must obtain genuine owner approvals; writing strings/booleans into a receipt is not independent signoff. Hash verification ensures identity, not cryptographic proof of the approver.
- `PARTICIPANT` authorizes only the pinned participant harness scope, including fixture DML, injected transaction failures and credential/session/audit checks. It is not endpoint E2E certification. `ENTITY_READ_ONLY` does not authorize fixture writes.
- Live read-only proof checks database/server/GUID, original login/SID, isolation marker `HotelOps.UC004.Trial`, and rejects sysadmin, db_owner, db_ddladmin, db_securityadmin, database ALTER/CONTROL, CONTROL SERVER and IMPERSONATE ANY LOGIN. Unknown/null proof rejects. A database-scoped exclusive session applock prevents overlapping admitted runners while its connection remains open. No privilege is granted by the harness.
- The cancellation budget ends no later than the current lease expiry. There is no automatic lease renewal or mutation retry. Existing rollback/commit-uncertainty tests are preserved.

Receipt/SID/marker installation, effective object permissions, schema compatibility, fixture contents and actual lease issuance still need target-bound owner acceptance. The runtime owner must prevent concurrent external writers; an applock is not proof that uncoordinated tools participate. No approved receipt is shipped.

## Historical HTTP suite is not a UC004-only execution path

`Http/Program.cs` also contains UC141/profile/reset tests, a second database, ALTER TABLE, CREATE/GRANT/DROP USER, role fixture deletes and a self-signed HTTPS host. A UC004-only restricted-principal lease cannot authorize that composition. `HTTP_LEGACY` therefore rejects **before any connection, certificate creation, process launch or DML**. The historical scenarios remain for a separately reviewed extraction; no test expectation was weakened to claim PASS.

All historical SQL TLS bypass settings have been removed. The retained HTTPS callback now requires normal chain/hostname validation **and** its pin; a self-signed untrusted certificate is rejected. This does not make the blocked historical host runtime-ready. Next source work is extracting the UC004-only HTTP scenarios against an externally approved HTTPS origin/fixture contract, without carrying forward DDL/principal provisioning or other UC mutations.

Live SQL/TLS, restricted-rights proof, applock contention, lease cancellation during SQL, audit rollback, HTTPS and UC004 E2E remain NOT_RUN. No runtime activation or primary/shared database use is authorized by this candidate.
