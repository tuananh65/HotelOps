# HotelOps SRS — Reading Guide for Developers and Codex

**Status:** DRAFT. The source SRS is not approved for the changed roles and permissions.

This folder contains a conversion of the original Word SRS for search and retrieval. The conversion **does not enact, resolve, or approve** changes to requirements. The original Word document remains the reference for exact page layout and diagrams.

## Files

- [`HotelOps_SRS_DRAFT.md`](HotelOps_SRS_DRAFT.md): entire converted SRS, including tables and links to all extracted diagrams.
- [`srs_sections/`](srs_sections/): smaller files divided by the five major SRS parts so coding agents can focus on relevant content.
- [`assets/media/`](assets/media/): exported source diagrams and images referenced by the Markdown files. Keep this directory alongside the Markdown files.
- [`SRS_CHANGE_NOTES.md`](SRS_CHANGE_NOTES.md): **separately maintained** revision notes that should already exist in the repository; this conversion package does not overwrite them.
- `HotelOps_SRS_DRAFT.docx`: **separately maintained** original Word copy that should already exist in this repository; this package does not overwrite it.

## Part-by-part reading

| Part | Markdown |
|---|---|
| Front matter, TOC, changelog | [`00_front_matter.md`](srs_sections/00_front_matter.md) |
| I. Overall Requirements (flows, actors, Permission Matrix) | [`01_overall_requirements.md`](srs_sections/01_overall_requirements.md) |
| II. Use Case Specifications | [`02_use_case_specifications.md`](srs_sections/02_use_case_specifications.md) |
| III. Functional Requirements | [`03_functional_requirements.md`](srs_sections/03_functional_requirements.md) |
| IV. Non-Functional Requirements | [`04_non_functional_requirements.md`](srs_sections/04_non_functional_requirements.md) |
| V. Requirement Appendix (business rules, state catalog/transitions) | [`05_requirement_appendix.md`](srs_sections/05_requirement_appendix.md) |

## Mandatory revision warnings

1. **Reception** will be a new role separated from Operations Manager for designated room lifecycle/status operations. Precise use-case grants and transitions are pending.
2. **Department Head** manages departments within **one hotel branch/facility** rather than being the head of exactly one department. Exact permission scope is pending.
3. The original **Permission Matrix (section I.4.4)** remains **unapproved**. It does not contain Reception.
4. Existing business rules such as **GBR-041** (a department may have one Department Head user) and **GBR-043/GBR-044** (room status calculated from events) require reconciliation with those clarifications. Do not silently overwrite or reinterpret them.

## Suggested Codex task instruction

> Read `AGENTS.md`, `docs/requirements/SRS_CHANGE_NOTES.md`, and only the relevant SRS section(s) in `docs/requirements/srs_sections/`. Identify the exact Use Cases, database entities, unresolved rules, and scope limitations before proposing code. Stop and request clarification where Reception/Department Head permissions or room-state transitions are ambiguous. Do not use the old Permission Matrix as a final source of truth.

## Conversion limitations

- 59 figures/images extracted from the Word file; they remain **images**, not text descriptions or editable diagram source.
- Complex merged-cell tables remain **HTML tables embedded in Markdown**, which preserves structure more reliably than flattening them into pipe tables.
- Page breaks and pagination may differ from Word. In the original Word document, the pagination fields are not perfectly consistent; use headings/Use Case IDs for navigation.
- Inspect the original Word document for diagrams, merged cells, or formatting that require exact interpretation.
