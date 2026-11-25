# OWNER/SYS_ADMIN domain boundary — 25/11/2026

Latest runtime11/11 PASS on two active branches. Original13 GET checks covered three branches while another synthetic branch was active; preserved global-role-read-thirteen-pass.json. Latest global-role-boundary-eleven-pass.json adds two real POST attempts using valid branch/warehouse/item IDs and complete CreateReplenishment DTO. Both roles return403 PERMISSION_DENIED; actor-scoped work/document/stock movement counts remain unchanged. OWNER has only view grants; SYS_ADMIN correctly does not inherit operational domain permissions. No role/product changes.

Changed verification runner v2-global-role-read-scope.mjs and fixture action DomainBoundary in v2-invitation-fixture.ps1. node --check and PowerShell AST PASS; cleanup deleted both owned sessions and stopped5778, zero fixture accounts created. Backup CHECKSUM/VERIFYONLY was fresh. No operational database changes, password changes, email or git writes.

Preserved failed harness attempts: global-role-boundary-snapshot-failure.json (wrong stock movement join; corrected using line_id) and global-role-boundary-error-envelope-failure.json (ProblemDetails title instead of code). These were not product failures or PASS.

Scope limit: representative replenishment authorization boundary, not every write endpoint in system.
