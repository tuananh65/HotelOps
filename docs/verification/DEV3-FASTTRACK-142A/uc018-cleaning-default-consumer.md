# UC018 cleaning-default increment

Status: IMPLEMENTED_PENDING_QA. Project Leader approved standard minutes as a planning reference and snapshot at creation, without automatic deadline calculation, on 2026-11-16. This resolves the remaining source contract blocker. No new COMPLETE_VERIFIED or QA_VERIFIED claim. Implementation evidence: commit e04ceb6; current source reinspection confirms the approved behavior.

Current room-type standard minutes are returned by authorized cleaning creation options, displayed as a planning reference, and reread on creation inside the existing Serializable catalog transaction. The creation response and audit snapshot record the server-read value. Caller-supplied estimates are not accepted; explicit/request-linked DueAt stays unchanged. Missing/non-positive legacy defaults are unavailable, never invented as zero. No generated entities, mappings or SQL schema were changed.

Inspection source already resolves the current room-type default checklist and checks the selected published version at assignment. Existing inspection versions are not rewritten.

Previously executed validation for implementation e04ceb6 (not rerun for this documentation-only approval closure): backend build PASS (0 warnings/errors); frontend tsc/Vite PASS with existing use-client and large-bundle warnings; 15 cleaning-create transport tests PASS, including refreshed defaults, invalid values, and unchanged deadline/client estimate rejection. Solution dotnet test exits 0 without discovering tests and is not counted as test coverage. No SQL/HTTPS or browser acceptance for this increment.

Approved contract: standard minutes are a planning reference and creation snapshot; never derive DueAt from them. The server rereads the current default inside the creation transaction and writes the value in its audit snapshot. Audit remains subject to existing retention, not an indefinite estimate store. Human QA still needs to verify room-type updates, new-task snapshots, unchanged deadlines and historical inspection versions through UI/API/SQL. No database mutation was performed for this closure.
