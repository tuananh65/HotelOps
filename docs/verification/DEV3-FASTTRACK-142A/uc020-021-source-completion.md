# UC020 / UC021 source completion

New IMPLEMENTED_PENDING_QA: UC020 Create Room; UC021 Update Room Information.
New COMPLETE_VERIFIED: 0. Total source-complete: 51/144; historical QA_VERIFIED: 27/144; source-incomplete: 93/144.

Canonical /structure/rooms and /structure/rooms/:id now expose the existing create/update workflow. Branch structure and management navigation provide scoped Add Room / Manage Rooms links. Directory/detail/history links stay under the canonical room path. Existing forms preserve explicit confirmation, authenticated cookie transport, CSRF, current branch, ETag and change reason. No room-use decision is inferred from stored operational status.

Reviewed existing backend: RoomCreationService validates active branch-scoped room type/floor/optional area, checks normalized room-number uniqueness, inserts active DIRTY room + initial INIT status history + audit in BatchAsync's Serializable transaction. EffectiveRoomReadiness returns PENDING_CLEANING for a new never-passed room when lifecycle capability is installed; missing capability remains UNKNOWN. RoomBatch2Service changes only room metadata and updated_at, retains room ID/status/history and open-work references, and audits reason + before/after after version/reference checks.

Validation actually executed: backend build PASS (0 warnings/errors); frontend tsc/Vite build PASS (existing React Router use-client and >500 KB chunk warnings, ~1002.55 KB JS); 3 focused canonical mocked-browser tests PASS (create, update, scoped directory/detail); 13 EffectiveRoomReadinessTests PASS (focused backend rules). These are mocked transport/browser tests, not SQL/HTTPS business acceptance. No database mutation or schema change in this batch.

Human QA: real Administrator session -> UI -> HTTPS -> disposable SQL; duplicate number, inactive/cross-branch references, optional area, stale ETag, transaction rollback, initial status-history/audit and Pending Cleaning board, and unchanged existing work/history/status after metadata update. No COMPLETE_VERIFIED claim before this acceptance.

Files: frontend/src/hotel/{StructureManagementRoute.tsx,BranchStructureTree.tsx,HotelWorkspace.tsx}; frontend/src/shared-host/SharedAuthHost.tsx; frontend/tests/hotel-router-integration.test.mjs; receipt and matrices.
