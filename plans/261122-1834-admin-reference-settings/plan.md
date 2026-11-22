# Simplify Admin reference settings

Single agent, sequential work. Keep internal codes: ReferenceValue.Code is required and (Kind, Code) unique; role/permission codes also support authorization. No schema or authorization-policy changes. Remove code column and manual code entry from reference settings; generate technical codes on creation and preserve existing codes on update. Hide UOM and ITEM_CATEGORY from Admin settings rows and group choices; preserve actual inventory references and other actors' lookups. ASSET_TYPE is a separate category and stays visible.

- [x] Inspect existing entities, mappings, catalog API and UI.
- [x] Implement bounded UI change.
- [x] Build; verify real Admin rows, form options and outgoing create/update payloads without changing business data.

Build passed; browser verification passed: three visible rows, two inventory rows hidden, four allowed group choices, generated create code, preserved update code/kind, unchanged database references. No JavaScript errors. Write requests intercepted before reaching backend. See plans/reports/test-261122-1834-admin-reference-settings.md.

Visual direction: preserve existing green/white compact tables, LED status and settings dropdown. No new UI components or decorative elements.
