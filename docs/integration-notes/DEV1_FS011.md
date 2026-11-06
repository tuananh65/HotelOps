# DEV1 FS011 integration note

Explicit task authorizes autonomous feature-branch source changes and commit/push; no DEV2/DEV3 per-step approval inferred. New developer and Tester trials only. No existing worktree/shared DB/production permission changes.

| Exact path/symbol | Contract/impact | Reconciliation |
| --- | --- | --- |
| src/HotelOps.Api/Program.cs AddDbContext | Adds CandidateCommandGuard.Instance beside existing commit guard | Same scoped context/owner retained; unarmed transactions unchanged; DEV2 preserve both when reconciling one accepted publisher |
| Services/Notifications/CandidateCommitGuard.cs IsArmed | Internal transaction-identity lookup, not persisted success proof | No context replacement; no universal provider wrapper |
| Services/Notifications/CandidateCommandGuard.cs Verify/hooks | Sync armed EF commands refused; lexical COMMIT/ROLLBACK/BEGIN/SAVE text refused on async EF paths | Defense in depth only; dynamic/self-commit procedures and raw provider/external clients unsupported; values must remain parameterized |
| Services/Notifications/ProvisionalCleaningNotification.cs AvailableAsync | Explicit FS009/010/011/011Tester target+phase tuples; all require Development/local owner+version/constraints | No shared/production flag; exact accepted A01/A02 still missing |
| Services/Housekeeping/CleaningTaskCreationService.cs LifecycleInstalled | Recognizes FS011 and Tester copy of same lifecycle V1 | DEV3 current-cycle/readiness vs historical room-history acceptance remains open; no new workflow states |
| scripts/verification/FS011/BoundaryProof.cs | Actual external assignment reason mutation without new durable records, retained synthetic marker | Residual security risk demonstrated; SQL admin/noncooperating writers not constrained by EF |
| Test-RestrictedWriter.ps1 | Temporary scratch user/DML DENY experiment, transaction rolled back | UPDATE229 proves permission mechanism; a direct DML DENY also blocks current producer. Dedicated signed/owned procedure needs accepted DEV2 schema contract |
| New-TesterTrial.ps1 / Tester-Setup.ps1 | Separate new owner/database, source hash validation, no overwrite/reset; fault-mode gate before SQL | No sharing developer historical evidence DB; source generation scoped, ignored private/generated files excluded from Git/ZIP |

Existing FK/unique/immediate checks cannot defer mandatory publication proof across arbitrary assignment-first SQL writes. No speculative global trigger, shared permission change or new FK introduced. UC028-only proposal lists actual compound source/constructor/inbox contamination; no unverified clean PR claim. No UC030/031/034 implementation, no automatic credit/Tester acceptance.
