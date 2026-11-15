# Checkpoint 61 — UC043 defect workflow acceptance

`TASK_ID=DEV3-FASTTRACK-142A`; sprint remains ACTIVE, original deadline unchanged. Previous goal turn was progress: checkpoint60 committed source fixes and verified UC042. This checkpoint completes the next related workflow without a broad restart audit.

**Newly completed: UC043 — Record Inspection Defects (1 UC). Totals: 10 COMPLETE_VERIFIED / 134 PARTIAL / 144.** See [UC043_ACCEPTANCE.md](UC043_ACCEPTANCE.md) for scope and requirement-by-requirement evidence.

## Implementation

Invalid upload input now returns HTTP400/MSG23 through a ValidationException subtype. The shared upload helper preserves every validation/storage check; other consumers still catch the existing base exception. Storage, IO and audit/commit failures remain distinct and cannot trigger a corrective-input retry. The client displays the fixed policy message without exposing server internals.

After current authentication, role, permission, branch and inspector checks, terminal defect writes validate If-Match and return HTTP409/MSG24 for Passed or Failed. A stale request remains412. This terminal refusal also works after Request Rework changes the cleaning lifecycle. The existing restrictions on editable states, current cycle and generic reinspection writes remain. The read-only form displays MSG24; a concurrent conclusion response clears the editable source until an explicit GET.

Defect descriptions now accept CR/LF according to the SRS multiline field while remaining required, bounded to4000 characters and guarded against other controls. Location and severity requirements are unchanged. Successful defect add/edit displays MSG08 after server acknowledgement.

## Direct validation

| Check | Actual result |
|---|---|
| `dotnet build HotelOps.slnx`; ModelProbe build | PASS, 0 warnings/errors |
| `dotnet test HotelOps.slnx --no-restore` | Exit0, no solution test project; not counted as coverage |
| Actual Dev1 regression project | 552 PASS, 11 SKIP, 0 FAIL |
| Frontend typecheck / production build | PASS; final bundle692.49 kB |
| Inspection client + Auth/Router + shared host invocation | 156 PASS, 0 FAIL/SKIP; `checkpoint61-frontend-tests.txt` |
| Inspection mocked-HTTP browser suite | 13 PASS; `checkpoint61-browser-tests.txt` |
| Real add/edit/evidence workflow | `checkpoint61-defect-execution-final.json`: PASS, multiline description, MSG08/23, invalid-file correction, format/size/count rejection, audit/file rollback, edit in both allowed states, stale/foreign rejection and persistence |
| Actual Passed/Failed terminal locks | Both receipts PASS for normal and blocked rooms; POST/PUT409 with scoped MSG24, stale412, immutable snapshot and disabled form |
| Actual Reject → Request Rework handoff | `checkpoint61-defect-rework-handoff.json`: PASS,20 composed check records across normal/blocked cases, exact defect links and locks after rework creation, associated lifecycle/authorization/evidence/notification probes and per-case cleanup |
| Independent cleanup | PASS, `checkpoint61-cleanup-final.txt`; private files removed, original guards restored, owned hosts stopped |
| npm ci / full frontend suite | Not rerun; lockfile unchanged. Earlier broad results remain historical. |

The execution fixture deliberately faults Save results, Record defect and Finish audit inserts. HTTP500 and SQL/pipeline error output are expected test stimuli; snapshots/ETags and final committed row/file counts prove rollback. Only the two legitimate private files remain before final cleanup. The final execution receipt adds the server-side10MiB+1 boundary to the earlier passing `artifacts/checkpoint61-defect-execution.json` trial.

The first new mocked-HTTP concurrent-conclusion test timed out because the harness used an exact Severity label that did not match the current select's accessible name. It was corrected to the existing combobox selector; the focused rerun and full13-test suite passed. This was a harness failure, not counted as PASS or hidden. Actual SQL/HTTPS trials passed on their first executions.

Warnings retained: three unmapped RoomTypeBranchV1 indexes and unordered row-limiting at runtime, Vite React Router `use client` and >500kB bundle notices, browser NO_COLOR/FORCE_COLOR warning. No dependency downgrade, authentication bypass or warning suppression.

## Safety, remaining work and delivery

All SQL mutation was synthetic, confined to `meita / HotelOps_DEV3_142A_20261114_cc78c13f`, GUID `1D5A9B53-7D19-435A-96AB-9FDFF447B6E0`, marker `cc78c13ffc124efcad67b3d804baa84c`. Each fixture verifies identity/baseline and rollback cleanup before commit. No shared HotelOpsDB, generated EF entity, migration, persistent schema or permission catalog change. The rework trial reuses existing temporary protected capabilities and removes them; hosted notification scheduling is not claimed.

Next highest-impact implementation: UC041 open-inspection reassignment. Next: UC048 history acceptance and the remaining FE11/13 plus UC006/010 lifecycle gaps. These remain PARTIAL. The complete sprint is not claimed achieved.

Initial local/remote baseline: `c55c590e3388ecd32c2218804fa4e231d89e0f90`, canonical `integration/dev1-dev2-dev3-unblock-20261114`. Normal authorized commit/push follows branch/tree/remote checks.

Changed product/test files:

- `src/HotelOps.Api/Services/Housekeeping/EvidenceInputException.cs` (new)
- `src/HotelOps.Api/Services/Housekeeping/InspectionExecutionService.cs`
- `src/HotelOps.Api/DTOs/Housekeeping/InspectionWorkflowDtos.cs`
- `src/HotelOps.Api/Controllers/HousekeepingWorkflowController.cs`
- `frontend/src/features/inspectionExecution/{api.ts,model.ts,InspectionWorkspace.tsx}`
- `frontend/tests/fe05-inspection-execution.{test.mjs,spec.ts}`
- `frontend/scripts/inspectionDefectTerminalChecks.mjs` (new)
- `frontend/scripts/{sprintInspectionBrowser,sprintApprovalBrowser,sprintRejectionBrowser}.mjs`

Documentation/evidence: decision61, UC matrix, this receipt, UC043 acceptance, four runtime receipts, cleanup and two focused test logs.
