# DEV3-NEXT-006 — UC023 room directory navigation

Status: IMPLEMENTED_SOURCE_ONLY; independent review pending.
Branch: feature/dev3-uc023-room-directory-results.
Base: 71b97f1030d5da3167795030fa847d043704537f.

## Implemented business slice

Room directory results previously rendered generic DTO key/value fields. Added an accessible table with room number, server branch, floor, nullable area, room type, stored status and active state. Each row offers read-detail and recorded-history links so the user no longer re-enters a room ID manually. Links use the returned room branch even in all-branches results. The existing /rooms/:id route recognizes view=history only for room routes; other views retain existing behavior. Both target reads retain the explicit Load from server action and existing API/session guards.

Reused active RoomsController GET /api/rooms, /api/rooms/{roomId}?branchId and /api/rooms/{roomId}/history?branchId; DTOs in src/HotelOps.Api/DTOs/Rooms/RoomDirectoryContracts.cs and accepted frontend rooms/detail/history decoders. No new endpoint, policy, lifecycle inference, mutation or dependency on DEV2 PR25. Read links do not grant update authority; backend independently checks each request. History scope remains recorded status events only.

## Focused verification

- node --test --test-name-pattern="UC023|global room|session replacement|branch change|late|restricted|anonymous" tests/hotel-router-integration.test.mjs: 9/9 PASS, exit 0. Three new cases cover scoped detail/history navigation, all-branches row binding with absent area/inactive room, empty results and immediate branch-change hiding. Existing denied/global, session-replacement and late-response regressions retained.
- node --test --test-name-pattern="rooms|roomDetail|roomHistory" tests/hotel-api.test.mjs: 3/3 PASS, exit 0. Only existing matching tests counted; no history API case claimed when not selected by repository names.
- npm.cmd run typecheck: PASS, exit 0.
- npm.cmd run build: PASS, exit 0; two inherited React Router use-client warnings.
- Scoped Prettier for the three changed source/test files: PASS, exit 0.
- Staged allowlist, whitespace and secret-pattern scan checked before publication.

Offline browser harness forbids network, browser storage and sensitive output. No backend C# changed and no new backend build/live receipt claimed. Source is additive to previously credited UC023; no duplicate completion credit or whole-UC acceptance claimed.

## Preservation and limitations

Auth/Users, FE12, UC012 Create/Update, backend, schema, permissions and DEV1/DEV2-owned source unchanged. Only the room browse rendering and room view=history initialization change in HotelWorkspace; existing selectors, mutations and routes remain. Existing pagination/filter controls and explicit read dispatch are reused. Backend source/read acceptance is reused; live HTTPS/SQL for this UI path NOT_RUN. No shared SQL, migrations, deployment, activation or merge. UC139 remains PARTIAL with dispatch OFF; UC008 Update remains version-blocked.

## SHA-256
frontend/src/hotel/RoomDirectoryResults.tsx: 401A78F60865621F809404855498DB7FF6AED837EBBC8A8FC151AFC07ED56AFA
frontend/src/hotel/HotelWorkspace.tsx: 69451327E9E2C24C5EB5A2A40CC2460867D85159287A5902287EE2FA693F1B4A
frontend/tests/hotel-router-integration.test.mjs: DE1A89F415ADD58ACCFD1E22D9034659EEAD0DE4F90239188BB9E0E5A5E9063A
