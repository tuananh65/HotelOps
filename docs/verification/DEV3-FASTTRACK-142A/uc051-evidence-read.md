# UC051 Incident evidence read integration

Historical read-only increment. The subsequent approved permission and write implementation are recorded in uc051-source-completion.md; its current status supersedes the partial status below.

2026-11-16: PARTIAL_IMPLEMENTATION. No new source-complete or QA-verified UC.

Implemented existing INCIDENT attachment listing and private downloads under the source-owned Incident read contract. The list is paginated and exposes file name, note, author and timestamp, never storage keys or external URLs. Download resolves only an undeleted attachment of this exact Incident, uses the existing private file reader with path/size/MIME checks, forces a download and sets no-store/nosniff/sandbox. Both endpoints revalidate current session and source visibility before disclosure within the admission guard's Serializable read transaction. Closed/Cancelled records can still be read when the user can read their source.

Canonical Incident detail now renders the evidence list and authenticated download links. Current read access remains Maintenance Lead/Staff under the existing Incident scope; this increment does not silently widen read authorization to the other UC051 actors. Write permission mapping has been asked separately (dedicated INCIDENT_EVIDENCE_ADD versus existing INCIDENT_MANAGE); upload/note mutation remains unimplemented until that contract is resolved. No grants, schema changes or database writes.

Changed source: IncidentsController, Dev2ActionAdmissionRegistry, IncidentReadContract, new IncidentEvidenceRead, new frontend IncidentEvidencePanel, Workspace and IncidentReadAccessTests. The old test assumed the entire system had only two accepted actions; it now verifies the exact five Incident read contracts and denies every Incident mutation without constraining unrelated modules.

Validation: backend build PASS, zero warnings/errors; TypeScript/Vite build PASS with existing React Router directive and chunk warnings; three focused IncidentReadAccessTests PASS. No new SQL/HTTPS/download/browser acceptance. Legacy files outside the existing flat-key private storage format return unavailable instead of using FileUrl; no storage migration is inferred. UC051/049/118 remain partial; 41/144 source-complete and 27/144 historical QA totals unchanged.
