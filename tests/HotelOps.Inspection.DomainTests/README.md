# INSP-002: UC045 domain validation only

Run `dotnet run --project tests/HotelOps.Inspection.DomainTests` from the repository root.
This is a dependency-free console unit-test harness, following the repository's executable
verification project convention. It exits nonzero on failure. It never starts the API,
creates a DbContext, reads credentials, connects to SQL or publishes notifications.
It is not registered as a test adapter in HotelOps.slnx; `dotnet test HotelOps.slnx`
does not discover these checks.

The validator reuses generated Inspection/CleaningTask/checklist/result/defect entities
as read-only backend inputs, the existing PASS/FAIL/PENDING_CONCLUSION vocabulary,
Inspection reason constraints (trimmed 1–500 characters, no controls) and strong
SQL rowversion ETag format. Entities and mappings are unchanged. A valid result is
not authorization, a trusted source proof, a transition command or a durable receipt.
A fabricated complete object graph cannot be distinguished from a genuine database
snapshot by an in-memory validator. Never bind RejectInspectionSource from HTTP.

References: SRS UC045, GBR005/012/060/063 and INSP-002 scope. Mandatory results and
every FAIL's described defect are revalidated against the supplied assigned version;
optional unanswered criteria do not prevent rejection. Published-version eligibility
is not rechecked: existing assignments retain their version after retirement.

BLOCKED_SPECIFIC_RULE (not implemented):

- D1: actor/role/permission/branch authorization and inspector ownership enforcement.
- D2/D4: authoritative loader/provenance, durable conclusion reference, evidence revision
  coverage/digest/invalidation. Only supplied identity consistency and the existing
  inspection rowversion comparison are validated here.
- D3: current-cycle authority, CleaningTask phase, RoomBlock/readiness and atomic lifecycle.
- D5: idempotency, durable replay and ambiguous-commit recovery.
- D6: reinspection round and terminal Rework mapping (outside UC045 domain scope).
- D7: production writer activation/notification transaction participation.

No FAILED writes, production routes, DI registrations, schema/mapping changes, Work-Time,
UC046/047 implementation or frontend changes are included. Full UC045 remains unaccepted.
