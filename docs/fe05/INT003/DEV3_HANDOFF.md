# FE05 INT003 â†’ DEV3 scoped handoff

Candidate baseline: `origin/integration/fullstack-base` fetched and pinned at **e7f84422a33a8a65c771efdfb248e8231e83bb6a**.
Local worktree: `D:/SEP490/Projects/HotelOps-DEV1-FE05-INT003`.
Local branch: `integration/dev1-fe05-checklists`. No integration commit exists: HEAD alone does not identify the new FE05 payload; use SOURCE_INTEGRATION.json and Git status as well.

The target's Program.cs/DI/resolvers, router, authentication, SQL-session, CSRF, DbContext/entities, package lock and Vite config are unchanged. FE05 adds collection GET, final feature components and two existing route wrappers. Do not overwrite shared files with snapshots from the old React/collection worktrees.

## DEV3 deliverables

1. Bind existing `frontend/src/screens/auth/SignInScreen.tsx` to real backend auth; it still calls prototype `services.login` on this exact target. No feature-specific replacement authentication is supplied.
2. Hydrate/refresh session with GET `/api/auth/session`, handle session expiry/revocation/restricted credentials, and POST `/api/auth/logout`. Clear/hide checklist data and forms on logout/identity change, including old in-flight responses.
3. Protect existing `/checklists` and `/checklists/:id` mounts; retain `?branchId=<actual-positive-ID>` and numeric detail ID on login-return/deep-link. Backend remains authoritative for permissions/scope. Mock `currentUser`, `can=true`, demo UUIDs and shell branch labels cannot grant access.
4. Reuse existing `frontend/vite.config.ts`: API_PROXY_TARGET, DEV_HTTPS_CERT and DEV_HTTPS_KEY; HTTPS /api proxy with secure=true/changeOrigin=false. Configure trusted local/backend certificates per existing environment contract; do not weaken host-cookie/TLS rules or add broad CORS to bypass verification.
5. Preserve real cookie/CSRF architecture from `docs/fullstack-integration/AUTH_SESSION_API_CONTRACT.md`; no JWT switch. All unsafe API requests including login require X-CSRF-TOKEN, credentials:include; refresh token after identity replacement. SessionDto does not carry a new permission/branch authority.

The integrated feature adapter already uses relative /api, cookie credentials, fresh CSRF for writes and exact server ETag in If-Match. Collection summaries have no mutation ETag; opening a record loads fresh detail. Coordinate shared transport changes with this existing feature boundary rather than rewriting it.

## RoomType / provisioning

Known-ID create fallback remains. Generic `/api/room-types` requires MASTER_DATA_MANAGE; checklist operations require CHECKLIST_MANAGE. Do NOT grant HK MASTER_DATA_MANAGE or invent permission codes. PL/DEV3/master-data owner must approve a minimal read-only selector contract for active exact-branch types under the appropriate existing authority. Route/DTO and authorization change require explicit follow-up approval; no shared RoomType API was implemented here.

Normal-user CHECKLIST_MANAGE grant/assignment/branch-access provisioning must be reviewed independently. INT001's synthetic Task72 BRANCH grant is not shared catalog acceptance.

## Post-DEV3 verification

When shared auth/router/proxy source changes, run focused checks on the resulting integrated target and an explicitly authorized isolated synthetic runtime:

- Login through shared Sign-In FORM, fresh SQL session/cookie; hydration/reload/deep-link/login-return and logout/identity switching.
- No unauthorized content flash or stale in-flight recovery after logout; restricted/expired session, anonymous 401, no-grant/foreign branch 403 remain distinct.
- HTTPS cookie/CSRF positive and negative checks, ETag preservation and collection/detail DTO roundtrip.
- Representative UC039 Create/Publish and UC040 Draft/new version/Retire with collection refresh and persisted historical FK/content.
- RoomType selector according to approved read scope or explicit PL-approved limitation.
- Timeout/uncertain mutation must reconcile fresh server state before another operation. INT001 Publish v2 persisted after a harness timeout; no blind automatic mutation retry or assumed durable idempotency.

INT003 does not run the 7 live E2E cases again and does not verify shared Sign-In. Existing browser-focused tests use HTTP interception and are not live evidence. Source integration does not imply UC039/UC040 ACCEPTED or permission to push/merge/deploy.