# Final integration: RESTORED & REVERIFIED

Three feature-owned files are now present and the exact one-line Program registration was explicitly approved/applied. Historical/current-before-task comparison and initial proposed actions below are retained; all read integration actions have now completed. Real production HTTPS/SQL131 checks PASS; no duplicate points. See final integration report and receipts.

# Task84 — UC142 historical/current integration diff

DEV1 FE-02. SRS §2.16 UC142 and Change Notes reviewed; historical implementation at
`cf8fb020362f8f64192e7f2f14a10547cebe5b91`. Historical completion/earned1 is retained.
Task83 reports/ledger/accepted JSON and actual Task70 controller/service/schema sources reviewed.
Current HEAD `4a187320643d0c82f232c6b9dd5a667b8401c280` is not the accepted integration snapshot.

| Component | Snapshot | Working tree before Task84 | Integration action / ownership |
|---|---|---|---|
| Routes | RoomTypeBatchController.List/Detail GET api/room-types and api/room-types/{id}; same controller also has unrelated writers | Absent | Extract only2 GETs into RoomTypeDirectoryController proposal; do not bulk restore controller or writer routes. DEV1. |
| Service | RoomTypeCreationService partial + RoomTypeBatchService.ListRoomTypesAsync/ReadRoomTypeAsync/BatchAsync/tag helpers | Absent | Added dedicated read-only RoomTypeDirectoryService. Reuses existing DTO/auth/scope/retained locks/tag contract; excludes create/update/deactivate/checklist/room/department writers. DEV1. |
| DTO | RoomTypeDetailsDto/ListItem/Page in RoomTypeBatchDtos.cs | Already restored Task75, present untracked | Reused unchanged; no overwrite. BranchId/null legacy, code/name/status/count, CanUpdate, ETag/pagination preserved. |
| EF | db.BranchRoomTypes and optional RoomTypes partial mapping | Global legacy RoomType mapping only | EF10 unmapped SQL projection in feature service; no DbContext/entity edits. SQL filtering/count/order/page remain server-side. Only bounded page materialized. |
| Schema gate | Data/RoomTypeSchema.ReadAsync | Absent | Exact read-only SQL guard extracted into service-local RoomTypeDirectorySchema; requires V1 contract before column projection. Returns503 on legacy-only/incompatible schema. No DDL or inferred ownership. |
| Authorization | MASTER_DATA_MANAGE SQL global/branch, no role bypass; branch-wide read not OWN promotion | PermissionService foundation exists | Use same permission/scoping. Legacy ownership NULL is global-only. No catalog/grant edits. |
| Transaction | SERIALIZABLE; UserEligibilityLock before session/grants, parent branch/catalog lock; final permission/session checks | Locks/session/core exist | Reuse on same scoped context; catalog resource HotelOps:RoomTypes:v1 unchanged; no mutation/audit writes. Explicit actor account/employment eligibility checked after lock. |
| Detail revision | SHA256 JSON-Web of DTO + latest room_types audit ID + active room count | Audit/rooms model exists | Same tag formula/order, permission before data, branch match mandatory. |
| DI | Program registers shared multi-writer RoomTypeCreationService | No equivalent registration | Propose ONLY AddScoped<RoomTypeDirectoryService>(); Program.di.patch, NOT APPLIED without explicit approval. Shared. |
| Tests | Task70 accepted345 mixed checks;49 independent UC142 (40HTTP/persistence+9SQL final-boundary) | Historical receipts in Git, not current runtime evidence | Fresh build/DI/static regression evidence separate. Real HTTPS/SQL still gated by production DI and appropriate trial permission. |

No DEV2 dependency for this read-only flow. No notification added. No new role/permission,
business state, shared mapping or schema. Existing UC018/019 writer semantics are not reimplemented.
Historical CanUpdate remains management capability information; no new write action route is exposed.

## Compatibility choices

The private projection is read-only EF usage, not a replacement entity or model registration.
This removes the need to restore broad shared partial mappings solely for one read feature.
It does not bypass schema validation: original V1 metadata/FK/CHECK/filtered-index/deployment-trigger
guard is retained verbatim as SQL. Legacy-owned NULL rows inside V1 stay global-only; a wholly
legacy schema is not relabelled V1. Existing optional deployment/schema package is not run.

Route/DTO/error conventions preserved. Proposed controller keeps CookieTicket.TryRead internal
to the API assembly and existing UserAccountActivationErrors filter. No shared auth API exposed.
Startup schema validation is not changed. Existing Program/P0/fallback/B1.1/Task82 patches preserved.
