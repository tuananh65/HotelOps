# FS-014 — Cleaning Blocker, Resume & Complete

**PARTIAL — PROVISIONAL V1, chưa shared integration acceptance.** Baseline nhánh `feature/dev1-room-ops`, local/remote cùng `77d62009d18c766e2387dfb7f7cb9501dd741257`, working tree sạch. Không triển khai UC035–038, FE-14/15; không tạo PR/Tester package.

## UC và source changes

| UC | Trạng thái | Hành vi thực tế |
| --- | --- | --- |
| UC033 | BLOCKED dependency; có API boundary | POST `api/branches/{branchId}/cleaning-tasks/{id}/blockers`: session, branch/task, CLEANING_EXECUTE/current assignment, active BLOCKER reference, note, If-Match và GUID Idempotency-Key. Request hợp lệ trả 503 `CLEANING_CLOSURE_CONTRACT_REQUIRED`, không ghi blocker/lifecycle/notification. |
| UC031 Resume | Implemented candidate trong isolated trial | POST endpoint `/start` hiện có với `{ resolveBlocker: true, note }`; ON_HOLD/current assignee, Working workday, không interval mở, current cycle, snapshot hiện có. Resolution + interval mới + IN_PROGRESS + readiness + audit cùng transaction; giữ actual start và interval history. |
| UC034 | BLOCKED dependency; có API boundary | POST `/complete`: cùng boundary quyền/ownership/ETag/key/note, trả 503 trước mutation. Chưa có checklist/evidence completion workflow, Pending Inspection hoặc notification staging thành công. |

Frontend dùng execution panel và CSRF transport hiện có. Metadata thêm `resumeCapability`, `reportBlockerCapability`, `completeCapability`; metadata cũ mặc định unavailable. Resume V1 yêu cầu note và xác nhận; hai nút Report/Complete bị disable. Không có dữ liệu mẫu thay API, transport/router/session mới hoặc thao tác READY.

Các file source tạo/sửa:

- `src/HotelOps.Api/Controllers/CleaningExecutionController.cs`
- `src/HotelOps.Api/DTOs/Housekeeping/CleaningLifecycleDtos.cs`
- `src/HotelOps.Api/Services/Housekeeping/{CleaningLifecycleService,CleaningLifecycleRules,CleaningBlockerResolution,CleaningClosureBoundary}.cs`
- `frontend/src/api/dev1/cleaningExecution.ts`
- `frontend/src/screens/housekeeping/CleaningExecutionPanel.tsx`
- `frontend/tests/cleaning-execution.{test.mjs,spec.ts}`
- `tests/HotelOps.Dev1.RegressionTests/{CleaningClosureBoundaryTests,CleaningResumeSqlTests}.cs`
- `scripts/database/release/fs014-candidate/01_ResumeV1.sql`
- `scripts/verification/FS014/New-ResumeTrial.ps1`

## Permission, persistence và transaction decisions

Developer đã chấp thuận trong phiên FS014: current assignee có CLEANING_EXECUTE được xác nhận xử lý blocker, bắt buộc resolution note; chỉ isolated SQL trial, PROVISIONAL V1. Không đổi role grants, generated EF entities, mappings, global DI hoặc shared configuration.

Candidate sidecar `fs014_cleaning_blocker_resolutions` dùng `progress_id` làm source identity/FK. Task/reason/reporter/reported timestamp ở progress log BLOCKED; resolution lưu `resolved_by`, `resolved_at`, `resolution_note`. Null resolution là open; bộ resolution đầy đủ là closed, CHECK constraints ngăn partial state. Trigger thay đổi sidecar làm đổi task rowversion. Mọi BLOCKED log phải có persisted state và reference thuộc BLOCKER; không suy ra resolved từ progress/history hoặc từ client.

Resume đóng toàn bộ blocker open không liên kết nguồn của task với cùng note; ghi audit riêng cho từng progress source và audit RESUME_CLEANING. Blocker đã resolved, không liên kết nguồn, vẫn có thể Resume sau xác nhận/note; không ghi lại resolution cũ. Chỉ cập nhật workflow/status về IN_PROGRESS sau kiểm tra current cycle, giữ `actual_start_at`. Dùng BatchAsync/HotelOpsDbContext transaction hiện có, thứ tự user → WorkTime → branch/catalog; interval participant kiểm tra Working day và một open interval. Lỗi resolution/audit/time/readiness làm rollback toàn bộ. Request lặp với ETag cũ trả 412, không mở interval thứ hai; đây không phải durable replay response.

V1 chưa có accepted reader xác nhận Incident/Material Request. Bất kỳ blocker nào có link đều trả `BLOCKER_SOURCE_CONFIRMATION_REQUIRED`, kể cả imported resolved flag; không tự coi module nguồn đã xử lý. Legacy BLOCKED log thiếu sidecar trả `BLOCKER_STATE_UNRECONCILED`. Không tự backfill trạng thái lịch sử.

UC033/034 bắt buộc supervisor Notification theo SRS. Không tái sử dụng provisional publisher FS013 để mở ghi. Chưa chọn event code, recipients hoặc inbox records theo giả định. Hai lệnh không có đường thành công hoặc toggle bỏ qua Notification; xem [integration notes](../integration-notes/DEV1_FS014.md).

## Isolated SQL

Trial mới: `HotelOps_Canonical_FS014_Resume_V1_Fresh`, owner marker `HotelOps.FS014.Owner=DEV1_FS014`, contract marker `HotelOps.FS014.Resume=PROVISIONAL_V1`. Runtime Resume chỉ mở trên đúng target/markers và constraints/trigger. Provisioner tái sử dụng frozen FS013 manifest, kiểm tra normalized hashes; retarget exact isolated guard của foundation, không sửa source SQL cũ. Trial có schema WorkTime hiện có; không triển khai public Workday API.

Đã chạy `New-ResumeTrial.ps1 -ExecuteApprovedCreate -AllowLocalSqlCertificateException`. Certificate exception chỉ cho SQL local trial; không bỏ TLS của API. Lần đầu target `HotelOps_FS014_Resume_V1_Trial` bị canonical-name guard từ chối sau tạo database; giữ nguyên database dở. Lần sau tạo target canonical mới thành công. Không ghi trial lịch sử/shared/production; không drop/reset. Component fixtures là synthetic, append mới mỗi case; không phải dữ liệu UC033 thật. Fixture setup hiện dùng transaction; các fixture dở từ lượt đầu vẫn được giữ.

## Validation thực tế

| Level / command | Kết quả |
| --- | --- |
| `dotnet build HotelOps.slnx` | PASS, 0 warning/0 error; `evidence/build.log` |
| `dotnet test HotelOps.slnx` | Exit 0; solution chỉ chứa API, không dùng làm số test; `evidence/solution-test.log` |
| xUnit project, filter `FullyQualifiedName!~CleaningResumeSqlTests` | 217 PASS / 0 FAIL / 0 SKIP; `evidence/fs014-unit-regression.trx` |
| SQL component | Final 14 PASS / 0 FAIL / 0 SKIP, gồm audit failure/resolved/history; `evidence/fs014-sql-final.trx` |
| `npm.cmd test` tại frontend | 53 PASS / 0 FAIL; `evidence/node-regression.log`, gồm prototype tests có sẵn; không dùng prototype PASS làm backend acceptance |
| `npm.cmd run build` tại frontend | PASS; warning React Router `use client` và bundle >500KB |
| Playwright `--config playwright.fs013-ui.config.ts` | 9/9 PASS, API interception; không phải HTTP/SQL live acceptance |
| Live HTTPS/API/cookie/SQL | NOT_RUN trong FS014 |

Focused xUnit trước checkpoint: 78 PASS rồi 91 PASS. Component lượt đầu 12 FAIL do fixture dùng `status=ON_HOLD` trái CK_cleaning_workflow_v1; sửa fixture thành legacy IN_PROGRESS/workflow ON_HOLD. Retest1: 1 PASS/11 FAIL do helper ETag thiếu cast binary(8). Retest2: 12 PASS. Các TRX thất bại giữ nguyên trong evidence; không thay assertion hoặc constraint để làm xanh.

SQL component chạy service/EF/SQL thật; session validation là test double, fault audit/readiness là interceptor. Kiểm tra success, stale ETag, concurrent 200-equivalent/412-equivalent, duplicate, thiếu Workday, interval khác, sai owner, linked blockers, unreconciled source và hai lệnh default-off không mutation. Không xác minh authentication middleware/cookie qua HTTP hay public UC139 từ các component này. Unit inventory ở [FE-04](../unit-test-inventory/FE-04/UNIT_TEST_INVENTORY.md).

Các lượt build/Node đầu trong sandbox lỗi quyền ghi artifacts; chạy lại ngoài sandbox được cấp quyền. NU1900 vulnerability feed chưa truy cập được khi restore test project. Browser có NO_COLOR/FORCE_COLOR warning; runtime EF mapping warnings nền chưa sửa. Không đo coverage.

## Remaining gaps / FS-015

Chốt accepted supervisor Notification event/recipient/transaction/idempotency contract cho UC033/034; triển khai Report Blocker thật và Complete snapshot/evidence → Pending Inspection thật. Chốt source confirmation reader cho Incident/Material Request; chạy live HTTPS/API regression trên trial và review shared WorkTime deployment trước integration acceptance. Chưa mở UC035–038. FE-04 chưa complete, không tự cộng UC credit hoặc TESTER_READY/ACCEPTED.

Git: HEAD vẫn `77d62009d18c766e2387dfb7f7cb9501dd741257`; source/evidence chưa commit. Automatic approval review từ chối lệnh commit vì chưa công nhận chỉ dẫn trong tệp đính kèm là explicit developer approval theo AGENTS.md. Không chạy commit/push lại bằng đường khác; cần developer xác nhận trực tiếp commit và push nhánh `feature/dev1-room-ops`. Preflight remote cùng baseline; chưa có FS014 remote SHA. Không force-push, PR hoặc merge. Inventory ghi UNCOMMITTED, baseline và SHA256 của source để không gán sai commit identity.
