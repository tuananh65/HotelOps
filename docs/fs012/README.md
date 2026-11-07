# FS-012 — SQL write boundary, reconciliation và Tester support

**PARTIAL về acceptance/integration.** Phần implementation/proof độc lập được hoàn tất trên feature branch; UC029 vẫn **PROVISIONAL_NOT_ACCEPTED**, A01/A02 **MISSING_EXECUTABLE_SOURCE**, kết quả Tester **TESTER_EXECUTION_PENDING**. Không mở candidate cho shared/production, không PR/merge cả branch.

Baseline: `0ad98b951cf4737842afe3e382e897f7fb4bacc8`. Branch: `feature/dev1-room-ops`. Runtime/trial source commit `d1260425206a953086107eb8de97f845405fc782`; code checkpoint của retest là `0a4139fa3520577e82c388fab0893b508da9b820` (thêm kiểm hash portable cho installer). Các commit tài liệu/package sau đó không đổi executable source. Final remote/clean-tree verification được báo sau commit/push trong completion response.

FS012 thêm module SQL tạo Assignment + audit + operation + publication + intent trong caller transaction, cùng user NOLOGIN hạn quyền để kiểm chứng direct-writer restrictions trên **một trial mới**. Không đổi generated entity/mapping/migration, shared configuration/User Secrets, role/status/catalog hoặc module DEV khác. Runtime adapter và lifecycle marker chỉ thêm opt-in FS012. Assignment direct writes dưới user thử nghiệm bị từ chối; privileged bypass vẫn persist và được ghi rõ. API chưa chạy bằng deployed restricted login.

| Nội dung | Tài liệu / evidence |
| --- | --- |
| Boundary thực tế, writer inventory, grants và giới hạn | [SQL_WRITE_BOUNDARY.md](SQL_WRITE_BOUNDARY.md), SQL_BOUNDARY_INVENTORY.json |
| Authorized/direct/raw/external/concurrent/fault proofs | [EXTERNAL_WRITER_TESTS.md](EXTERNAL_WRITER_TESTS.md), BOUNDARY_ATTEMPT01..07.json, COMMIT_FAULT_RESULTS.json, PHYSICAL_GUARD_RESULTS.json |
| Bounded A01/A02 reconciliation | [A01_A02_RECONCILIATION.md](A01_A02_RECONCILIATION.md), REMOTE_PREFLIGHT.txt |
| ZIP frozen, checkpoint/mapping/hash/secret validation | [TESTER_HANDOVER_FREEZE.md](TESTER_HANDOVER_FREEZE.md), HANDOVER_FREEZE.json |
| Tester preflight/startup/HTTPS/fixtures/recovery/defects/retest | [TESTER_EXECUTION_SUPPORT.md](TESTER_EXECUTION_SUPPORT.md), TESTER_READONLY_*.json, TESTER_FROZEN_SETUP_PLAN.txt |
| Build/unit/Node/browser/live và các lượt FAIL giữ riêng | [REGRESSION_RESULTS.md](REGRESSION_RESULTS.md), VALIDATION.json, evidence/fs012.trx, full01/full02 receipts |
| 11 UC, không tự cộng credit | [FE04_PROGRESS.md](FE04_PROGRESS.md) |
| Merge gates / clean UC028 slice | [MERGE_READINESS.md](MERGE_READINESS.md) |
| Việc còn lại FS013 | [NEXT_ACTIONS.md](NEXT_ACTIONS.md) |
| Shared impact / điểm cần integration review | [DEV1_FS012.md](../integration-notes/DEV1_FS012.md) |

FS011 handover vẫn authoritative cho đợt Tester hiện hành: checkpoint `4f62d1388a6669fbf61ead520ee8d33bc8ae8809`, ZIP SHA256 `8e661796fe83b7b15c16d84f79547ce72e4cdd6272f7996ce2fc659003141974`,30 files,75 unit cases/34 methods,38 functional NOT_RUN. Version mới [FE04_RETEST_FS012.zip](../test-handover/FE-04/FE04_RETEST_FS012.zip) là developer retest addendum riêng: source manifest, executed unit mapping, affected-case groups, unchanged functional reference và evidence đã phân loại. Không phải receipt Tester hoặc accepted Notification deployment package.

Trial FS012 giữ mọi dữ liệu thử trước; không DROP/reset/reseed. Sau gián đoạn chỉ append shift/phòng synthetic mới, không chỉnh shift/task lịch sử, không ghi vào Tester DB. Mọi services được stop theo owner/PID/start identity khi kết thúc. Phần chưa xác minh: deployment login hạn quyền, inbox/photo dưới user prototype, network outage/recovery transport thật, abrupt worker-kill, accepted shared A01/A02, full FE04 acceptance và Tester execution.

Danh sách file đầy đủ: [CHANGESET.md](CHANGESET.md). Raw error contexts của live01 được lưu nguyên byte trong evidence/full01-original-error-context.zip với hash/roundtrip receipt ORIGINAL_ERROR_CONTEXT_ARCHIVE.json; các bản Markdown chỉ trim presentation trailing whitespace để qua diff check, không đổi lỗi/status. Secret scan cuối xét toàn bộ thay đổi từ baseline, gồm source đã commit và evidence/logs, ở SECRET_SCAN_ALL_CHANGES.json; ZIP/workbook có receipt quét riêng.

Manifest retest có scope tracked non-docs; dependency docs/fs012/SOURCE_MANIFEST_BASE.json lấy từ pinned code checkout. ZIP là addendum kèm JSON/TRX receipts, không phải mirror mọi evidence file của repository. Các đường evidence/logs còn lại trong báo cáo nằm tại docs/fs012 của delivery commit; raw failure contexts và full validation logs được giữ ở repository.
