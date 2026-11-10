# FAST014 — ACCEPT010 alignment and blockers

Package pin: DEV2_FE05_N01_ACCEPT_010_HANDOFF.zip, SHA256 E5EA9DE23BC671C7088BA9325733EC22B2D474326E877A179E9F6D6CE8505EE2.
Source identity 9ca9572711d6cc34c532e9530edd4c602bae615215e3a94a3d12eaf80b91fc1e is LOCAL_ONLY_UNCOMMITTED, NOT a Git SHA or runtime acceptance.
116 package hashes and 25 allowlist entries verified. No notification/schema package files copied into production source. Unmodified original contract/executor/protocol files are linked conditionally into the existing FE05 console test harness; pinned ScriptDom 180.140.0 is a test-only dependency. Extracted references remain ignored under TestResults/fast014/accept010/package. No second N01 graph/store/framework.

Original interfaces used, without guessing signatures:
- HotelOps.Sql09.IAcceptedFe05OwnerCaseDriver.ExecuteAsync(string originalCaseId, ExecutionGrant grant, CancellationToken ct) -> Task<OwnerCaseEvidence>.
- HotelOps.Accept010.IAcceptedFe05CaseObserver.RequireBindingsBeforeAsync(AcceptanceCase plan, ExecutionGrant grant, CancellationToken ct).
- HotelOps.Accept010.IAcceptedFe05CaseObserver.RequireActualCaseAsync(AcceptanceCase plan, ExecutionGrant grant, OwnerCaseEvidence actual, CancellationToken ct).

Original L2 IDs are 01–14. FAST013 O01–O14 were an independent scenario list, NOT a valid translation of the original matrix. FAST014 consumes the original L2Plan.json; cases 07/08/10 retain BOTH 'A holds then B contends' and 'B holds then A contends'. No case renumbering or expected-to-actual ordering copy.

Owner candidate invokes exact ComposeRejectInspectionAsync / ComposeInspectionReworkAsync on a retained caller context/transaction/prelocked Users, and original ObserveInspectionOutcomeInCallerTransactionAsync for unknown outcome (no mutation retry). Candidate observer has actual SQL read helpers for source/ledger/graph/issuer/proof identities, never uses structural receipt booleans as authority. Complete accepted per-case fixture hosts/independent observer scopes, authenticated protected inventory/cleanup, grant policy, ordering execution and full proof/recipient/projection verification remain REQUIRED; these abstract bindings are not supplied or claimed complete. Conditional compatibility compile/tests do not make B05 accepted.

AcceptanceGate has immutable ACCEPT010_SQL_EXECUTION_HOLD_BEFORE_CONNECTION for this checkpoint. FE05 adapters and original L2 driver honor it before source/fixture/context factories; no schema/runtime override is provided. Refusal checks are contract/unit evidence ONLY, never L1/L2 SQL PASS. Even a supplied signature cannot waive the hold.

| Blocker | FE05 result | Remaining approval/dependency |
|---|---|---|
| B01 | Original hashes/contract inspected; no import/deploy | FS013 Fix02 + ACCEPT010 exact forward/rollback/prior trigger/catalog/index acceptance missing |
| B02 | No connection to N01 runtime lane; original hold retained | Accepted target GUID/restricted principal/module matrix/explicit authorization/current central exclusive lease/assembly+fixtures |
| B03 | No mock provider/expected error copied as actual | Accepted L1 fixture/catalog and authenticated named-index/provider observation; 24 L1 NOT_RUN |
| B04 | Exact three proposals unchanged; acceptedConcreteBinding=null/private evidence=false | PL approval of existing-code/resource/field/OWN semantics + same-transaction Rework staging metadata |
| B05 | Exact-interface FE05 adapters, original IDs/orderings and real owner-call/SQL observer helpers implemented as candidates | Accepted concrete 14-case fixture scopes/independent observer (not just callback metadata), full committed/replay/rollback/concurrency/cleanup SQL evidence |
| B06 | Original version/content digest, prelock/R05/proof/recovery reused; new actual storage negative control | Durable private namespace/WORM/ACL-equivalent retention not accepted. Original proof/projection + unknown outcome acceptance still missing |
| B07 | FAST012/FAST013 actual commit objects and private remote refs verified (receipt) | No source availability blocker for these commits; runtime source/assembly pin acceptance remains separate |
| B08 | No privilege widening or GUID fallback | Accepted restricted database_guid visibility/attestation missing; no sysadmin/db_owner/VIEW ANY DATABASE workaround |

R05 pin remains 95f7422e284bc81bcfcf9f460dace52022de5cc7. No re-import of R05/Users prelock/recovery/D4. FE04 UC029 acceptance is not FE05 L1/L2 acceptance. No new tasks assigned to other developers.

Reproduce conditional compatibility build after verifying/extracting the exact ZIP to ignored package directory:

    dotnet build tests/HotelOps.Inspection.WriteTests -p:Accept010Inputs=<absolute-package-root>
    dotnet run --no-build --project tests/HotelOps.Inspection.WriteTests -- accept010-compat <absolute-package-root> <owned-output-directory>

The conditional original references must match the integrity manifest. Do not provide a permissive gate, fake signing keys or an unapproved local database as N01 acceptance.
