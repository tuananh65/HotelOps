# UC049 creation evidence increment

2026-11-16. PARTIAL_IMPLEMENTATION. No new completed UC; source 42/144, historical QA 27/144.

Optional evidence now flows from the canonical Incident creation form through the existing CSRF/session transport to the creation DTO and service. Up to five JPG/PNG/PDF files, 10 MiB each, are validated by the same content/MIME/name rules as UC051. Complete bytes are stored under private opaque keys; existing INCIDENT attachments and their IDs are saved with the creation audit and any Internal Request link in the existing transaction. Report time uses SQL UTC. No generated entities or schema changed.

The action authority grant owns file lifetime until the true outer transaction commit: an inner transaction lease cannot mark files committed. Failure before commit cleans invocation-owned files; an uncertain commit acknowledgment retains files for reconciliation. Existing authorization revalidation remains before saves, disclosure and actual commit. UC051 append uses the same staging helper without changing its independent permission requirement. Initial attachments belong to the creation operation, not a separate append action.

Direct validation: backend build passed with zero warnings/errors; 22 focused backend checks passed (input, evidence permission/validation, staging/private metadata and rollback file cleanup, commit lifetime). Frontend TypeScript/Vite build passed with existing React Router directive and bundle-size warnings; three linked-creation transport checks passed, including evidence forwarding. Node loader deprecation remains. These are offline checks, not SQL transaction or browser acceptance.

No SQL mutation, catalog installation or grant changes. Production creation admission, authoritative Pending Triage lifecycle mapping and durable Maintenance Lead notification remain mandatory gaps. The existing action gate remains closed where no accepted creation contract exists. UC049 and UC118 are not promoted by this increment. Human QA must verify creation success, rollback, revoked authority during upload, outer-transaction failure and download authorization on the disposable target once creation is admitted.
