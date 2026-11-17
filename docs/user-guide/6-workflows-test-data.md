# Dữ liệu thực hành 6 workflow — W6_20261118

Cập nhật 18/11/2026. Đã backup, thử nâng cấp trên bản sao, áp dụng schema và dữ liệu đầu vào lên **HotelOpsDB**, theo xác nhận của người dùng. Đây là bộ dữ liệu bắt đầu quy trình; **chưa nghiệm thu E2E cả 6 workflow**.

## Mở dữ liệu

Ứng dụng local: https://localhost:7443/login. Chọn chi nhánh **DVBV — Branch ID 1**, tìm tiền tố `W6_20261118` hoặc số phòng `W6-1811-`.

| Workflow | Dữ liệu trên HotelOpsDB | Bắt đầu thực hành |
|---|---|---|
| 1. Tra cứu/phối hợp phòng | Phòng **16 / W6-1811-A**, **17 / W6-1811-P**, **18 / W6-1811-F**, **19 / W6-1811-M** | `le.truong` mở Room directory; các phòng mới có trạng thái vật lý DIRTY. Không tạo check-in/check-out hoặc quyền Reception mới. |
| 2. Vệ sinh | Task **16 / W6_20261118-A**, **17 / W6_20261118-P**, **18 / W6_20261118-F** | `mai.tran` phân công `lan.dang`. Cả ba ở PENDING_ASSIGNMENT; phải vào ca, thực hiện công việc rồi mới chuyển kiểm tra. |
| 3. Kiểm tra/làm lại | Phòng **17** dành kịch bản đạt; phòng **18** dành kịch bản không đạt/làm lại | Dùng task 17/18 làm đầu vào. Checklist mặc định CL-GEN, phiên bản 1 có sẵn; loại phòng test ID **11** có 6 mục vệ sinh sao từ checklist. Chưa tạo kết quả PASS/FAIL, inspection hoặc rework giả. |
| 4. Sự cố/bảo trì | Incident **13 / W6_20261118-INC**, phòng **19**, trạng thái OPEN | `minh.le` tiếp nhận sau khi có danh mục phân loại hợp lệ; `thinh.ngo` thực hiện khi được phân công. Chưa tạo lệnh bảo trì để vượt qua bước phân loại/triage. |
| 5. Yêu cầu vật tư | Request **14 / W6_20261118-MR1**, **15 / W6_20261118-MR2**, **16 / W6_20261118-MR3** | Chủ yêu cầu `lan.dang`, bộ phận HK, kho WH-HK. Cả ba DRAFT, vật tư AMN-SOAP; số lượng 5, 5, 5000. Yêu cầu cuối dùng thử trường hợp thiếu tồn; không bảo đảm tồn thực tế nếu người dùng thay đổi kho. Chưa duyệt/cấp hoặc điều chỉnh tồn. |
| 6. Ca/thời gian | **6 ca**, **12 phân công**, ngày **18–20/11/2026** | Ca MORNING 06:00–14:00 giờ Việt Nam; `mai.tran`, `lan.dang`, `minh.le`, `thinh.ngo`. Trên DB chính chưa bắt đầu workday để người dùng tự test. |

Các ID trong bảng thuộc **HotelOpsDB**, không dùng ID trên bản sao. Mật khẩu/tài khoản giữ nguyên, không cấp lại mật khẩu trong lượt này.

### ID phân công ca

| Tài khoản | 18/11 | 19/11 | 20/11 |
|---|---:|---:|---:|
| mai.tran | 18 | 19 | 20 |
| lan.dang | 21 | 22 | 23 |
| minh.le | 24 | 25 | 26 |
| thinh.ngo | 27 | 28 | 29 |

Đây là **Shift Assignment ID**, khác Shift ID. Ca HK lần lượt 14/16/18; ca MT 15/17/19. UTC được lưu từ giờ địa phương của mẫu MORNING, không sửa lịch sử tháng 10. Bốn membership mới chỉ phản ánh đúng `users.primary_branch_id`, `department_id` và branch access đang có; không thêm role/grant/phạm vi chi nhánh.

## Giới hạn đã xác định

- **Vật tư:** danh mục quyền hiện tại không có `MR_VIEW`; mã ứng dụng yêu cầu quyền này cho màn tạo/đọc yêu cầu. Không tự thêm quyền hoặc dùng sysadmin để bỏ qua. Tuyến người duyệt độc lập vẫn cần kiểm tra theo quyền và trưởng bộ phận hiện tại.
- **Sự cố:** `INCIDENT_CATEGORY` chưa có giá trị hoạt động. Báo cáo ban đầu được phép chưa phân loại, nhưng triage cần category hợp lệ; chưa thể chuẩn bị một lệnh bảo trì đã qua bước này mà không bổ sung cấu hình được chốt.
- **Vệ sinh/kiểm tra:** schema nền tảng đã cài, dữ liệu đầu vào hợp lệ; chưa chứng minh toàn bộ notification/capability và các bước gán, hoàn tất, kiểm tra, làm lại trên HTTP/UI. Không coi các marker nền tảng là chứng nhận mọi workflow đã chạy được.
- Backend vẫn ghi nhận lỗi có sẵn: `WorkAlertWorker` báo `InvalidOperationException`; `IncidentNotificationWorker` báo `FormatException` khi tạo SQL; EF cảnh báo ba index của projection `RoomTypeBranchV1` không map vào table. Lượt này không sửa các service đó.

## Backup và bảo toàn dữ liệu

- Backup đã xác minh: `artifacts/development-preflight-fd29bae16adf481187c7b7acf998a2bd/HotelOpsDB-W6-fd29bae16adf481187c7b7acf998a2bd.bak`.
- SHA256: `3F50D826D02878ABCA5D85AAD1783A74C5D5CA2301DFA1881D50BF3790B0BBB2`.
- Bản sao thử: `HotelOps_DEV3_142A_Recovery_fd29bae1`; đã RESTORE VERIFYONLY, phục hồi thực tế và DBCC CHECKDB trước nâng cấp.
- Receipt và kết quả từng gói trong cùng thư mục artifact. Các adapter local đã dùng nằm ở `.impeccable/review/six-workflow/`; source installer và entity EF dùng chung không bị sửa.
- Một lần backup trước ở thư mục Backup của SQL Server đã phục hồi được nhưng không hoàn tất hash vì Windows từ chối đọc file. Bản đó và clone `HotelOps_DEV3_142A_Recovery_33e69aea` được giữ nguyên; **không dùng receipt FAILED đó để nâng cấp**.
- Schema hiện có **110 bảng**, trước đó 72. Các gói thêm storage/concurrency/checklist/workflow/nhân sự theo source hiện có; phần seed permission của prerequisite membership được loại riêng để giữ nguyên danh mục quyền.
- Đã đối chiếu SHA256 giá trị tất cả cột gốc của **72 bảng ban đầu** trước/sau schema, sau seed và sau chạy seed lại. Bản ghi cũ, mật khẩu, role, permission, user-role và branch access khớp. Không backfill trạng thái workflow legacy.
- Fixture thêm audit `LOCAL_TEST_PROVISIONING`, actor NULL thể hiện provisioning local, không giả một lệnh HTTP của nhân viên. Các trạng thái hoàn tất/phê duyệt không được tạo trước.

## Kiểm chứng thực tế

| Kiểm tra | Kết quả |
|---|---|
| Backup, restore, CHECKDB bản sao | Passed |
| Schema: rollback trial rồi áp dụng bản sao trước DB chính | Passed cho các gói đã áp dụng |
| Seed: rollback trial, áp dụng bản sao, áp dụng DB chính | Passed |
| Chạy seed lại trên DB chính | ALREADY_SEEDED, không thêm bản ghi trùng |
| FK/CHECK, số lượng fixture, trạng thái ban đầu, scope, ca không trùng | Passed |
| So sánh dữ liệu gốc 72 bảng | Passed |
| EF đọc rooms/tasks/incidents/material requests/shifts, 13 inspections và 12 maintenance legacy | Passed |
| HousekeepingSchema / ProgressSchema | true / true |
| Service Start/End workday cho 4 tài khoản, trên bản sao, rollback toàn bộ | Passed; không phải kiểm thử quyền HTTP |
| Build `dotnet build HotelOps.slnx --no-restore` | Passed, 0 warning, 0 error |
| `dotnet test HotelOps.slnx --no-build --no-restore` | Lệnh exit 0 nhưng solution chỉ chứa API, không có test project: **Not Run**, không coi là test suite Passed |
| HTTP local | `/login` 200; `/api/rooms` khi chưa đăng nhập 401 |
| E2E 6 workflow qua UI và từng role | **Chưa hoàn tất**; giới hạn ở trên |

Kết quả EF/service: `artifacts/six-workflow-probe/result.json`. Kiểm chứng SQL: `artifacts/six-workflow-probe/fixture-verification.txt`, `schema-readiness.txt`.

## File và cách chạy lại

File mới:

- `scripts/database/development/Seed-SixWorkflowInputs.sql`: dữ liệu ban đầu, không migration hoặc thay quyền.
- `scripts/database/development/Seed-SixWorkflowInputs.ps1`: guard backup/identity, transaction, clone trước DB chính, rollback và receipt.
- `scripts/database/development/Verify-SixWorkflowInputs.sql`: kiểm chứng chỉ đọc, dành cho trạng thái ban đầu trước khi người dùng thực hành.
- Tài liệu này; cập nhật liên kết/trạng thái trong `6-workflows.md` và `6-workflows-data-preparation.md`.

Kiểm tra chỉ đọc:

```powershell
sqlcmd -S localhost -E -d HotelOpsDB -b -i scripts/database/development/Verify-SixWorkflowInputs.sql
```

Không chạy lại installer nền tảng trực tiếp: chúng có thứ tự phụ thuộc và guard máy/database lịch sử. Không restore đè DB chính hoặc xóa fixture bằng cách reset database. Chưa commit, push hay deploy.
