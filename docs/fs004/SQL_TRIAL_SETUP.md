# SQL trial setup — approved and executed

Exact approved target: `HotelOps_Canonical_20261105_180000_f040001_Fresh` on `.\SQLEXPRESS`, Windows Authentication. The suffix is f040001. The user explicitly approved this new database, 18 reviewed/hash-pinned sources, synthetic seed and login/session/progress/concurrency/audit writes only here. Public certificate export only; no private key export or trust-store changes.

Local machine/instance identity and database absence were checked under a session application lock before CREATE. Result: CREATED_NEW_DISPOSABLE, existingDatabaseWritten=false. No existing database was reset, dropped or overwritten.

[SOURCE_MANIFEST.json](SOURCE_MANIFEST.json) records 18 SHA-256 hashes verified by the runner. [SQL_STATIC_REVIEW.json](SQL_STATIC_REVIEW.json) records scope review. An initial unanchored scanner matched prose inside SQL; the corrected statement-anchored scan passed. The initial scanner was not a successful automated gate; execution had its own hash/target guards and manual scope review.

Order: full schema, RBAC catalog, four baseline compatibility patches, canonical references/seed/verify, users rowversion patch, branch room types, checklists, housekeeping and progress packages with validators. Only sources 0–5 replace HotelOpsDB in memory; original files remain unchanged. The users rowversion patch is required by StartupSchemaValidator but absent in the base installer. Legacy Task17 metadata satisfies package guards, not historical test credit. Ownership marker: HotelOps.FS004.Owner=DEV1_FS004.

Runtime validators: startup Compatible, RoomTypes BranchV1, Checklists V1, Housekeeping and Progress supported. No migrations/generated entity edits or pending Workday/Notification/Lifecycle proposals.

First fixture attempt failed on a directory ACL. Read-only verification confirmed zero fixture users/rooms/tasks: transaction rolled back. Credentials were changed to DPAPI CurrentUser encryption with workspace ACL inheritance preserved; subsequent seed succeeded without reset.

Default commands from repository root are plan-only:
```powershell
powershell -NoProfile -File scripts/verification/FS004/New-ApprovedTrial.ps1
dotnet build scripts/verification/FS004/FS004.Verification.csproj
dotnet scripts/verification/FS004/bin/Debug/net10.0/FS004.Verification.dll plan
```

Setup commands below are historical; **do not rerun creation/seed for reuse**:
```powershell
powershell -NoProfile -File scripts/verification/FS004/New-ApprovedTrial.ps1 -ExecuteApprovedCreate -AllowLocalSqlCertificateException
dotnet scripts/verification/FS004/bin/Debug/net10.0/FS004.Verification.dll schema --local-sql-certificate-exception
dotnet scripts/verification/FS004/bin/Debug/net10.0/FS004.Verification.dll seed --approved-seed --local-sql-certificate-exception
```

Creation refuses an existing target; seed refuses existing fixtures/manifests. Partial setup is retained on failure. No automatic reset/drop exists. Replacement/reset/deletion or broader access requires separate approval. Other databases and existing User Secrets remain untouched.
