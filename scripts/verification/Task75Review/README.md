# Task75 — Baseline restoration and next batch scope review

Status: **REVIEW ONLY — no baseline restoration or feature implementation authorized yet.**

Ngày: 03/11/2026, Asia/Saigon. Sau yêu cầu batch, developer chọn: “Chỉ chuẩn bị danh sách thay đổi và phạm vi Task75 để review trước.” Quyết định này giới hạn lượt hiện tại ở hồ sơ review. Không coi Task75 đã hoàn tất implementation; không mở release gate, không chạy SQL, không tạo notification giả hoặc cộng điểm partial.

## A. Recovered latest task and baseline

Latest verified numbered implementation: **Task74**, snapshot `cf8fb020362f8f64192e7f2f14a10547cebe5b91` trên integration/dev1-fe-handoff; frontend `0006f47` kế thừa snapshot. Source controller/service, reports và accepted receipts được đối chiếu trong Git objects. Task74 local validation `obj/task74-validation.local.json`: success=true, 287 assertions, build PASS với NU1900; không discover framework tests. Đây là historical evidence, không phải tests mới chạy trong Task75.

Checkout hiện tại: feature/BE1-database-integration, HEAD `4a187320643d0c82f232c6b9dd5a667b8401c280`. Không có source Task60–74 trong working tree; không được implement từ baseline cũ hoặc copy đè local changes. Không tìm thấy Task75/76 implementation trước lượt này. Số 75 chỉ dành cho proposal đang chờ review này; latest completed implementation vẫn là Task74.

Preflight read đầu tiên ghi nhận `2026-11-03T13:55:18.6291412+07:00`. Preservation manifest: `obj/task75-preflight-20261103-065701/manifest.json`, gồm 665 tracked/untracked paths, existing deletion state, HEAD/index/worktrees và bản sao patch local. Không copy secrets hoặc truy cập database.

## B. Verified UC ledger and points

[UC_LEDGER_RECONCILED.json](UC_LEDGER_RECONCILED.json) chứa đủ 75 UC, tên đúng theo delivery ledger ở cf8fb02, classification, weight và earned points. Đã kiểm tra tổng: 26 complete, 9 partial, 19 not implemented, 21 blocked external; 60 verified points. COMPLETE_VERIFIED lịch sử và COMPLETE_VERIFIED_V1 đều thuộc 26 accepted UCs của baseline, không cộng lại.

- UC count: **26/75 = 34.6666667%**.
- Weighted: **60/256 = 23.4375%**.
- Remaining: **49 UCs / 196 points**.
- Lượt review Task75: **+0 UC / +0 points / +0 percentage points**.

Historical source/report/receipt evidence cho Task70–74 đã được đối chiếu: 345; final Task71 404; Task72 checklist282 và history208; Task73 600; Task74 287 assertions. Những tổng này gồm setup/shared checks, không phải số use cases và không cộng thành throughput độc lập.

## C. Proposed selected UCs and exact titles

Đề xuất một batch liên quan về cleaning/inspection, reuse phần đã có; **chưa đủ điều kiện bắt đầu cả batch**:

| UC | Exact SRS title | Weight | Baseline | Phần còn cần, không làm lại phần đã có |
|---|---|---:|---|---|
| UC-029 | Assign Cleaning Task | 3 | PARTIAL | Tích hợp và kiểm chứng mandatory notification delivery/retry; giữ assignment/workload/due/concurrency đã có |
| UC-041 | Assign Room Inspection | 3 | PARTIAL | Tích hợp notification cho inspector; giữ SoD, published checklist/cycle và assignment đã có |
| UC-046 | Request Cleaning Rework | 5 | PARTIAL | Notification cho assignee, durable retry/idempotency; giữ selected-defect links và rework writes đã có |
| UC-044 | Approve Room Inspection | 5 | NOT_IMPLEMENTED | Pending Conclusion → Passed, cleaning completion, Ready-unless-blocked calculation, atomic audit và notification |
| UC-045 | Reject Room Inspection | 3 | NOT_IMPLEMENTED | Failed criteria/defects/reason, Failed/Rework Required states, room recalculation, audit/notification và rework navigation |

Nguồn SRS: docs/requirements/srs_sections/02_use_case_specifications.md UC029/041/044/045/046; Change Notes; catalog hiện có; Task73 notification handoff và Task74 batch recommendation trong cf8fb02. Không có permission INSPECTION_APPROVE/INSPECTION_REJECT mới được tạo; catalog hiện có INSPECTION_PERFORM và việc gắn exact operation/resource sẽ phải được review theo contract, không suy quyền từ role name.

Điều kiện quan trọng: notification là bước bắt buộc được SRS mô tả, không phải chi tiết im lặng để áp dụng ASSUMED_V1. Room lifecycle/effective-block cũng đã được Task74 giữ unresolved; không tự map các status còn mâu thuẫn. Mục tiêu 4–5 complete chỉ khả thi sau khi external contracts có implementation/evidence, không phải cam kết hoàn tất trong lúc chúng chưa tồn tại.

## D. Baseline changes proposed for review

[BASELINE_RESTORE_MANIFEST.json](BASELINE_RESTORE_MANIFEST.json) liệt kê **226 proposed paths**, từng loại A/M, snapshot blob và current SHA256 nếu tồn tại. **Không path nào đã được restore.** Đây là inventory chênh lệch để review, không phải allowlist tự động apply toàn bộ:

| Nhóm | Added | Modified | Tổng |
|---|---:|---:|---:|
| Production source DEV1 | 66 | 7 | 73 |
| Verification harness | 44 | 5 | 49 |
| Existing DEV1 deployment packages | 21 | 2 | 23 |
| Documentation | 72 | 9 | 81 |

Bảy production files có sẵn cần đối chiếu trước khi phục hồi:

- src/HotelOps.Api/Controllers/UsersController.cs
- src/HotelOps.Api/Program.cs
- src/HotelOps.Api/Security/Authorization/PermissionService.cs
- src/HotelOps.Api/Services/Users/ApprovedDev1ResponsibilityQueries.cs
- src/HotelOps.Api/Services/Users/GlobalAdminSerialization.cs
- src/HotelOps.Api/Services/Users/UserAccountCreationService.cs
- src/HotelOps.Api/Services/Users/UserProfileService.cs

Shared impact: Program thêm sáu DI registrations cho AccessReview/HotelStructure/Rooms/RoomType services. PermissionGrantRules được tách để dùng chung với access review; partial DbContext mappings và hai model V1 mới hỗ trợ optional packages. Không thay generated baseline entities hoặc main DbContext. Snapshot còn chứa historical internal UC006 preparation: phần này cần tách review, không tự mở controller/DI/gates hoặc coi là batch feature mới.

Phải giữ nguyên local fallback hardening, Health AllowAnonymous, Development OpenAPI public và WeatherForecast deletions đã được duyệt. Snapshot cf8fb02 chưa có fallback/cleanup; không copy đè các file này. Program có thể ghép six registrations vào block hiện tại mà vẫn giữ OpenAPI change. Không restore toàn bộ branch hay checkout/reset/merge; chỉ apply từng phần được duyệt sau này.

Canonical evidence dumps, *.local.json receipts, WAV, bin/obj và Git config/history không nằm trong restoration inventory. Existing receipts tiếp tục đọc từ snapshot; không copy hàng triệu dòng evidence vào working tree chỉ để chạy feature. SQL package files là existing DEV1 artifacts để review; việc liệt kê không cấp quyền chạy SQL hoặc deployment.

## E. Proposed HTTP/HTTPS and per-UC acceptance

Giữ các endpoint snapshot hiện có:

- POST /api/branches/{branchId}/cleaning-tasks/{id}/assign (UC029).
- POST /api/branches/{branchId}/cleaning-tasks/{id}/inspections (UC041).
- POST /api/branches/{branchId}/inspections/{id}/reworks (UC046).

Routes mới **đề xuất, chưa triển khai/chốt contract**:

- POST /api/branches/{branchId}/inspections/{id}/approve (UC044).
- POST /api/branches/{branchId}/inspections/{id}/reject (UC045).

Mỗi UC phải có acceptance độc lập trên production Program.cs, HTTPS, real cookie/CSRF và disposable SQL: success, 401, missing permission, wrong branch/assignee, malformed input, missing source, invalid phase/criterion/checklist/defect, persisted business state + audit + notification relationship. Không dùng test stub notification hoặc response enqueue để nhận completion credit.

Public endpoints và fallback đã có được regression bằng runner hiện hữu; không sửa controller/service nghiệp vụ DEV2. Consumer action links phải tái authorization branch/source; notification không cấp quyền đọc resource.

## F. Required concurrency and rollback

UC029/041/046: duplicate request/idempotency, grant/session/assignee revocation, competing assignment/rework creation, business+audit+publication atomicity, actual delivery failure/retry/restart recovery và no duplicate delivery.

UC044/045: exactly one conclusion; concurrent approve/reject; criterion/defect/rework/checklist revisions; grant/session expiry after writes; effective RoomBlock and related DEV2 writers competing with Ready calculation; stale ETag/If-Match; audit/publication failure rollback. Exact resource lock order must được duyệt với DEV2 trước implementation; không tự claim existing lock covers all writers.

## G. Shared regression and validation status

Sau khi implementation được duyệt: chạy shared regression một lần cho UC013/023/142, UC038/048, UC039/040 immutable consumers, UC042/043 và UC032/109 nếu lifecycle writes chạm progress guards. Run dotnet build HotelOps.slnx và relevant console harnesses; git diff --check. Nếu dotnet test không discover framework tests thì không tính testing PASS.

Lượt review này: **build/tests/HTTPS/SQL NOT RUN** vì chỉ tạo review documents/manifest; không có C# change để build. Historical PASS ở Task74/fallback không được nhận là Task75 acceptance. JSON ledger totals và preservation checks được kiểm tra riêng.

## H. Blockers, owners and decisions needed

| Gate | Owner | Cần có trước dependent implementation |
|---|---|---|
| R1 Baseline restore | Developer + DEV1 | Review/select manifest paths; approve shared DI/mapping/auth/account impact; preserve local patch |
| R2 Notification producer/consumer | DEV2 owns delivery; DEV1 owns producers | Approved adapter API + atomic publication/storage ownership, real in-app delivery, retry/recovery/idempotency semantics và executable evidence; current Task73 handoff là proposal |
| R3 Room lifecycle/effective block | Team requirements + DEV1/DEV2 | Exact stored/projected states, current cleaning cycle, active block definition, retained shared writer protocol; không thay pending Reception permissions |
| R4 Conclusion contract | Developer + DEV1 | Confirm request/ETag/reason/result/defect eligibility, existing permission/resource mapping và stale/conflict behavior |
| R5 SQL/schema | Developer/DB owners | Explicit disposable target, reviewed existing module prerequisites; any new table/column/trigger/outbox needs separate schema approval |

Không sửa DEV2-owned implementation để tự đóng R2/R3. UC006 và UC016 giữ nguyên release/deactivation gates.

Alternatives đã đọc SRS, không phải safe full-completion replacements:

- UC037 Monitor Cleaning Tasks: read subset có thể độc lập; cancellation bắt buộc room recalculation và notification, nên GET-only chỉ PARTIAL.
- UC047 Reinspect Room: conclusion có room lifecycle + notification và rework/result model; chưa independent.
- UC107 Accept Assigned Task: Accept-only không hoàn thành Decline path có notification; stored Assigned/Accepted contract cũng cần review với coarse legacy task statuses.
- UC103 Set Task Priority and Deadline: notification, work-time cancellation guard và stop-series semantics còn thiếu.
- UC138 Manage Operational Workday and Shift Records: notification cho affected employees bắt buộc; không thể tính shift-definition-only complete.

## I. Classification

Task75 hiện là **SCOPE_REVIEW_PENDING**, không phải implementation complete. Ba selected partial UCs giữ PARTIAL; UC044/045 giữ NOT_IMPLEMENTED với các dependency nêu trên. Không đổi ledger classifications chỉ vì chọn vào batch; không tăng điểm vì đã có proposal.

## J. Progress and deadline

Actual: **26/75 UCs,60/256 points =23.4375%**. Remaining49 UCs/196points. Delta0.

Conditional forecast, không phải earned progress:

- Chỉ hoàn tất ba notification-dependent existing partials: +3UC/+11points →29/75,71/256=27.734375%.
- Cả năm selected UCs independently accepted: +5UC/+19points →31/75,79/256=30.859375%.

Deadline “before November21” được hiểu là trước 00:00 ngày21/11/2026 Asia/Saigon. Tại khoảng14:00 ngày03/11 còn17.4167 ngày; để hoàn tất phần còn lại cần khoảng **2.81UC/ngày và11.25weighted points/ngày**, tính cả ngày lịch và không trừ chờ dependency. Nếu dùng18 dates03–20/11 thì averages49/18=2.7222UC và196/18=10.8889points/date; đây là daily targets, không phải measured capacity.

Measured Task74: +2UC/+4points trong4158.1066seconds (~1h09m18s), khoảng1.73UC/hour/3.46points/hour cho đúng batch đó. Không extrapolate tuyến tính sang49UC còn lại: room/time/notification gates, external ownership và độ khó khác nhau. Target4–5 complete không đủ căn cứ cam kết khi R2/R3 chưa đóng.

## K. Actual elapsed time

Preflight started 13:55:18.6291412+07:00 ngày03/11/2026. Đây là preparation/review interval, **chưa có final implementation acceptance**, nên không báo elapsed-to-acceptance hoặc throughput Task75. Timestamp final review và preservation results sẽ được ghi trong REVIEW_VALIDATION.json. Bao gồm thời gian đọc/audit và chờ developer; không phải uninterrupted coding.

## L. Recommended next batch sequence

1. Developer review baseline manifest và chọn phần được phép khôi phục, giữ patch fallback local.
2. DEV2 xác nhận/deliver notification adapter cùng durable retry acceptance; DEV1 wiring ba producer flows sau contract approval, không reimplement assignment core.
3. Sau R3/R4, implement UC044/045 trên Task73 inspection execution foundation và chạy per-UC HTTPS/SQL acceptance.
4. Nếu chỉ một phần gates đóng, thực hiện phần độc lập có scope được duyệt; ghi đúng PARTIAL, giữ verified totals. Không bắt đầu UC006/UC016 để thay target.

Source/DB/HEAD/index/worktrees không thay đổi trong lượt chuẩn bị này; chỉ thêm review artifacts và preflight preservation records. Chưa commit/push/merge hoặc gửi handoff tới người/hệ thống khác.
