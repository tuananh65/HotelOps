# Synthetic fixtures

Only synthetic data in the exact approved trial; no production import/restore. Canonical references provide two branches/eight departments. IDs are looked up through existing codes.

Four active accounts: assigned housekeeper and another housekeeper (HK_STAFF branch-one), denied account (OWNER without ROOM_VIEW), setup administrator (SYS_ADMIN plus HK_SUPERVISOR branch-one). Required UserBranchAccess rows are present; no catalog/policy changes.

25 branch-one rooms, two branch-two rooms, scoped floors/types. One IN_PROGRESS task assigned to owner with two frozen cleaning snapshots; another task exercises inactive-phase409. Legacy-compatible fixture checklist is a prerequisite, not UC039/inspection credit.

Passwords are random and hashed by production IdentityV3 service. DPAPI CurrentUser ciphertext: .private/credentials.dpapi. .private/fixture.json contains synthetic metadata only. Workspace ACL inheritance is preserved; secrecy does not depend on a claimed current-user-only directory ACL. Decryption is in-memory child IPC only.

Ignored .private also holds logs, PID receipts, public PEM and synthetic evidence. No private-key file exists. Same Windows DPAPI identity and matching target/owner/schema are required for reuse. Baseline-count tests passed twice without reset/reseed; historical progress intentionally persists.
