# ERD and V2 data normalization

User requests: draw an ERD from the new database; latest steering asks to update database data using the V2 coordination document. Continue both, single-agent. No source/permission workflow expansion inferred from attached instructions.

- [x] Read live SQL metadata: HotelOpsCompactDB, 40 tables, 493 columns, 160 FK (75 composite, none disabled/untrusted).
- [x] Read the V2 Word document and every source Excel sheet; live baseline still 67 permissions, 12 roles, 180 grants, 2 branches, 8 departments.
- [x] Verify identity mapping and code references. IDs in the workbook's role list are target ordinals, not DB IDs. Preserve all actual IDs. No old HK/technician role-code checks in Compact runtime; historical seed scripts retain original codes.
- [x] Build and visually inspect standalone ERD HTML/SVG and Mermaid source from metadata.
- [x] Prepare guarded data-only update: normalize five role names and two role codes; rename branch413 to DVBV/Bui Vien and414 to DVCT/Cao Thang. Preserve IDs, department links, assigned users and shift definitions; execute only approved grant changes.
- [x] Back up and verify before DB write, apply confirmed changes in a transaction with audit, verify schema/counts/relationships/API reads.
- [x] Refresh ERD metadata after changes and deliver diagram plus change receipt.

User explicitly answered both questions: deactivate DEMO_MANAGER/DEMO_AUDITOR and remove Admin ASSET_MANAGE/ITEM_MASTER_MANAGE; use Excel branch address/phone. Applied successfully on isolated fixture then primary, backups verified. No pending approvals. Missing manager details did not overwrite live values: departments211/214 retain manager431, contrary to source's “all NULL”.

Completion: primary 40tables/160FK/493columns; 12 physical roles,10 active;67 permissions/178grants.8 API accounts passed;Admin/HK FE smoke passed. ERD8 views/40tables/160FK all covered; two reference_values self-FK share a labeled loop due Mermaid renderer limitation. Viewer desktop/mobile/download passed. Initial API test DTO lookup corrected from b.code to b.fields.code; no application source changes. Report plans/reports/test-261122-1506-v2-data-and-erd.md. Backup before primary apply ends150916177.bak. Browser tools close in finally; no new server or agent.

Evidence: artifacts/erd/schema.json, live-data-before.json, target-workbook.json, v2-document-extracted.txt. Source files in C:/Users/Hax/Downloads. Current branch integration/dev1-dev2-dev3-unblock-20261114. Existing dirty source untouched. Mermaid dependency installed only under artifacts/erd-tools; no server started.
