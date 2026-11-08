# [HotelOps DEV1 FE04] FE04-RELEASE-001 — PR_READY candidate

Ngày 2026-11-08. Branch: release/fe04-housekeeping-20261108, tạo riêng từ origin/develop.
Develop đã fetch và kiểm tra lại: 6aee2a0e692a7814f41c81224709cddfbe1e2ec4.
FE-04 source: 8d49a9087ce75fa39c89f35be9a90cd27faa3216.
Integration tham chiếu: 29eee0fe1465266b26d5cb837989e1f5f62193dc; không merge branch này.
Release HEAD chính xác và PR URL được xác minh qua GitHub sau commit/push; xem PR head.

Chỉ extract source FE-04 và dependency trực tiếp. Không merge 43 commits của branch gốc.
Exact allowlist: [allowlist.json](allowlist.json), 160 files, không file bị xóa.
Identity: [source-files.json](source-files.json), 144 files giữ nguyên Git blob của source, 11 files compatibility/new, 5 files báo cáo/manifest. AuthorizationTests.cs chỉ bỏ blank line thừa cuối file.
Không thay đổi Auth/RBAC, Work-Time HTTP, FE-05 implementation, DEV2 modules, appsettings, generated HotelOpsDbContext.cs, solution hay startup schema validator.

## Phạm vi UC028–UC038

| UC | Source được bàn giao | Trạng thái / giới hạn giữ nguyên |
| --- | --- | --- |
| UC028 | Create task, room cleaning-item snapshot, options, audit/readiness/current-cycle boundaries; UI/API/DTO | Implemented source; schema/source gates giữ nguyên. Không tự cộng acceptance. |
| UC029 | Planning + assignment candidate, immutable command/replay, mandatory notification staging; 06R1 source/transaction adapter | PROVISIONAL_NOT_ACCEPTED, default OFF. 06R1 adapter nằm sau compile guard, không DI/call-site activation. |
| UC030 | Reassign, assignee/current-cycle/interval/ETag/replay/audit/notification transaction | PROVISIONAL; default OFF, chưa accepted notification delivery. |
| UC031 | Start/resume, workday/interval and blocker resolution | PROVISIONAL V1, exact isolated opt-in; không mở Work-Time HTTP endpoints. |
| UC032 | Scoped assigned read, progress/checklist/evidence upload và CSRF/ETag validation | Implemented source; progress 100% không suy ra Complete/READY. Acceptance không được suy từ unit/browser tests. |
| UC033 | Blocker report + interval closure/audit/mandatory staging | PROVISIONAL V1; default OFF. |
| UC034 | Complete với task/progress ETag, required checklist, interval closure, Pending Inspection | PROVISIONAL V1; không tạo Inspection conclusion/READY. Default OFF. |
| UC035 | Read source/accept/start rework, exact defect/source digest/version | PROVISIONAL_NOT_ACCEPTED; chỉ SYNTHETIC_TEST_ONLY proof ở isolated candidate, không authority thật. |
| UC036 | Per-defect remediation/evidence, interval close, PENDING_REINSPECTION + staging | PROVISIONAL_NOT_ACCEPTED; không giả accepted handoff/conclusion. |
| UC037 | Monitor/filter/detail/timeline/readiness; cancellation candidate | Read implemented. Cancellation PROVISIONAL V1/OFF; previous non-cancelled cycle thiếu authority tiếp tục fail-closed. |
| UC038 | Scoped turnover list/detail/evidence, source coverage | Implemented read với incomplete Inspection/RoomHistory coverage; unavailable result filters trả 503, không suy PASS/FAIL. |

## Compatibility và merge safety

- 53 dòng đăng ký DI gốc được giữ nguyên; chỉ thêm CleaningMonitorService, TurnoverHistoryService, ProvisionalCleaningNotification/Delivery và hai transaction interceptors. Không hosted worker, producer activation hoặc 06R1 compile flag.
- RoomTypeCreationService chỉ thêm optional candidate dependency. RoomTypeBatchService thêm optional beforeCommit/afterUserLocks và preserveCommitErrors; default của caller cũ giữ nguyên, không bỏ transaction/session/permission revalidation.
- HotelOpsDbContext.RoomTypes.cs chỉ thêm ConfigureWorkTime/ConfigureNotificationCandidate; không sửa mapping gốc. Ba entity Workday/WorkInterval/TimeCorrectionRequest và participant/mapping được copy nguyên identity vì FE-04 dùng chung Work-Time transaction. Đây không phải triển khai Work-Time HTTP hay DDL.
- Develop chưa có frontend. Thêm router/header chỉ FE-04; giữ nguyên 7 screen entry points và các panel/transport nghiệp vụ. Không kéo prototype/mock/Auth UI/DEV2/FE-05 screens. Hai fixture browser sửa locator role và timelineLimit=20 theo contract hiện hành; không hạ validation.
- Startup validation, toàn bộ middleware sau builder.Build và shared configuration giữ nguyên. Model composition + negative missing-connection check PASS. Không dùng kết quả này để khẳng định SQL startup compatibility trên DB thật.
- 15 SQL files FS013/014/015/016/018 là source candidate/dependency giữ nguyên identity; KHÔNG thực thi DDL, tạo trial hay thay đổi shared DB. Baseline đã có housekeeping/checklist schemas; candidate opt-in vẫn đòi exact database/marker/schema.
- No source merge conflict trên base hiện tại; exact staged allowlist được so trước commit. Không commit/push original feature branch, không merge develop.

## Validation thực tế

| Check | Kết quả |
| --- | --- |
| dotnet build HotelOps.slnx trên release | PASS, 0 warning, 0 error; build cuối sau composition checks |
| dotnet test HotelOps.slnx --no-build --no-restore | Exit 0; solution API-only, không tính là regression tests |
| Focused backend FE-04/authorization/guards/routes | 240 PASS, 0 FAIL, 1 SQL SKIP do opt-in OFF |
| Release EF model + missing-connection startup validator | 2 PASS / 0 FAIL, chạy riêng; không SQL connection target |
| Frontend npm ci --ignore-scripts --no-audit --no-fund | PASS; có transitive dependency deprecation warnings |
| npm run build | PASS; React Router use-client directives bị bundler bỏ qua (warning) |
| FE-04 Node tests, 9 files | 49 PASS / 0 FAIL |
| Browser HTTP interception | Run đầu 71 PASS / 3 FAIL. Hai FE-04 fixture đã sửa, targeted retest 2/2 PASS. Một room-board case FE-03 ngoài scope bị grepInvert trong config release; source test giữ lại. 73 distinct FE-04 cases có PASS qua hai run, không claim một run 73/73. |
| Secret patterns | PASS trên mọi source file release; lượt đầu flag biến password sinh GUID trong live tests, đã xác minh false positive. Không phải vulnerability/full-history scan. |
| git diff --cached --check | Hai blank-at-EOF ban đầu ở AuthorizationTests/NoIdentity đã được bỏ; kiểm tra lại trước commit. Git cảnh báo LF→CRLF theo cấu hình máy. |
| SQL/HTTPS/live endpoints/accepted inbox delivery | NOT_RUN: không có approved isolated target trong lượt release này |

Build đầu thiếu dependency transaction/read schema và test helper đã FAIL, sau bổ sung dependency build/focused tests PASS. Một lệnh build ban đầu chạy tại workspace nguồn gặp ACL obj; không dùng làm receipt release. Một lệnh npx chạy nhầm workspace đã dừng khi đề nghị tải package, không tính test result. Không che các lần thất bại hoặc gọi synthetic PASS cho SQL/live.
Backend receipts local ignored: TestResults/fe04-release/fe04-release.trx và composition.trx; browser stdout của run đầu và targeted retest trong session. Không commit binaries/TRX/browser artifacts/secrets.

## External dependencies và quyết định

Giữ nguyên bằng chứng/contract đã có; không mở lại full audit FS001–FS020 hoặc xin phê duyệt lại.
Các hạn chế acceptance còn nguyên: 06R1 publisher chỉ Maintenance action path, policy sealed không Cleaning extension, delivery SourceType maintenance_work, C04 graph khác FS013; UC046 authoritative FAILED/defect proof + reinspection handoff thiếu; terminal previous-cycle readiness authority chưa có. Không sửa DEV2, không giả Maintenance URL, không tạo competing graph/DDL/proof authority.
Notification producer/worker và candidate flags giữ OFF mặc định. Chưa xác minh authoritative Inspection, final SQL/HTTPS rollback/concurrency/revocation, accepted inbox delivery hay production least privilege. Không claim 11/11 UC complete.

Khuyến nghị **GO cho merge source-only với default-OFF/fail-closed được giữ nguyên**, Project Leader quyết định cuối. **NO-GO cho activation hoặc tuyên bố full acceptance** trước khi các dependency trên được chốt và kiểm tra trên approved target. Không tự merge.
