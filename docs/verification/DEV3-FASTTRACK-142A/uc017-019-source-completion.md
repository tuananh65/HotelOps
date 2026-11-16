# Room Type canonical workflow integration

2026-11-16. UC017 and UC019 IMPLEMENTED_PENDING_QA, not COMPLETE_VERIFIED. UC018 remains PARTIAL_IMPLEMENTATION.
SOURCE_COMPLETE=48/144; QA_VERIFIED=27/144 historical; SOURCE_INCOMPLETE=96/144.

Canonical Branch Structure links /structure/room-types, supporting branch-filtered list, detail links, Create, Update and Deactivate. Existing forms preserve confirmation, current session, CSRF, scoped MASTER_DATA_MANAGE and strong update/deactivation ETag. Legacy global room types remain read-only. Success shows MSG08; deactivation requires MSG33 confirmation and a validated reason.

UC019 now reads active-room references alongside detail under the existing transaction/authorization boundary, displays MSG34 and prevents confirmation when active rooms exist. The write still checks dependencies and ETag again. Inconsistent foreign-branch room references fail closed without disclosing those rooms. No room/history FK is rewritten; deactivation changes only active state with audit. Existing creation validates branch code/checklist and persists Active room type plus audit atomically.

Schema dependency: the existing guarded scripts/database/release/uc017 package and BranchV1 schema gate remain required. No installer was run, no database/schema/grants changed, no schema bypass introduced. Human QA must verify the package on the verified disposable target before runtime acceptance; legacy/incompatible schema yields failure rather than unsafe writes.

Validation: backend solution build PASS, zero warnings/errors; frontend TypeScript/Vite build PASS with existing React Router directive and bundle-size warnings. Four canonical mocked-browser tests PASS: create, update, deactivate and active-room preflight preventing writes. All 82 hotel transport/DTO checks PASS. No relevant Room Type backend automated suite was found in tests; no SQL/HTTPS mutation, concurrency or rollback execution is claimed.

UC018 gap found while checking the whole postcondition: inspection planning reads the current default checklist and preserves prior version links, but no cleaning task consumer of StandardCleaningMinutes was found. The canonical Update action and persistence are implemented, yet full UC018 completion is not claimed. Remaining work is to integrate the cleaning default without inventing deadline semantics.

Human QA for UC017/019: duplicate normalized code, active same-branch published checklist, invalid references, scoped/revoked grants, legacy read-only behavior, concurrent room creation/activation versus deactivation, stale ETag, mandatory reason, audit and transaction rollback. Tests in this batch use mocked API data and do not prove SQL persistence.
