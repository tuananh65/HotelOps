# Task86 — exact change scope

## Production source

| Path | Loại | Mục đích |
|---|---|---|
| src/HotelOps.Api/Controllers/BranchAdministrationController.cs | NEW | Năm HTTP routes UC012, strict ETag/HTTPS, delegate service |
| src/HotelOps.Api/Services/HotelStructure/BranchAdministrationService.cs | NEW | Create/read/update, dependency preview, fail-closed deactivate, SQL authorization/session/transaction/audit |
| src/HotelOps.Api/Services/HotelStructure/BranchAdministrationSchema.cs | NEW | Required SQL-rowversion/CI/unique-code metadata gate |
| src/HotelOps.Api/Services/HotelStructure/BranchDeactivationDependencyReader.cs | NEW | Branch-scoped dependency facts; unresolved scope không cấp phép deactivate |
| src/HotelOps.Api/Program.cs | MODIFIED APPROVED A | Duy nhất một AddScoped BranchAdministrationService theo Program.di.patch; so với preflight, không so toàn git diff có local patches từ trước |

Không phục hồi/overwrite snapshot, không sửa generated EF/entity/schema-source/RBAC/auth/DEV2 code, existing DTO, Task82/84/85 reports/source/harness. Không dùng quyền SQL Task84/85.

## Verification/documentation

Toàn bộ file mới của nhiệm vụ nằm dưới `scripts/verification/Task86/`, gồm bảy reports bắt buộc, pre/postflight/hash manifest, exact approval scopes/receipts, trial-only rowversion/SQL fault proposal, guarded prepare/read-only scripts, DI and HTTPS runners, build/test logs và immutable receipts từng lượt. Output .NET cách ly dưới Task86/artifacts. SOURCE_AND_ARTIFACT_MANIFEST.json liệt kê exact paths/hashes ngoài generated outputs; không đưa mật khẩu/cookie/session credential vào artifact.

Prepare-Task86Trial.ps1 là bản sao byte-identical của .ps1.proposal đã duyệt (PowerShell không thực thi trực tiếp extension .proposal). Attempt đầu không chạy được extension; attempt default sandbox tiếp theo lỗi SSPI trước khi connect; lượt được cấp môi trường thực thi thành công. Không có automatic approval rejection.

Giữ receipt HTTPS đầu 83 PASS/1 FAIL (actor UC038 không có grant), resume 37 PASS/1 FAIL (runner @@SPID cast), resume2 51 PASS/0 FAIL. Sửa actor/runner trong files Task86 riêng, không sửa code UC038 hoặc catalog để làm test pass. Lượt resume2 dùng đúng mười users fixture đã được duyệt/tạo, không thêm user hoặc reset DB.

## SQL target duy nhất được ghi

`REDACTED_LOCAL_SQL_HOST\SQLEXPRESS` / `HotelOps_Canonical_20261104_223000_c01286a1_Fresh` — database mới tạo sau instance/nonexistence guard, giữ lại sau test. Base schema/canonical/catalog seed và prerequisites theo hash scope được duyệt; một Branch rowversion column; fixture t86.*, một supervisor bổ sung theo approval riêng; audit fault CHECK constraint đã gỡ. Không cập nhật role_permissions/catalog, không chuyển branch thành Inactive.

Không truy cập HotelOpsDB, không reset/truy cập trial Task82/84/85, không thay Git refs/history/index/worktree membership. Postflight đối chiếu 1095 file đã hash trước mọi ghi/build: 1094 hash+mtime giữ nguyên, một Program thay đổi đúng approval. Bin/obj/artifacts bị loại khỏi hash scope và không được tuyên bố đã so byte toàn bộ.

Làm rõ membership: câu trên mô tả **các thao tác Task86**. Postflight cuối quan sát một addition đồng thời ngoài những thao tác này: `D:/SEP490/Projects/HotelOps-Codex-B`, branch `ai/codex-b`, HEAD 4a18732. Bốn entries gốc còn nguyên; actual total=5, không tuyên bố toàn membership unchanged. Xem POSTFLIGHT_VERIFIED.json.
