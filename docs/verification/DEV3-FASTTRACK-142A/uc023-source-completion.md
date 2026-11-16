# UC023 Room Directory source completion

2026-11-16. IMPLEMENTED_PENDING_QA; not COMPLETE_VERIFIED.
NEW_SOURCE_COMPLETE=UC023. SOURCE_COMPLETE=43/144; QA_VERIFIED=27/144 historical; SOURCE_INCOMPLETE=101/144.

Canonical entry: Room directory navigation, /room-directory and /room-directory/:id?branchId=...; history can be selected directly with view=history. The integrated read-only wrapper reuses HotelWorkspace and its session-bound transport, resets state on navigation and exposes no create/update/deactivate action. Existing Room Status routes remain separate.

SRS normal flow coverage: branch selection and explicitly server-authorized global query; floor/area/type/status/active filters and literal room-number search; page selection, default floor/room sorting; scoped row links to details and recorded status history. Details include stored status, current status reason/time/source evidence and description. Empty results show MSG10 while the applied selectors remain visible. Effective readiness is now preserved in list/detail decoding and displayed independently of stored operational status; missing/unrecognized readiness becomes UNKNOWN. No room-use decision is inferred from stored status.

Backend reused unchanged: RoomsController validates HTTPS and protected ticket; RoomDirectoryService validates current session, branch/global ROOM_VIEW, consistent serializable count/page/detail/history reads and authorization recheck before disclosure. Existing EF mappings and persistence are unchanged. Source relationships are checked against the selected room; unresolved source references are not invented.

Direct validation: TypeScript/Vite production build PASS; existing React Router use-client directive and large-bundle warnings remain. One focused canonical browser test with mocked transport PASS: list, BLOCKED readiness, absence of mutations, scoped detail/history navigation and zero writes. All 82 hotel API transport/DTO tests PASS. Test bundling initially lacked a CSS loader for the newly imported shared host; the harness now ignores styles for these behavior checks. No backend changes or backend build required in this batch.

Human QA: actual disposable SQL/HTTPS, grant revocation and expiry, branch/global scope, search/filter/page boundaries, status-source consistency and block/readiness changes. The browser check used fixtures and does not prove these runtime cases. No database mutation, migration, grant or permission catalog change was performed. Modified files are the canonical route host, read-only HotelWorkspace integration, RoomDirectoryRoute/Results, readiness decoder, fixtures/tests and these handoff documents.
