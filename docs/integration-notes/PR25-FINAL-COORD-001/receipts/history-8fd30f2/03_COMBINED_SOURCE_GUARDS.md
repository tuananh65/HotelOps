# Combined guards — verified source only

Tree: 14f4595c0edeb1f6349d636ec1a08a22a287f569.

| Contract | Findings and executable evidence |
|---|---|
| UC006 | Dev2DependencyReader.AuthorizeGlobalUserAsync yêu cầu effective global MASTER_DATA_MANAGE + target CanManageAsync; private actor/session/target context được kiểm lại. ReadUserGlobalAsync liệt kê tất cả Branch không active-only filter, giữ unscoped/inconsistent Maintenance và unresolved policy. Branch coverage reread và source fingerprints conflict khi thay đổi. Caller transaction giữ sorted actor/target User locks; reader không commit. NoSql chạy đủ 8 nhóm inactive-branch regression và các checks bổ sung. |
| UC016 Asset | AssetService.Location.MoveAsync bind trusted actor/cookie; local transaction → eligible User → permission/session → Branch UPDLOCK/HOLDLOCK → locked Asset; recheck branch, active Room/Floor/Area ancestry và final authority trước commit. Guard ràng buộc transaction/branch lease, không tự commit. |
| UC016 Incident | CreateIncidentAsync giữ User-first → Branch → Asset/number-allocation; kiểm cả explicit location lẫn locked Asset location, audit/domain writes trong cùng transaction và final permission/session check trước commit. Status/triage và Asset condition/update/retirement/handover giữ paired User-first locks. NoSql chạy writer-order provider stubs, không chứng minh SQL locks thực. |
| UC012/022/026 | Exact branch/room/department readers, source-bound fingerprint gồm assignment/parent/rowversion/quantity facts; mismatched scopes/unknown policies không bị bỏ qua. Caller SERIALIZABLE/resource lease + final fingerprint reread vẫn bắt buộc. DependencySnapshot và GlobalUserDependencySnapshot mặc định CanDeactivate=false. |
| FE12 | Fe12MaintenanceDependencyAdapter giữ MAINTENANCE_VIEW, persisted source facts, active account/employment/membership eligibility, source fingerprint và matching caller transaction lease. Missing accepted cycle binding vẫn ném MAINTENANCE_CYCLE_CONTRACT_REQUIRED; WorkTimeBindingAccepted/NotificationStagingAccepted=false. |
| Program.cs / DI | So với integration chỉ thêm 3 scoped participants: Dev2DependencyReader, Dev2PlacementAuthority, Fe12MaintenanceDependencyAdapter. Các registration còn lại nguyên bản integration. Actual registration-prefix probe ValidateOnBuild/ValidateScopes và resolve CurrentUser/permission/session/readers/Asset/Incident thành công; kiểm Asset/Incident nhận authority khác null. |

UC006 immutable UserAccountDeactivationReleaseAcceptance giữ 3 pending gates; không có Deactivate controller/DI activation. UC010 PendingReleaseGates không đổi. UC012 structure deactivation trả WriterCoordinationRequired; UC022/026 candidate immutable gates và NonController được kiểm bằng suites. UC016 writer guards hiện hữu được bảo toàn; không cấp business acceptance mới. WorkdaysController vẫn NonController, MVC có đúng 2 GET reads. Notification worker/producer không được đăng ký/activate. PR51 ETag cutover không được đưa vào functional delta 24 paths.

Không sửa generated Entities, HotelOpsDbContext, shared policies, config, SQL/DDL/migrations hoặc DEV3 writers. Không thực thi production startup schema validator, listener, SQL, HTTPS hoặc business mutations.

DI probe dùng actual Program registration prefix trong evidence-only verification project, Windows Development, placeholder connection chưa mở và console logging. Probe không thực thi startup SQL. Ba lỗi harness/environment ban đầu đã giữ receipt và sửa ở harness: CS9137 OpenAPI generator namespace; default Production guard; Windows Event Log access denied. Không sửa source để bỏ các guards.

DI model initialization ghi 3 EF warning 20602 cho RoomTypeBranchV1 indexes (IX_room_types_default_checklist_v1, UX_room_types_branch_code, UX_room_types_legacy_code). Mappings này không thay đổi bởi merge/PR; báo cáo warning, không tự refactor mapping.
