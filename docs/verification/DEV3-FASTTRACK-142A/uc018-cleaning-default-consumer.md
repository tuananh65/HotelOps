# UC018 cleaning-default increment

UC018 remains PARTIAL_IMPLEMENTATION pending the requested business interpretation of standard cleaning minutes. No new source-complete or QA_VERIFIED claim for this increment.

Current room-type standard minutes are returned by authorized cleaning creation options, displayed as a planning reference, and reread on creation inside the existing Serializable catalog transaction. The creation response and audit snapshot record the server-read value. Caller-supplied estimates are not accepted; explicit/request-linked DueAt stays unchanged. Missing/non-positive legacy defaults are unavailable, never invented as zero. No generated entities, mappings or SQL schema were changed.

Inspection source already resolves the current room-type default checklist and checks the selected published version at assignment. Existing inspection versions are not rewritten.

Actual validation: backend build PASS (0 warnings/errors); frontend tsc/Vite PASS with existing use-client and large-bundle warnings; 15 cleaning-create transport tests PASS, including refreshed defaults, invalid values, and unchanged deadline/client estimate rejection. Solution dotnet test exits 0 without discovering tests and is not counted as test coverage. No SQL/HTTPS or browser acceptance for this increment.

Pending business question: does UC018 require only a planning reference/creation snapshot, or a scheduling/deadline calculation? SRS gives no formula or start anchor. Do not equate advisory display/audit with an approved scheduling policy. Audit is subject to existing retention; it is not a new permanent estimate store.
