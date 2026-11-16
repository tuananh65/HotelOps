# C168 — source completion / human QA handoff

2026-11-16. Canonical branch `integration/dev1-dev2-dev3-unblock-20261114`; initial local/remote `86c2755aa22778202dce8de74edbe5661d092c9d`. Code-first directive applied; no new SQL mutation or production deployment.

`NEW_IMPLEMENTED_PENDING_QA=[UC032]`
`TOTAL_IMPLEMENTED_PENDING_QA=1` (excludes already accepted rows)
`TOTAL_COMPLETE_VERIFIED=27/144` (historical acceptance preserved; new=0)
`TOTAL_SOURCE_COVERED=28/144`

UC032 now closes the source path for an already assigned In Progress cleaning task: item/progress/note/photo save through existing authenticated API/transaction, field-associated MSG03/MSG23 correction, saved-note timeline and protected evidence links. Save100 remains In Progress. This does not certify fresh Start/UC031. Source/QA classification is based on SRS4.5, existing WorkProgressService and guarded Progress V1 persistence, not build alone.

UC135 adds canonical PDF export using the same frozen snapshot, current report actor/grant/scope checks, CSRF and committed export audit. Embedded OFL Noto Sans supports Vietnamese; PDFsharp6.2.4 MIT is new, existing dependencies unchanged. Independent PdfPig parser verifies text and wrapping. Unsupported glyphs fail422 with CSV/XLSX alternative; limits/cancellation never truncate silently. UC135 remains partial for other report families.

All144 rows now carry implementation and verification separately. [HUMAN_QA_HANDOFF.md](HUMAN_QA_HANDOFF.md) includes source gaps, persistence, entry/actor/setup/expected outcomes for implemented workflows and deferred checks. Existing27 accepted rows use the implemented source enum plus COMPLETE_VERIFIED; they are not counted as pending QA. Inventory:28 implemented,97 partial,15 contract-blocked,4 not implemented. UC031 fresh-Start gates and UC038 null inspection/filter gap corrected from overly broad old notes.

| Essential execution | Actual result |
|---|---|
| `dotnet build HotelOps.slnx` | PASS,0 warnings/errors after retry with local NuGet.Config access; initial sandbox restore access denied |
| ModelProbe build | PASS,3 existing EF1002 warnings in SprintCredentialScenario lines62/72/83 |
| Focused ReportPdfTests | 9 PASS; initial8 PASS/1 FAIL was invalid-surrogate InlineData serialization; fixed by constructing surrogate inside Fact, rerun9 PASS |
| EvidenceFileLifetimeTests | 5 PASS |
| Reports API/browser contract tests | 27 PASS, mocked HTTP |
| Cleaning progress client tests | 8 PASS |
| Focused UC032 browser smoke | PASS: existing note/protected link; invalid101 causes no PUT; malformedPNG causes no PUT; correction/save100 sends one CSRF/ETag PUT and remains In Progress. Mocked HTTP, no SQL |
| Frontend typecheck / production build | PASS; existing React Router use-client and >500kB bundle warnings |
| SQL/HTTPS | NOT_RUN this batch; no shared/disposable mutation. C167 SQL/HTTPS results are historical, not PDF proof |

Deferred: complete UC032 SQL/HTTPS acceptance and rollback/revocation matrix; live PDF export audit, viewer review and comprehensive browser E2E; repeated full solution/frontend regression. Existing runtime scenario now includes PDF for human-QA reuse but was only compiled, not executed here. No new harness expansion is required for delivery.

Known blockers: UC006/010 release gates retained; UC031 admission participants; UC038 accepted inspection adapter; FE13 material/legacy source and unresolved receipt/reopen/reroute contracts; JOB06 finite archive retention. New approved RPT03 category/SLA contract recorded in handoff: storage/UI/API still to implement; approved Priority SLA duration config absent, no inferred deadlines or shared migration.

Next highest-impact batch: UC031/034/037/038 missing FE04 source paths; independent FE13 target adapters; UC050/052 approved category/SLA persistence and trusted-actor API where contracts permit. Continue without waiting for human QA. Deadline remains2026-11-17 00:50 UTC+7; at03:44 UTC, about14h06 remained.
