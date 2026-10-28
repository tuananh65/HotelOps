# DEV1 — Review & Package Tasks 15–17

Task18, 26/10/2026 (Asia/Saigon). Repository branch `feature/BE1-database-integration`, HEAD `5299b5e`. AGENTS.md đã đọc. Không commit, push, merge, chuyển nhánh, sửa Permission Matrix hoặc ghi HotelOpsDB.

## Kết quả review

- Canonical Database: composition mới của release + bốn patch DEV2, 82 bảng; original SQL và EF entities không bị sửa. Seed tách catalog/reference/personnel và giữ empty personnel. Setup chỉ dùng independent guarded targets, không overwrite existing databases.
- User Directory: GET /api/users là list slice, session + global USER_VIEW policy; Global fail-closed với BRANCH/OWN và branch-bound ALL, không role bypass. Scoped directory visibility và detail/effective permissions chưa triển khai.
- Query validation/filter/count/order/paging thực thi trên EF/SQL; DTO allowlist không có credentials. Program.cs chỉ có một scoped DI registration đã được duyệt. Không sửa auth pipeline hoặc source DEV2.

## Validation đã chạy trong Task18

| Kiểm tra | Kết quả |
|---|---|
| dotnet build HotelOps.slnx | PASS, 0 warnings / 0 errors |
| UserDirectory verification build | PASS, 0 warnings / 0 errors |
| UC-141 real API/SQL + scope doubles | PASS, 144 assertions |
| PermissionService / CurrentUserAccessor / Authorization / ResourceAuthorization | PASS, 43 + 14 + 61 + 49 = 167 checks |
| Tổng assertion regression | **311 PASS**; không phải số UC |
| git diff --check | PASS |
| PowerShell parsing + material-secret pattern scan | PASS; 0 pattern hits trong candidate files, có manual review |
| git diff --cached --check + exact index allowlist | PASS, index đúng 15 file A, không có file thừa/thiếu |

Staged-blob secret scan: 0 material pattern hits; không có JSON/evidence/WAV/backups/secrets/binaries/local receipt trong index. Git add có cảnh báo LF sẽ đổi sang CRLF khi Git chạm working copy theo cấu hình hiện có; đây là Git line-ending warning, không phải build warning. Không sửa cấu hình hoặc normalize các file để giấu cảnh báo.

UC-141 đã chạy lại trên **database trial mới dành riêng Task18**. Raw receipt ở `obj/Dev1Packaging/uc141-task18-results.json`, được gitignore và không stage. Không ghi hoặc reset HotelOpsDB, không dùng lại fixture database Task17. Các regression cũ chạy **không --live**; không vô hiệu target/least-privilege guards. Canonical trial lịch sử được review evidence, không chạy lại toàn bộ fresh/upgrade/CHECKDB trong Task18.

## Package và index

Đã chia exact allowlists trong [FILE_CLASSIFICATION.md](FILE_CLASSIFICATION.md), [security review](SECURITY_AUDIT.md) và [commit plan](COMMIT_PLAN.md).

- A: 15 file Canonical source/config/verification/docs, stage riêng trước.
- B: 9 file scope/planning/packaging documentation, đã audit và để dành commit thứ hai.
- C: 13 file UC-141 source/verification/docs, đã audit và để dành commit thứ ba.
- X: 123 local/out-of-scope files, không stage và giữ nguyên.

Staging chỉ chuẩn bị commit, không phải commit. Index chung chỉ giữ A để developer có thể review và commit A mà không lẫn B/C. Sau đó developer stage B rồi C theo plan; Task18 không chạy các lệnh commit được đề xuất.

Final index audit: **15 staged additions**, **1 unstaged tracked modification (Program.cs)**, **144 untracked files**. Trong số untracked: 21 file mới thuộc B/C và 123 exclusions. SHA256 trước/sau xác nhận **556/556 file có sẵn không đổi**; Task18 chỉ thêm bốn báo cáo này và ignored runtime receipt. HEAD vẫn 5299b5e, hai rehearsal worktrees còn nguyên.

## Ghi chú trước handoff

READY WITH NOTES cho packaging; không chứng nhận production deployment. Scoped admin visibility, SQL least privilege, machine-pinned trial tools, real-data upgrade và 165 EF-convention index advisories vẫn là các giới hạn đã biết. Historical Task16 coverage mô tả baseline trước UC-141; không sửa lại historical audit để giả vờ snapshot đó là source hiện tại.

Các raw evidence links trong hồ sơ Task15/17 là artifacts **local-only**, cố ý không có trong staged package. Markdown results và lệnh tái chạy được giữ; teammate muốn raw evidence phải tái chạy trên target độc lập đã review hoặc nhận artifacts qua kênh riêng được duyệt, không thêm receipts vào commit cho hết broken local links.
