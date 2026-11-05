# Task86 — yêu cầu duyệt shared diff cụ thể

Trạng thái cuối: A APPROVED/APPLIED (A_APPROVAL_APPLIED.json); B DESIGN APPROVED và sau đó được cấp quyền thực thi chỉ trên trial mới (SQL_APPROVAL.json). Document này ghi phạm vi shared được review; không cấp quyền migrate database khác.

## A — Program.cs, một dòng DI

Exact diff: `Program.di.patch` trong cùng thư mục. Chỉ thêm:

```csharp
builder.Services.AddScoped<HotelOps.Api.Services.HotelStructure.BranchAdministrationService>();
```

Vị trí: ngay sau TurnoverHistoryService. Không xóa/sửa registration khác, middleware, config hoặc authorization. Controller đề xuất trong `BranchAdministrationController.cs.proposal` có 5 route Create/Edit/Update/Dependency/Deactivate; route deactivate chỉ fail closed khi branch còn Active, không thực hiện chuyển trạng thái trong giai đoạn chưa có consumer coordination.

Service và controller đã compile; 39 DI/boundary/EF translation checks PASS (`DI_BOUNDARY_RECEIPT.json`). Cùng scoped DbContext; A1 interface/concrete vẫn cùng instance. Đây không phải production startup hoặc HTTPS/SQL acceptance.

## B — dbo.branches.row_version, chỉ trial mới Task86

Exact SQL: `branch-rowversion-forward.sql.proposal`. Thay đổi duy nhất:

```sql
ALTER TABLE dbo.branches ADD row_version rowversion NOT NULL;
```

Chỉ database mới dự kiến `HotelOps_Canonical_20261104_223000_c01286a1_Fresh`, marker `HotelOps.UserDirectory.Trial=Task17`. Nếu có cột rowversion khác hoặc kiểu không tương thích: dừng. Script transaction/XACT_ABORT, chạy lại không thêm cột thứ hai. Không truy cập bất kỳ database có trước Task86, không áp dụng trên HotelOpsDB.

Lý do: GBR-005 yêu cầu rowversion thật; Branch entity/mapping và schema nguồn hiện không có. Service dùng parameterized SQL predicate `branch_id + row_version` và unmapped projection, nên **không cần sửa generated entities hoặc HotelOpsDbContext**. EF legacy bỏ qua cột SQL tự sinh này; update ngoài service vẫn làm SQL rowversion tăng. Cần xác minh compatibility, idempotency và rollback bằng trial thực tế trước khi công nhận PASS.

Chưa có quyền SQL: sau duyệt thiết kế B, vẫn cần duyệt riêng script tạo đúng một trial mới và fixtures/harness trước khi thực thi SQL. B không cấp quyền migrate shared database.

## Chưa đề nghị sửa DEV2 / protocol deactivation

`IncidentService.CreateIncidentAsync` gọi ValidateCreateAsync trước transaction; query branch chỉ kiểm tra tồn tại, không kiểm tra IsActive. Vì vậy Serializable scan của DEV1 không đủ ngăn incident mới sau khi deactivate commit. Không sửa writer DEV2; deactivation giữ `DEACTIVATION_CONCURRENCY_BLOCKED`.

Cần joint review riêng về active-branch guard được giữ tới commit cho mọi writer, lock ordering, scope của PM và terminal-state mapping. Đây là dependency handoff, không phải yêu cầu blanket approval để sửa các writer đó.

## Cơ sở approval gate

AGENTS.md: “If a task requires modifications to shared components, explain the impact and request approval before making those changes.” Task86 §5/§11 cũng yêu cầu exact DI/schema approval riêng. Quyền DI/SQL Task84–85 không áp dụng cho Task86.
