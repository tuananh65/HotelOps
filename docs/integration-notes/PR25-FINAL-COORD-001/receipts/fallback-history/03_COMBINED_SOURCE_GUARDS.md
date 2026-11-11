# Combined source guards
COMBINED_SOURCE_CREATED=false
DI_COMPOSITION_VERDICT=NOT_VERIFIED_POST_SYNC
ASSET_INCIDENT_WRITER_VERDICT=NOT_VERIFIED_POST_SYNC
Không sửa C#, Program.cs, controller, generated entity, DbContext, schema, policy hoặc runtime flag.
Các kết luận DEV3 cũ tại 2140522 / d0a5a073 là lịch sử, không dùng làm review mới.
Chưa chạy API, SQL, HTTPS, concurrency, rollback hoặc migration.

Sau merge phải xác minh: UC006 effective global MASTER_DATA_MANAGE + CanManageAsync, inactive branches, unresolved facts, fingerprints, transaction reread; UC016 trusted session, User → Branch U/HOLDLOCK → resource locks, active ancestry, caller-owned commit; UC012/022/026 fail-closed coverage và CanDeactivate=false; FE12 cycle/time-kind safeguards.
Giữ UC006/010/012/016/022/026 acceptance gates; Workday writes và Notification producer/worker OFF; PR51 độc lập MERGE_HOLD.
Không phát hiện hoặc khẳng định defect nguồn mới khi chưa có combined source.
