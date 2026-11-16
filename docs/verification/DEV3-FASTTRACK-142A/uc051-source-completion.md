# UC051 source completion

2026-11-16: IMPLEMENTED_PENDING_QA. No COMPLETE_VERIFIED claim.

Approved decision: independent INCIDENT_EVIDENCE_ADD. No automatic grants. The versioned permission script is prepared for the exact disposable recovery identity, defaults to rollback and checks backup evidence before Apply. It has not been executed in this batch.

| Acceptance | Source |
| --- | --- |
| Current session and independent permission | IncidentEvidenceController requires HTTPS/authentication; existing global CSRF middleware protects POST; IncidentEvidenceWrite validates SQL session, active actor/role/grant/branch and independent source read scope |
| Reporter/current assignee or Maintenance Lead/Operations Manager | Dedicated write permission paths and current work relationship; source detail/evidence uses UC051 source authority while UC143 list filtering is unchanged; OWN applies only to related sources, BRANCH does not give unrelated staff blanket access |
| Note and/or files | Canonical IncidentEvidencePanel; trimmed nonempty note or files; 4000-character note bound, up to five complete JPG/PNG/PDF files at 10 MiB each; server verifies metadata/content and rejects invalid payload |
| Not Closed/Cancelled | Current source row locked before authorization/write; MSG24 rejection; read still available under source permission |
| Author/time and atomic audit | SQL UTC timestamp, IncidentEvent EVIDENCE_ADDED, INCIDENT attachments and audit saved in one Serializable transaction; state itself unchanged |
| Private files and cleanup | Generated flat keys in private evidence storage; all bytes written before SQL persistence; known precommit failure cleans new files; ambiguous commit retains files for reconciliation |
| Concurrent/stale submission | If-Match digest of current Incident state and latest event/attachment; source row serialization; successful append changes digest, stale/double submission rejected; UI never auto-retries POST |
| Display and download | Paginated notes/files, author/time; exact source ownership and current read authority checked on each download; no raw paths/URLs; MSG08 and refreshed evidence after confirmed save |

Validation: backend build PASS, zero warnings/errors; frontend TypeScript/Vite build PASS with existing directive/large-chunk warnings. Six focused backend tests PASS (read-contract admission, source visibility, independent write grant/role/scope, revoked/future grants, former assignee, note/file inputs). No new SQL/HTTPS/browser acceptance. Human QA must exercise real note-only/file-only uploads, stale/concurrent saves, rollback/file cleanup, session/grant revocation, Closed/Cancelled sources, role branches and private downloads after installing the catalog on the verified disposable database with explicitly authorized synthetic grants.

Source files: IncidentEvidenceController, IncidentEvidenceWrite, IncidentEvidenceRead, IncidentReadAccess, IncidentReadContract, Program registration, IncidentEvidencePanel, IncidentEvidencePermissionV1.sql and IncidentEvidenceWriteTests. Existing generated entities and database constraints unchanged. UC049 and UC118 still require their separate remaining business workflows. Source count becomes 42/144; historical QA count remains 27/144.
