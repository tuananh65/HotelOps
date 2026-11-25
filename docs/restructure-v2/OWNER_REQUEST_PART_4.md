- Double shipment.
- Double receipt.
- Double write-off.
- Lost update.
- Duplicate notification.

Không cho hai người xử lý cùng một phiếu thành công khi chỉ cho phép một transition.

SignalR event không được phát trước khi dữ liệu commit.

Email retry không tạo lại User.

AI recommendation không tạo mutation nếu chưa có xác nhận của người dùng hợp lệ.

---

## 37. END-TO-END TEST SUITE — V2

Thực hiện tối thiểu 48 kịch bản dưới đây.

### GROUP A — ROLE & ACCOUNT

| ID | Test |
|---|---|
| E2E-01 | Gộp DH vào OM hoặc xác minh đã gộp |
| E2E-02 | Giữ/khôi phục Maintenance Staff |
| E2E-03 | Maintenance Supervisor quản lý đội kỹ thuật |
| E2E-04 | Tạo tài khoản và gửi email |
| E2E-05 | Đăng nhập lần đầu bắt buộc đổi mật khẩu |
| E2E-06 | Truy cập API nghiệp vụ trước khi đổi mật khẩu bị từ chối |
| E2E-07 | Thu hồi session/token sau đổi quyền hoặc mật khẩu |

### GROUP B — HOUSEKEEPING

| ID | Test |
|---|---|
| E2E-08 | Start Shift cấp đồ fill |
| E2E-09 | Kiểm tra số lượng vật tư hiện có trong phòng |
| E2E-10 | Chỉ bổ sung đúng số lượng thiếu |
| E2E-11 | Không đủ vật tư thì chặn assignment |
| E2E-12 | Reservation nhiều phòng |
| E2E-13 | Checklist Cleaning chưa đủ thì không cho hoàn thành |
| E2E-14 | Dọn phòng bình thường và Inspection PASS |
| E2E-15 | Cleaning Rework không trừ vật tư trùng |
| E2E-16 | End Shift hoàn trả đúng vật tư |

### GROUP C — INVENTORY & REUSABLE ITEMS

| ID | Test |
|---|---|
| E2E-17 | Xuất tiêu hao theo FIFO |
| E2E-18 | Xuất tiêu hao theo FEFO |
| E2E-19 | Không cấp lô đã hết hạn |
| E2E-20 | Hoàn trả giữ đúng Batch/Expiry |
| E2E-21 | Cấp chăn vào phòng không giảm tổng sở hữu |
| E2E-22 | Thu hồi chăn/khăn sang trạng thái cần giặt |
| E2E-23 | Hoàn thành giặt là và chuyển READY |
| E2E-24 | Vật tư hỏng chuyển DAMAGED |
| E2E-25 | Ghi giảm vật tư không thể phục hồi |
| E2E-26 | Low Stock dựa trên lượng khả dụng |
| E2E-27 | HK Staff yêu cầu bổ sung tiêu hao |
| E2E-28 | Storekeeper từ chối vì thiếu tồn |

### GROUP D — MAINTENANCE & INCIDENT

| ID | Test |
|---|---|
| E2E-29 | HK Staff ghi nhận sự cố phòng |
| E2E-30 | Reception ghi nhận sự cố phòng |
| E2E-31 | HK Supervisor xác minh nguyên nhân sự cố |
| E2E-32 | Maintenance Supervisor tạo và phân công phiếu |
| E2E-33 | Maintenance Staff nhận phiếu, checklist và sửa chữa |
| E2E-34 | Maintenance Supervisor nghiệm thu |
| E2E-35 | Không thể sửa và đề xuất thay mới |
| E2E-36 | HK Supervisor kiểm tra không đạt, yêu cầu sửa lại |
| E2E-37 | Phòng chỉ Ready khi toàn bộ blocker đã xử lý |

### GROUP E — INTER-BRANCH TRANSFER

| ID | Test |
|---|---|
| E2E-38 | Tạo yêu cầu điều chuyển DVBV → DVCT |
| E2E-39 | OM nguồn xác nhận hoặc từ chối |
| E2E-40 | Storekeeper nguồn xác nhận gửi hàng |
| E2E-41 | Storekeeper đích nhập số lượng thực nhận |
| E2E-42 | Xử lý chênh lệch Requested/Shipped/Received |
| E2E-43 | Không nhận hàng trùng hoặc vượt phạm vi |

### GROUP F — INTEGRATION & SECURITY

| ID | Test |
|---|---|
| E2E-44 | SignalR thông báo realtime đúng người |
| E2E-45 | AI dự báo nhu cầu vật tư |
| E2E-46 | AI đề xuất điều chuyển và phân tích sự cố |
| E2E-47 | RBAC, Branch Scope và Unified Internal Request |
| E2E-48 | Inventory Reconciliation và Regression toàn hệ thống |

Mỗi test phải có:

- Preconditions.
- Actor.
- Branch.
- Test Data.
- Steps.
- Expected Result.
- Actual Result.
- API Evidence.
- Database Evidence.
- UI Evidence.
- Verdict.

Không coi unit test thay thế hoàn toàn E2E.

Không coi AI mock là runtime AI PASS.

Không coi SignalR kết nối được là notification routing PASS nếu chưa kiểm tra người nhận.

Kiểm thử trên cả hai chi nhánh khi phù hợp.

---

## 38. MAIN DEMO WORKFLOWS

Sau khi hoàn thành implementation, chuẩn bị 8 luồng demo nghiệp vụ quan trọng nhất.

### DEMO-01 — Dọn phòng bình thường

Start Shift → Cấp đồ → Phân công → Nhận phiếu → Kiểm tra vật tư trong phòng → Bổ sung đúng phần thiếu → Checklist → Inspection PASS → Ready.

### DEMO-02 — Thiếu đồ tiêu hao

HK Staff tạo yêu cầu → HK Supervisor xác nhận → Storekeeper xuất theo FEFO → Staff nhận đồ → Tiếp tục công việc.

### DEMO-03 — Thay chăn và khăn

Cấp đồ sạch → Thu hồi đồ cũ → Chuyển Laundry → Hoàn thành giặt → Ready Inventory.

Tổng sở hữu không thay đổi.

### DEMO-04 — Sửa chữa thiết bị

Báo hỏng → HK Supervisor kiểm tra → Maintenance Supervisor phân công → Maintenance Staff sửa → Nghiệm thu kỹ thuật → HK Supervisor kiểm tra lại → Ready nếu đủ điều kiện.

### DEMO-05 — Không thể sửa, đề xuất thay mới

Báo hỏng → Kiểm tra kỹ thuật → Không thể sửa → Đề xuất thay mới → OM xử lý theo quyền → Thay thực tế → Kiểm tra lại.

### DEMO-06 — Điều chuyển giữa hai chi nhánh

Kho thiếu → Yêu cầu điều chuyển → OM nguồn xác nhận → Xuất hàng → Kho đích nhập thực nhận → Xử lý chênh lệch → Hoàn thành.

### DEMO-07 — Tạo tài khoản

Administrator tạo User → Email Invitation → First Login → Đổi mật khẩu bắt buộc → Truy cập workspace.

### DEMO-08 — AI và SignalR

AI đề xuất bổ sung/điều chuyển → OM xem xét → Tạo phiếu thật → SignalR thông báo cho đúng người xử lý → Theo dõi trạng thái realtime.

Các luồng demo phải sử dụng API và dữ liệu thật trên môi trường test được phép.

---

## 39. EXECUTION ORDER

### PHASE 0 — Audit

- Source audit.
- Database audit.
- Baseline tests.
- Requirement mapping.
- Change Impact Matrix.

### PHASE 1 — Actor Correction

- Kiểm tra OM/DH.
- Giữ hoặc khôi phục Maintenance Staff.
- Maintenance Supervisor.
- Role permissions.
- Membership.
- Authentication.

### PHASE 2 — Inventory Foundation

- Item classification.
- Batch.
- Expiry.
- FIFO/FEFO.
- Consumable balance.
- Reusable states.
- Stock movement.

### PHASE 3 — Housekeeping

- Start Shift.
- Staff Supply.
- Room Supply Assessment.
- Reservation.
- Checklist.
- Cleaning Completion.
- End Shift.

### PHASE 4 — Maintenance

- Incident terminology.
- Cause investigation.
- Work Order.
- Assignment.
- Maintenance Checklist.
- Rework.
- Equipment Replacement.
- Room Readiness.

### PHASE 5 — Inter-branch Transfer

- Transfer Request.
- Source approval.
- Shipment.
- Receipt.
- Discrepancy.
- Reconciliation.

### PHASE 6 — Account Security

- Email Invitation.
- First Login Password Change.
- Backend enforcement.
- Session handling.

### PHASE 7 — Realtime Notification

- SignalR Hub.
- Event routing.
- Frontend subscription.
- Notification persistence.

### PHASE 8 — AI

- Data preparation.
- Forecast.
- Transfer recommendation.
- Incident analysis.
- Provider integration.
- Human confirmation.

### PHASE 9 — Validation

- Frontend build.
- Backend build.
- Unit tests.
- Integration tests.
- Migration tests.
- E2E.
- Regression.
- Inventory reconciliation.

### PHASE 10 — Documentation

- SRS.
- Software Design.
- Test Plan.
- Architecture.
- UC mapping.
- Final report.

Không dừng sau audit nếu các phần tiếp theo có thể thực hiện.

Không để thiếu AI API key hoặc Email credentials chặn toàn bộ phần nghiệp vụ độc lập.

---

## 40. REPORT 3, REPORT 4, REPORT 5 ALIGNMENT

Cập nhật các tài liệu dự án để phản ánh đúng source sau thay đổi.

### Report 3 — Software Requirement Specification

Cập nhật:

- Actor List.
- Actor descriptions.
- Use Case Diagram.
- Use Case Specifications.
- Business Rules.
- Functional Requirements.
- Non-functional Requirements.
- Request Workflows.
- Inventory classification.
- FIFO/FEFO.
- Maintenance.
- Transfer.
- Account Security.
- AI.
- SignalR.

### Report 4 — Software Design Description

Cập nhật:

- System Architecture.
- External Systems.
- Database ERD.
- Data Dictionary.
- Class Diagram.
- Sequence Diagram.
- Activity Diagram.
- API Design.
- Authentication flow.
- Notification architecture.
- AI integration.
- Transaction boundaries.
- Inventory data model.

### Report 5 — Testing

Cập nhật:

- Test Plan.
- Test Cases.
- Test Data.
- Expected Result.
- Actual Result.
- Defect Log.
- Regression Results.
- E2E Evidence.

Đối chiếu tên và cấu trúc Report thực tế trước khi chỉnh tài liệu.

Không tự thay đổi format báo cáo của trường nếu không cần thiết.

Không bịa test result.

### UC Change Mapping

Tạo:

`docs/restructure-v2/UC_CHANGE_MAPPING.md`

Các cột:

OLD_UC | OLD_ACTOR | CURRENT_IMPLEMENTATION | NEW_ACTOR | CHANGE_TYPE | NEW_REQUIREMENT | SOURCE_EVIDENCE | TEST_EVIDENCE

Phân loại:

- RETAINED.
- MODIFIED.
- MERGED.
- REMOVED.
- RESTORED.
- NEW.
- BLOCKED.

Không tự đánh số lại toàn bộ UC.

---

## 41. FINAL DELIVERABLES

Tạo các tài liệu:

1. CURRENT_SYSTEM_AUDIT.md.
2. BUSINESS_CHANGE_IMPACT_MATRIX.md.
3. ROLE_PERMISSION_BEFORE_AFTER.md.
4. ROLE_MIGRATION_RECOVERY_REPORT.md.
5. INVENTORY_CLASSIFICATION_MATRIX.md.
6. FIFO_FEFO_DESIGN.md.
7. REUSABLE_ITEM_LIFECYCLE.md.
8. MAINTENANCE_WORKFLOW_V2.md.
9. INTER_BRANCH_TRANSFER_WORKFLOW.md.
10. ACCOUNT_INVITATION_SECURITY.md.
11. SIGNALR_ARCHITECTURE.md.
12. AI_INTEGRATION_DESIGN.md.
13. DATABASE_SCHEMA_DELTA.md.
14. API_CHANGE_REGISTER.md.
15. FRONTEND_CHANGE_REGISTER.md.
16. UC_CHANGE_MAPPING.md.
17. E2E_TEST_RESULTS.md.
18. INVENTORY_RECONCILIATION.md.
19. DECISION_AND_BLOCKER_REGISTER.md.
20. FINAL_IMPLEMENTATION_REPORT.md.

Không tạo tài liệu chỉ lặp lại prompt.

Mỗi tài liệu phải có source evidence hoặc kết quả thực tế tương ứng.

---

## 42. FINAL EXECUTION LIMITATIONS

Được phép trong phạm vi nhiệm vụ:

- Sửa Frontend.
- Sửa Backend.
- Tạo Database Migration.
- Điều chỉnh Role/Permission theo thiết kế.
- Chuẩn bị và kiểm thử data migration.
- Khôi phục role bị thay đổi sai nếu đủ bằng chứng.
- Bổ sung API.
- Bổ sung UI.
- Bổ sung SignalR.
- Bổ sung Email Integration.
- Bổ sung AI Integration.
- Bổ sung automated tests.
- Cập nhật tài liệu.

Không được:

- Tác động Project 1/Rebuild.
- Reset database.
- Xóa dữ liệu lịch sử.
- Xóa tài khoản hiện có.
- Tự đổi mật khẩu tài khoản hiện có.
- Tạo tồn kho giả.
- Tạo hạn sử dụng giả.
- Bỏ qua authorization.
- Bỏ qua concurrency.
- Bỏ qua transaction.
- Gửi email thật đến địa chỉ không được phép kiểm thử.
- Gửi dữ liệu nhạy cảm đến AI provider.
- Tự mua dịch vụ external.
- Tự sử dụng API key không được cấp.
- Commit/push/merge/deploy khi chưa có chỉ đạo riêng.

Không áp dụng migration hoặc recovery có tính phá hủy trên dữ liệu chính nếu chưa xác minh đầy đủ.

---

## 43. FINAL IMPLEMENTATION RECEIPT

Trả về:

PROJECT_TARGET:

REPOSITORY_HEAD:

DATABASE_TARGET:

BASELINE_AUDIT:

CHANGE_REGISTER_TOTAL:

CHANGES_ALREADY_IMPLEMENTED:

CHANGES_IMPLEMENTED_NOW:

CHANGES_REMAINING:

ACTIVE_BUSINESS_ROLES:

OM_DH_CONSOLIDATION:

MAINTENANCE_STAFF_RETAINED:

MAINTENANCE_SUPERVISOR_ALIGNMENT:

LEGACY_ROLE_RECOVERY:

INCIDENT_TERMINOLOGY:

INCIDENT_CAUSE_INVESTIGATION:

CLEANING_CHECKLIST:

MAINTENANCE_CHECKLIST:

WORK_ORDER_LIFECYCLE:

EQUIPMENT_REPLACEMENT:

ROOM_SUPPLY_ASSESSMENT:

ACTUAL_MISSING_QUANTITY:

STAFF_SUPPLY_LEDGER:

FIFO:

FEFO:

EXPIRY_MANAGEMENT:

CONSUMABLE_ISSUE_DOCUMENT:

REUSABLE_ITEM_STATES:

LAUNDRY_TRACKING:

DAMAGED_ITEM_WRITE_OFF:

END_SHIFT_RETURN:

LOW_STOCK_ALERT:

INTER_BRANCH_TRANSFER:

REQUESTED_QUANTITY:

SHIPPED_QUANTITY:

RECEIVED_QUANTITY:

TRANSFER_DISCREPANCY:

UNIFIED_INTERNAL_REQUEST:

ROOM_READINESS:

EMAIL_INVITATION:

FIRST_LOGIN_PASSWORD_CHANGE:

BACKEND_PASSWORD_CHANGE_ENFORCEMENT:

SIGNALR_REALTIME:

AI_DEMAND_FORECAST:

AI_TRANSFER_RECOMMENDATION:

AI_INCIDENT_ANALYSIS:

EXTERNAL_SYSTEM_ARCHITECTURE:

DATABASE_MIGRATION:

BACKEND_BUILD:

FRONTEND_BUILD:

UNIT_TESTS:

INTEGRATION_TESTS:

E2E_TESTS:

INVENTORY_RECONCILIATION:

REPORT_3_ALIGNMENT:

REPORT_4_ALIGNMENT:

REPORT_5_ALIGNMENT:

FILES_CHANGED:

DECISIONS_REQUIRED:

BLOCKERS:

FINAL_VERDICT:

Phân biệt:

- IMPLEMENTED.
- VERIFIED.
- PARTIAL.
- BLOCKED.
- NOT_TESTED.

Không tự điền PASS cho chức năng chưa chạy.

---

## 44. FINAL INSTRUCTION

Thực hiện DELTA IMPLEMENTATION dựa trên source hiện tại.

Các yêu cầu trọng tâm:

- Không bỏ đội bảo trì.
- Không chuyển Maintenance Staff thành HK Staff.
- Giữ OM đã gộp DH nếu không có quyết định mới thay thế.
- Chỉ cấp bổ sung vật tư đúng số lượng thiếu trong phòng.
- Quản lý FIFO và FEFO theo lô.
- Không cấp vật tư hết hạn.
- Phân biệt vật tư tiêu hao và vật tư tái sử dụng.
- Không làm giảm tổng sở hữu khi thay chăn, ga, gối hoặc khăn tái sử dụng.
- Chỉ ghi giảm khi mất/hỏng được xử lý hợp lệ.
- Có phiếu điều chuyển giữa hai chi nhánh.
- Có số lượng yêu cầu, số lượng gửi và số lượng thực nhận.
- Có quy trình tạo phiếu, nhận phiếu, checklist và hoàn thành.
- Có nhánh thay mới thiết bị.
- Có xác minh nguyên nhân sự cố.
- Có Email Invitation.
- Bắt buộc đổi mật khẩu lần đầu.
- Có SignalR realtime.
- Có AI hỗ trợ dự báo, điều chuyển và phân tích sự cố.
- Có báo cáo và tài liệu phản ánh đúng implementation.

Không xây dựng lại toàn bộ dự án.

Không triển khai chức năng trùng lặp.

Không giữ workflow cũ đã bị thay thế chỉ để tránh sửa code.

Bảo toàn những chức năng hiện tại không bị yêu cầu thay đổi.

EXECUTION MODE: Fullstack Delta Implementation + Controlled Database Migration + E2E Validation.

THINKING EFFORT: Extra High nếu khả dụng.

INTEGRATION OWNER: Một agent chịu trách nhiệm thống nhất contract và tích hợp toàn hệ thống.
