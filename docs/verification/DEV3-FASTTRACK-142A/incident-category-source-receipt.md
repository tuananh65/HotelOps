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
- UC143 list also supports keyword over incident number/title/description, room/area/asset
  and inclusive detected-time bounds normalized to UTC. Invalid IDs, reversed bounds
  and keywords over 200 characters are rejected. Filters apply before count/pagination.
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
- Business tests / E2E / HTTP execution: NOT RUN, per the instruction to skip tests.
- Database installation safety checks executed separately: rollback and independent
  absence check passed; applied to `HotelOps_DEV3_142A_Recovery_2b221f40` only on
  `meita\MSSQLSERVER01`, verified GUID `5270D07C-7C27-477B-A60E-86485ADD7FAD`.
  `category_id` is nullable bigint; FK enabled/trusted with no cascade action.
  All 12 existing incidents remain uncategorized. No category rows were seeded.
- Source SQL SHA256: E24E3497A0DB6F27AE1E0049874583F21D7E3054BB10DE7C1795CE1D4C10468B.
  Existing recovery backup SHA256 was independently rechecked:
  51D135B6D7AAC0EE22A1FBBF4F2CFE9BBF29447A668F04BD1060C620A082D73F.
- Initial sandbox SQL connection failed with Windows SSPI/SSL credential errors;
  authorized execution outside sandbox succeeded without changing authentication.
  Shared/production databases were not mutated.

Remaining acceptance dependencies:
- No unapproved categories were seeded; approved active category catalog entries are
  still needed for actual selection. Lookup errors do not fall back to free-text IDs.
- Incident mutation admission remains governed by the existing Dev2 authority guard;
  this increment does not activate production or broaden its allowed environments.
- Migration is installed on the verified disposable clone only. No application runtime
  or worker was enabled; HTTP query/command acceptance remains unverified.
- Priority SLA durations/configuration, processing-cycle deadline history and triage
  lifecycle are still required for UC052/RPT-03 SLA completion.
- UC143 still needs approved current assignee/department sources, deadline sorting,
  SLA/overdue evidence and full evidence/progress detail; search does not close this UC.
- Human QA must cover inactive/wrong-group categories, rollback, cross-branch reports,
  historical uncategorized records, query translation and CSV output.

No full-UC or runtime-success claim is made by this source/build receipt.
