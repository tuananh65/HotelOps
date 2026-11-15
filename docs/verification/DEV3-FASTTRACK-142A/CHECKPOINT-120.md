# FASTTRACK-142A checkpoint120 — historical operational task visibility

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=2426847c8e45f930abaa223280f3ab27425a89f5
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=24/144 (prior accepted aggregate)
SHARED_DB_MUTATION=NO

UC123 linked operational task history now includes existing SourceType=INTERNAL_REQUEST / SourceId references as well as explicit links. The union is a predicate over independently TASK_VIEW-scoped tasks before count and paging; it does not duplicate tasks matching both. Existing final authorization checks protect rows and totals. Source attribution does not grant read authority. Missing explicit completion metadata is represented as nullable Mandatory and rendered as Completion requirement unclassified, never Optional. Candidate selection and POST still require an explicit Required/Optional choice. UC120 continues blocking unclassified legacy references; no closure semantics, generated entity or database schema changed.

Direct evidence: checkpoint120-trial3-runtime.json success=true, including trusted HTTPS reads of synthetic historical completed tasks, source/explicit overlap once, foreign branch and wrong source type exclusion, independent TASK_VIEW revocation hiding all rows and total, and real Edge rendering of the unclassified label. The existing linked creation/progress flow also passed, including idempotency, concurrent201/412, injected failure rollback, deadline audit and handler revocation. Top-level runtime scope text is inherited; linkedCreationCheck and linkedCreationBrowser entries describe actual coverage. Runtime cleanup=false delegates to the separate whole-clone cleanup receipt; all three trial cleanup receipts confirm DROPPED_AND_BACKUP_REMOVED.

Trial1 failed because the probe binary preceded the new legacy fixture; rebuilt before retry. Trial2 failed on an invalid synthetic task source_type=INCIDENT; actual CHECK permits MAINTENANCE, which is now used for the unrelated-source negative fixture. Constraints were not weakened. Both failures are retained as evidence.

Validation: backend solution build PASS0 warnings/errors; solution test exits0 with no test discovery. Explicit InternalRequest regression77 PASS,1 environment-gated SQL test SKIP (C119 separately verified that SQL test). Frontend production build/typecheck PASS. History browser suite initially29 PASS/1 FAIL due to exact-text selector including an em dash; corrected targeted suite4 PASS, covering legacy label, revoked visibility and successful/stale links. ModelProbe retains3 existing EF1002 warnings; Vite retains use-client/chunk-size warnings. No claim that the whole frontend suite was rerun after the selector-only correction.

No new whole-UC completion. Remaining UC123/117/118 gaps: material target authorization/visibility, legacy completion classification and missing creation adapters. UC119 receipt-read scope and UC120 material terminal contract remain separate unresolved business contracts. Next: continue highest-readiness FE13 lifecycle work under approved contracts, preserving fail-closed unresolved paths.

Changed product files: InternalRequestTaskLinks.cs and TaskLinksPanel.tsx. Evidence fixtures: SprintLinkedTaskCreationScenario.cs, SprintInternalRequestTaskLinksScenario.cs, sprintLinkedTaskCreationBrowser.mjs and internal-request-history-browser.test.mjs. UC matrix updated without changing completion counts.
