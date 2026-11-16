# Incident Category source increment — 2026-11-16

Status: IMPLEMENTED_PENDING_QA. Newly COMPLETE_VERIFIED UCs: 0.

Approved contract: official INCIDENT_CATEGORY references; additive Incident.CategoryId;
no invented category codes, SLA durations or maintenance-planned-time substitution.

Implemented:
- Optional positive CategoryId through the existing incident creation form/DTO.
- Creation and list filters use an active-reference dropdown. The branch-qualified
  lookup uses the existing INCIDENT_VIEW/current session/role/branch read contract
  with a serializable read and final disclosure check; no new permission is granted.
  A caller with creation permission alone cannot read this lookup without INCIDENT_VIEW.
- List category filtering happens before pagination/counting and intersects the
  authorized query. Category display is enriched in one bounded query per page.
- Active reference/group validation with locks inside the existing admitted command
  transaction; category binding and CREATE audit commit with the incident.
- Detail enrichment after the scoped incident query succeeds; missing storage or
  historical category returns a specific unavailable reason.
- RPT-03 category filtering intersects the existing authorized branch/location/period
  population. Category rows also follow this population, including CSV snapshot output.
- Department filtering still fails closed without its business source contract.
- Additive recovery-only SQL candidate: one nullable bigint column and restrictive FK;
  exact server/database/GUID guard, transactional DDL, default rollback, no seed.
  Generated EF entities, shared mappings and existing authentication are unchanged.

Direct execution evidence:
- `dotnet build HotelOps.slnx --no-restore`: PASS, 0 warnings, 0 errors.
- `npm.cmd run typecheck` in frontend: PASS.
- `git diff --check`: PASS.
- Tests / E2E / HTTP / SQL execution: NOT RUN, per the instruction to skip tests.
- Database changes applied: NONE. SQL candidate has not been runtime verified.

Remaining acceptance dependencies:
- No unapproved categories were seeded; approved active category catalog entries are
  still needed for actual selection. Lookup errors do not fall back to free-text IDs.
- Incident mutation admission remains governed by the existing Dev2 authority guard;
  this increment does not activate production or broaden its allowed environments.
- Review and exercise the migration on the identity-verified disposable clone before
  enabling this storage. Do not apply the candidate to shared/production databases.
- Priority SLA durations/configuration, processing-cycle deadline history and triage
  lifecycle are still required for UC052/RPT-03 SLA completion.
- Human QA must cover inactive/wrong-group categories, rollback, cross-branch reports,
  historical uncategorized records, query translation and CSV output.

No full-UC or runtime-success claim is made by this source/build receipt.
