# UC014/015 Floor and Area source completion

2026-11-16. IMPLEMENTED_PENDING_QA, not COMPLETE_VERIFIED.
NEW_SOURCE_COMPLETE=UC014,UC015. SOURCE_COMPLETE=45/144; historical QA_VERIFIED=27/144; SOURCE_INCOMPLETE=99/144.

Branch Structure now exposes Add Floor, Add Area and scoped management links. Canonical routes /structure/floors and /structure/areas support browse, existing-record links, detail, create and update. Form confirmation preserves the current session, CSRF and update ETag. Route changes reset state. Saved changes show MSG08; recognized duplicate and parent errors show MSG35/MSG03 with safe local text rather than raw server detail. Canonical navigation does not advertise the unresolved deactivation writer.

Existing backend was inspected and reused unchanged: protected HTTPS endpoints, current MASTER_DATA_MANAGE global/branch permission evaluation and session rechecks; serializable transactions with user/branch locks; normalized branch-wide floor/area code uniqueness; active same-branch optional parent floor; room-parent conflict protection for Area moves; audited before/after values and change reason; strong entity/audit ETag and stale rejection. IDs, related foreign keys and historical references remain unchanged. The two-level Floor/Area schema does not accept an Area as a parent, so no arbitrary cyclic parent graph is introduced.

Source coverage follows SRS UC014 create Active plus audit and UC015 read/version/edit/reason/update plus audit. No generated entities, database schema, grants or backend policy were changed. Deactivation UC016 remains a separate incomplete workflow.

Validation: frontend TypeScript/Vite build PASS with existing React Router directive and large-bundle warnings. Four focused canonical browser tests with mocked API PASS (create/update Floor and Area, explicit confirmation, CSRF, update version and no deactivation link). All 82 hotel API transport/DTO tests PASS. The first build caught missing typed allowlist entries for the three new error mappings; added the exact codes and reran successfully. Backend compilation was not rerun because backend source was unchanged.

Human QA must run actual UI/HTTPS/disposable SQL acceptance: active/cross-branch parents, duplicate and case-normalized codes, current/revoked/expired scoped grants, ETag races, branch activity changes, reason/audit correctness and transaction rollback. Mocked success does not prove SQL persistence. No new COMPLETE_VERIFIED claim or database mutation is made.
