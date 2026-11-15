# FASTTRACK-142A-ACCELERATOR — Checkpoint 84

NEW_COMPLETE_VERIFIED=[]; TOTAL_COMPLETE_VERIFIED=19/144; PARTIAL=125/144.

Fixed Notification Center mutation navigation: when marking the last unread row on page2, return to page1 with the applied filters intact. Previously the list could show an empty page despite remaining matches. Added a reusable SQL/browser fixture with26 timestamp-tied rows plus2 UTC-boundary rows. It verifies 25-row pagination without omissions/duplicates, timestamp/ID order, type/priority/search/date/read filters, selection reset, persisted filtered reads, empty-list recovery and unchanged unrelated recipient markers.

Direct results: backend and ModelProbe builds PASS with0 warnings/errors; frontend typecheck and production build PASS (existing React Router use-client and Vite large-chunk warnings). Solution test exit0, no tests in solution; explicit regression645 PASS/12 SKIP. Focused notification/Auth/Router tests129 PASS. No lockfile change; npm ci not rerun.

- [Baseline HTTPS/SQL/browser trial](checkpoint84-trial1-runtime.json):45 check records, success/cleanup true;6 new filter-browser stages plus11 existing inbox stages.
- [Acknowledgment-capable trial](checkpoint84-ack-trial1-runtime.json):53 records, success/cleanup true;6 filter-browser stages plus existing acknowledgment workflow. Existing harness repeats one stage label; record counts are not independent test/UC counts.
- [SQL baseline](checkpoint84-source-cleanup.txt), [schema rollback](checkpoint84-schema-cleanup.txt), [host/private-file cleanup](checkpoint84-cleanup.json). Optional schema install/repeat/rollback commands exited0. No shared DB mutation. Initial runner invocation was blocked by local PowerShell execution policy before runtime; rerun in a process-local script session succeeded.

UC129 remains PARTIAL for concrete acceptance gaps: UCv3 requires branch/department scope and corresponding filters, while inbox query currently scopes by recipient/live session only. Source opening covers FE05/FE11, not all UC124–128 source families. SRS129 retention-list cutoff and tested filters do not by themselves require JOB06 physical deletion; do not conflate that separate job with inbox completion. Synthetic TASK_ASSIGNED rows here prove inbox behavior, not real producer/source provenance.

User resolved the JOB06 blocker: archive before purge; skip referenced rows until safe, preserve receipts/FKs/triggers, define finite retention/access policy. [Archive design](JOB06_ARCHIVE_DESIGN.md) records a safe unreferenced-row slice and separately reviewed referenced-audit storage migration. Design only: no archive schema/worker deployed and no archive tests claimed. Archive TTL is still a required data-owner policy, not invented from live retention.

Next batch: close notification source/scope gaps using proven source contracts; develop archive-policy/eligibility preflight independently; continue FE11 linked-source and FE13 lifecycle work where approved contracts suffice. This checkpoint closes no whole UC and does not complete the sprint.
