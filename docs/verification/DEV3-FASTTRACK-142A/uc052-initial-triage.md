# UC052 initial Triage / UC054 dependency — 2026-11-16

Source increment only. UC052 and UC054 remain PARTIAL_IMPLEMENTATION; no new source-complete or COMPLETE_VERIFIED UC is claimed.

## Implemented path

Canonical Incident detail now offers a dedicated Triage form when current server admission and capabilities allow it. The form uses current configured priority options/default deadlines, an initial action plan, optional UTC deadline overrides with a minimum ten-character reason, confirmation, CSRF, native Incident If-Match and the selected SLA target row version. It sends once, checks the returned identity/deadlines/new ETag, and provides the existing UC053 assignment destination after confirmed success. The generic unversioned Triage form is excluded.

A004A-020 has its own production contract: authenticated unrestricted HTTPS/SQL session; active Maintenance Lead; current scoped INCIDENT_TRIAGE and independent INCIDENT_VIEW; exact controller/action/root/input and trusted actor binding; owning Serializable transaction and final authority revalidation. No permission catalog or grants are added. Read disclosure of Triage options independently rechecks that authority. Existing Incident read contracts still cannot authorize writes, and unrelated Incident mutations remain unadmitted.

Initial Triage requires physical OPEN with exactly matching CREATED evidence, no prior TRIAGED event, no linked maintenance work, no resolved/closed timestamp, existing category and current severity. It does not repair legacy execution/history. The service rejects stale native Incident ETag, missing configuration and changed SLA-target version. Defaults use DiscoveredAt plus configured minutes; changed deadlines require a reason and cannot precede discovery. Event, initial action plan, current priority, default/chosen deadlines, configured minute values/target version and audit are persisted in one transaction. Existing SLA values cannot be overwritten. The resulting TRIAGED event/audit supplies the proof required by MaintenanceAssignmentProof; no physical PENDING_ASSIGNMENT enum is introduced.

IncidentTriage.sql prepares additive native Incident row_version and a priority-reference-FK SLA-target table. Configurations default inactive; no values are seeded, including the historical defaults listed in SRS. It requires IncidentSla.sql first, exact disposable identity/nonzero GUID, schema validation, migration lock and explicit ApplyChanges; preview rolls back. Generated entities and shared database are unchanged. Configuration changes do not recalculate already-recorded deadlines.

## Direct checks

- Backend solution build PASS, zero warnings/errors. Solution test exits zero without discovery.
- Focused deadline/proof/read/admission tests: 24 PASS. An old assertion assuming every Incident mutation was unadmitted was updated to distinguish the new independent Triage contract while retaining denial for other mutations.
- Frontend TypeScript/Vite production build PASS; existing React Router use-client and bundle-size warnings remain.
- Two mocked browser checks PASS: defaults/success and reasoned override/stale target, exact CSRF/If-Match/target version and no retry. Initial failures exposed an ambiguous select label and a browser-fixture datetime value format; both were corrected. An initial backend build failed because a generated policy-string edit left trailing text; corrected before the passing builds.
- No SQL migration execution, configuration seed, live HTTPS/API or transactional SQL rollback/race test in this increment. Browser tests mock HTTP; unit tests do not prove SQL mutation acceptance.

## Remaining business/runtime work

UC052 is not complete: Cancel Duplicate/Invalid, associated block release/readiness and reporter notification remain separate; approved deadline-change/Reopen cycle-history handling is not exposed by initial Triage. UC053 assignment/reassignment admission and eligibility must be completed before the handoff is usable end to end. UC054 Decline receipt/transaction/Lead notification and reassignment are still required.

Runtime requires verified disposable installers, reviewed active SLA target rows/current grants and Human QA of authorization revocation, missing classification, repeated Triage, stale Incident/target versions, rollback and concurrency. The current deadline form explicitly uses UTC; SYS-PAR-10 presentation remains to reconcile before full acceptance. No trial PASS, full UC completion or production deployment is claimed.

Subsequent direct validation in the UC054 batch: SLA/Triage/Decline installers each executed twice on the identity-verified disposable database inside an outer transaction, followed by rollback/cleanup PASS. No target seeds or persistent installation; business runtime remains NOT_RUN. See uc054-decline-source-completion.md.
