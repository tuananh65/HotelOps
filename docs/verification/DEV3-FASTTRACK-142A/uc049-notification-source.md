# UC049 reported notification source

2026-11-16. UC049 remains PARTIAL_IMPLEMENTATION; SOURCE_COMPLETE=42/144 and QA_VERIFIED=27/144 historical, unchanged.

The existing CREATED Incident event is now a durable notification intent. Delivery verifies its initial transition, reporter and report timestamp. It rescans retained events so failed delivery and temporarily denied recipients can be retried independently of the original report transaction. Recipient scope is active Maintenance Lead with current branch access and an eligible same-role INCIDENT_VIEW branch grant. No new permission or grants.

Delivery runs in its own serializable transaction with user eligibility locks, source rereads, notification range locks, provenance checks and per-user receipt deduplication. Authority is checked again after persistence before commit. The notification center resolves its source only after current recipient/source authority and session checks, returning the canonical Incident detail URL. It does not trust a stored action URL or widen source access.

Configuration: IncidentNotifications:Enabled (default false), IncidentNotifications:PollSeconds (default 30, valid 1..3600). DI registers the worker and delivery service. Existing NotificationCenter retention bounds apply. No worker was enabled and no SQL mutation was performed in this batch. This supports current physical CREATED/null-to-OPEN events only; it does not settle the pending SRS Pending Triage mapping.

Validation: backend build passed, zero warnings/errors. Four focused tests passed: actual EF SQL translation of retained scan/source lookup plus existing Incident current-role/branch/admission tests. The first translation check exposed unsupported constructor projection composition; changed to member-initialized projection and reran successfully. No SQL execution, concurrent delivery or browser acceptance is claimed. Human QA must exercise retries, concurrent workers, rollback invisibility, duplicate recipients, revoked/expired grants, cross-branch denial, retention and notification source opening on the verified disposable target.

Remaining mandatory UC049 work: production create admission and authoritative Pending Triage lifecycle mapping. Runtime acceptance of initial attachment rollback, delivery and source opening remains deferred. No UC is promoted by this increment.
