# FASTTRACK-142A checkpoint121 — legacy incident linkage

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=fd4b9fdf8b1dc0711107cafb528a248eb9b64b69
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=24/144 (historical aggregate unchanged)
SHARED_DB_MUTATION=NO

UC123 linked incidents now include the existing InternalRequest.RelatedIncidentId alongside explicit link records. Both paths intersect existing IncidentReadAccess; IR_VIEW does not confer incident access. Count/pagination operate over one incident query, preventing duplicate rows when both references identify the same target. Existing final session/target authorization checks remain. Unclassified legacy completion metadata is nullable and displayed as Completion requirement unclassified. An explicit Required/Optional link takes precedence once created with current authority, CSRF and ETag. No generated entities, schema, closure predicate or security policy changed.

Direct evidence: checkpoint121-trial1-runtime.json success=true. Real HTTPS checks cover legacy own-target visibility, hidden/foreign target exclusion, null classification, explicit link deduplication/classification, target role revocation, existing CSRF/If-Match checks, department mapping revocation, closed target rejection, injected rollback, stale412, duplicate409 and concurrent200/412. Real Edge verifies the unclassified historical label, explicit Required link and independently authorized incident detail. Sent/Received regression also ran. The generic top-level scope field is inherited; incidentLinkCheck and incidentLinkBrowser identify actual evidence. Cleanup receipt confirms the exact disposable clone and backup were removed; runtime cleanup=false delegates to that receipt.

Backend solution build PASS0 warnings/errors; solution test exited0 without a discovered test project. Actual InternalRequest/IncidentRead regression80 PASS,1 environment-gated SQL test SKIP. Frontend production build/typecheck PASS; focused incident-link browser regression1 PASS. ModelProbe retains3 existing EF1002 warnings; Vite retains use-client and chunk-size warnings. Dependencies unchanged.

No whole-UC upgrade. UC123/117/118 still need material visibility and unresolved legacy completion classification; UC119 receipt read scope and UC120 material terminal mapping remain pending contracts. Next highest-impact work is closing these lifecycle gaps when the required contracts are available, while continuing independent approved implementation. No shared or production mutation.

Changed files: InternalRequestIncidentLinks.cs, IncidentLinksPanel.tsx, SprintIncidentLinkScenario.cs, sprintIncidentLinkBrowser.mjs, UC_STATUS_MATRIX.md and this receipt/evidence.
