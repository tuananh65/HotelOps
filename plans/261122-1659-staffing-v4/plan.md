# Staffing V4 — approved implementation

Authority: current user attachment `82bc7a51-170d-4678-8cc5-509c7bd68b67/Pasted text.txt`. Single agent, AgentKit ak-cook. Target HotelOpsCompactDB on the existing local SQL instance. No application/schema/permission changes, real weekly schedule, email, Git publishing, or legacy database changes.

Approved outcome: preserve 54 identities and access links; reset their credentials; activate39; create Department Head DH002 and Storekeeper EMP050 at DVCT and activate both. All56 use the owner-approved internal-test credential, read into memory from the approval file; never copy credential/hash/token to scripts, reports, audit or terminal. Rotate stamps, revoke existing sessions, clear temporary expiry/lockout using existing auth semantics. Use the actual ASP.NET Identity PasswordHasher with210000 iterations. Global Owner and Admin grants unchanged.

New proposed identities: DH002 Nguyễn Quốc Minh / MinhNQ1 / MinhNQ1@ducvuong.com, DEPT_MANAGER, DVCT, employment HK (same pattern as existing DH001; branch-scoped SHIFT_MANAGE manages all departments). EMP050 Trần Đức Khang / KhangTD1 / KhangTD1@ducvuong.com, STOREKEEPER, DVCT/STORE. Validate identifiers before use. No new department authority/manager designation.

- [x] Fresh preflight: internal environment, exact V3 roster, schema/access/state/concurrency, no duplicate new identities; administrator recovery readiness.
- [x] Backup + VERIFYONLY + isolated restore; implement atomic A/B/C stages, redacted audits, rollback and duplicate guard.
- [x] Rehearse rollback, concurrency rejection, apply, all56 authentication/RBAC and fixture workflows/Workday; recovery proof.
- [x] Fresh primary backup/concurrency check; apply V4 and verify actual primary login56, session invalidation, scope and data integrity.
- [x] Backend/frontend build and relevant tests; review scripts, report exact requested fields, local journal and cleanup owned processes.

Environment evidence so far: HotelOps listeners are loopback; existing Cloudflared service routes blackgrid.site to unrelated gateway5080; gateway /api/v2/auth/me404. No V3-owned API/Vite processes remain. Current V4 starts dedicated loopback API ports only. No pending approval gate.

Recovery: verified full pre-change backup restored into an isolated database; test admin credential recovery from that backup with fresh stamp/session revocation, preserving original identity/permissions. Do not overwrite primary or restore old sessions automatically. Atomic stages commit together or roll back together.

Completed2026-11-22 10:24UTC: primary56ACTIVE/0INACTIVE,40tables/160trustedFK,67permissions/178grants unchanged; newIDs478/479. Audit249-251 batchC7046EC2-244C-4EAA-8799-B9E946422659. Fixture68API PASS, primary61API PASS including56logins, UI7PASS. Backend26PASS+1workerSKIP; frontend4PASS; bothbuildsPASS. Primary Workday0. Sessionrevocation, backuprestore, rollback, concurrency rejection and Adminrecovery verified. OwnedAPI5786/5787 andVite5186 stopped. Report plans/reports/test-261122-1659-staffing-v4-implementation.md. No pending approval or blockers; workerQA test remains explicitly unexecuted.
