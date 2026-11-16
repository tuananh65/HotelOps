# UC099 return document timeline

2026-11-16. Implementation increment; not COMPLETE_VERIFIED.

The existing material request detail is admitted by MaterialReadContract under independent MR_VIEW, current session, branch/ownership scope, Serializable disclosure and MaterialReadProvenance. The matrix's prior statement that production authority is empty was stale. Raw audit History remains unadmitted; this change does not expose it.

Return lines now carry their official return line ID, item/unit names, actual and damaged quantities, document CreatedAt and PostedAt/PostedBy. The relationship follows stock_return_line_details.original_issue_line_id into the request's fulfillment documents, never an inferred item match. UTC conversion matches other workflow events. No schema, entity or database changes.

The business timeline groups lines by return document, shows creation separately from posting, and places each at its persisted timestamp. Missing posting time remains unavailable; it is not replaced by creation/update time. Multiple return lines remain distinct. Draft/cancelled/reversed documents do not imply available stock or automatically reduce historical issued quantities. Existing permission and provenance checks remain unchanged.

Changed files: MaterialRequestContracts.cs, MaterialRequestService.Queries.cs, MaterialHistoryView.tsx, UC_STATUS_MATRIX.md, this receipt.

Validation: backend build --no-restore PASS, zero warnings/errors. Frontend production build including TypeScript PASS; existing React Router directive warnings and 944.33 kB bundle warning. Solution test command ran zero tests. SQL/API/browser verification of this increment NOT_RUN. Remaining UC099 acceptance: authenticated scoped list/detail, review/issue/return lifecycle and linked work navigation. Source/build evidence does not establish full acceptance.

Existing dev2-ui offline regression: 4 passed, 1 skipped (mounted positive authorization); Node module.register deprecation warning. These checks cover form/auth rendering, not return timeline semantics or runtime acceptance.
