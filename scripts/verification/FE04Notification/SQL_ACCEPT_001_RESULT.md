# FE04-NOTIF-SQL-ACCEPT-001 â€” PARTIAL

## Source and live target identity

Remote FE04 consumer HEAD verified: `e76d6fc2ad9915bf03bd5640a4ee6f63c1806550`.
Remote DEV2 Schema002 HEAD verified: `9cda7997416281c1af41e69704ed3bbda1d36eb9`, descendant of that consumer. Imported exactly its 13 schema-only changed paths; source hashes match the ENV owner manifest. `SCHEMA002_SOURCE_PIN.json` records every hash. No C# source, FS013 domain writer, N01 foundation or generated mapping was overwritten. This source integration does not deploy a new runtime.

Approved target: `DESKTOP-JI8U50T\SQLEXPRESS / HotelOps_ENVNOTIF001_Trial_20261109`, GUID `a138fa48-cc14-4070-9ca4-793f1b60c5c1`, owner token `3e67f753-4171-4846-b94d-621368b6a806`, environment owner DEV1 ENV-001 / schema owner DEV2. Lease remains `2026-11-09T11:44:09.1792768Z` through `2026-11-10T11:44:09.1792768Z` (18:44:09 on 10 November, Asia/Saigon). No extension.

## Actual read-only preflight

- Exact SQL instance, database GUID, manifest/SQL owner lease markers and current SQL UTC: PASS.
- All 13 Schema002 source hashes: PASS, including installers, forward and recovery SQL.
- Live Schema002 Preflight.sql / Verify.sql catalog assertions: PASS; existing graph indexes, filters, trusted CHECK/FKs and immutability/source triggers verified without executing installer DDL.
- Prior trigger SQL UTF-16 SHA256: `FD36CBE5829164F56D286CBC3263BB7420758E32BCD0C41D110A52078DA2D1C7`.
- Installed trigger SQL UTF-16 SHA256: `3E791036BF96D648B3930444F1E6AFF48A8FB7AE8C12A8F2C95462AA0F6E3587`, matches captured installation.
- Live counts: tasks 0, assignments 0, operations 0, publications 0, intents 0.
- Setup connection is `DESKTOP-JI8U50T\hax`, database user dbo, sysadmin=1/db_owner=1. The only non-system database principal is `fs013_app`, SQL_USER, authentication NONE, db_owner=0. It has the existing NOLOGIN boundary execute/select grants and DENY graph DML. It is not a login-backed, approved application writer.
- No mutation runner lease was acquired because business mutation prerequisites did not pass. No DDL or business/auth mutation was executed by DEV1 in this task. Existing owner installation evidence is not claimed as a new rollback or writer acceptance test.

## Common target/identity blocker

The approved physical target identity is established. A common **runtime writer** contract is not established: FE04 AvailableAsync still recognizes its original FS013 canonical target and DEV2 IntegrationSqlGuard/CleaningAssignmentStore still recognize KIENPC and the original runtime SQL login. Those safety gates remain unchanged. No new login/grants, EXECUTE AS impersonation, target-literal bypass or activation flag was invented to make a positive writer test pass.

Runtime/schema owner must identify and approve a real restricted application writer and its protected connection handoff, mutation/fixture scope and exclusive-runner protocol. Then DEV1/DEV2 can bind the same exact instance/database/GUID/lease/identity contract on both sides, retaining least privilege, session/branch/source authorization and transaction guards. hax setup permission or fs013_app WITHOUT LOGIN does not replace that approval. The question requesting that existing handoff is pending; dependent mutations remain blocked.

## SQL acceptance: 0 PASS / 0 FAIL / 15 NOT_RUN

All 15 original case IDs remain in `SQL_ACCEPTANCE_NOT_RUN.json`, now pinned to the approved ENVNOTIF001 target. Assignment/publication, mandatory rollback, staging failure, idempotent replay/conflict, concurrent identical/conflicting commands, cancellation, supersession, recipient revocation, branch isolation, delivery retry, Inbox authorization, commit guard and cleanup all require the accepted restricted writer and owned authoritative fixtures. SQL1205 has **not** been exercised; absence of a mutation run is not a deadlock-handling PASS.

Schema002 resolves the previous source issues: the source trigger has explicit assignment, REWORK_SUBMITTED and N01 arms, rejects unknown families, retains assignment revision/deadline checks, and recovery verifies/restores captured prior trigger bytes. Catalog/hash checks PASS; positive/negative trigger writes and persisted rollback/replay roundtrip remain NOT_RUN. No existing history was deleted or altered for cleanup.

## HTTPS evidence

Owned listener PID 6284 and command binary path matched the owner's runtime metadata. Actual assembly ProductVersion: `1.0.0+e76d6fc2ad9915bf03bd5640a4ee6f63c1806550`; SHA256 `0713232FE447585C9558BE7C610833604050684D8B2D18761372A266A50F40F8`.

At `2026-11-10T02:31:43Z`, trusted TLS GET `https://localhost:7029/api/health/database` returned 200; anonymous cleaning-monitor GET returned 401. No TLS bypass, login, public mutation, producer/worker activation, runtime restart or deployment. The live C# runtime is still the accepted consumer e76d6fc; deployed database schema is verified independently against Schema002. Health success is not UC029 acceptance.

## Verification and progress

Schema002 manifest safety: 18 NoSQL cases PASS and all package PowerShell parses. Consumer C# files are byte-identical to e76d6fc, so existing 55 focused DEV1 / 74 Cleaning / 79 N01 NoSQL receipts are reused; no new live coverage is inferred. New composed-source build: PASS, 0 warnings/errors. Previous owner NU1900 vulnerability-feed and EF20602 model warnings remain historical/runtime limitations, not hidden failures or new acceptance evidence.

Preflight/source integration is complete; actual SQL acceptance remains 0/15. Task progress **40%**, covering source identity/import, live target/catalog/hash verification, safety validation and owned HTTPS read-only checks. UC029: source composed and default-OFF; assignment, notification and live business acceptance have no new positive evidence. No accepted-percentage or UC029 ACCEPTED claim.

Next exact action: runtime/schema owner supplies the approved restricted-writer handoff. Bind the common target contract and obtain exact fixture/mutation authority, hold the single Schema002 runner lease, execute all 15 cases sequentially with coordinated two-connection races within cases, record rollback/SQL1205/cleanup receipts, then perform authorized live HTTPS business verification. No other database or FE04/FE05 trial may be used.
