# Fixed operational levels

User requests fixed priority/severity data and removal of operational configuration from Admin UI. Single agent, preserve existing IDs and 40 tables. Scope includes catalog write guards and workspace ordering; existing permissions remain unchanged.

- [x] Inspect schema, real reference values, dependencies and SRS. Fixed codes LOW/NORMAL/HIGH/CRITICAL match UC-050/UC-064. Current compact data uses DEMO-HIGH, DEMO-MEDIUM and a generated normal-priority code.
- [x] Backup verified before DB writes; transactional normalization applied, preserving IDs and dependency records.
- [x] Block API writes to fixed operational groups; retain lookup reads. Remove operational configuration from Admin including old deep links.
- [x] DB has 8 fixed levels/40 tables and preserved links. FE/BE builds passed; 28 .NET passed/2 fixture skips, 12 API write denials and desktop/mobile UI plus role regression passed.
- [x] Report files, outcomes and limitations in `plans/reports/test-261122-1954-fixed-operational-levels.md`. No push/deploy/schema changes. Keep API 2964:5768 and FE 5186.
