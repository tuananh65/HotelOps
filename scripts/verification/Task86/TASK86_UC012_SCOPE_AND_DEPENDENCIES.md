# Task86 — UC012 scope và dependency

Branch xác minh: `feature/BE1-database-integration`; HEAD `4a187320643d0c82f232c6b9dd5a667b8401c280`. Upstream cùng tên. Bốn worktrees ghi trong PREFLIGHT.json; không checkout hoặc thay refs/index.

## Nguồn yêu cầu và ledger

- AGENTS.md: Database First, không sửa generated EF/shared DI/schema khi chưa approval; business service, transaction+audit, async/CancellationToken.
- `docs/requirements/srs_sections/02_use_case_specifications.md:1055`: UC012 Update Branch Information, main Update + alternate Create và Deactivate; actor Administrator. Không có yêu cầu notification UC012.
- `05_requirement_appendix.md`: GBR-001/002 authentication/authorization; 003 input/case-insensitive code; 005 SQL row_version; 007 atomicity; 008 audit; 032 branch code/name/address/timezone; 033 dependencies khi deactivate. SYS-PAR-10 Asia/Ho_Chi_Minh.
- `SRS_CHANGE_NOTES.md` và các pending-role rules không cấp Reception/Department Head quyền branch administration mới. Không sửa permission matrix.
- `scripts/verification/Task80/UC_LEDGER_TASK80.json`, row UC-012: PARTIAL, weight=2, verifiedPoints=0; Task74 có create/read/metadata-update lịch sử nhưng dùng aggregate hash ETag, chưa deactivation. Snapshot không phải runtime hiện tại.
- Task84 UC142 và Task85 Phase A/B đã có evidence riêng. Chỉ dùng làm baseline/harness pattern; không cộng lại điểm hoặc ghi đè receipts cũ.

Baseline 26/75, 60/256 = 23.4375%. UC012 chưa có earned points. Không chỉnh master ledger trong Task86.

## Compatibility matrix

| Thành phần | Checkout trước Task86 | Thực hiện Task86 / gate |
|---|---|---|
| Branch | identity bigint; Code varchar(30) unique; Name nvarchar(200); Address nullable nvarchar(max); Phone varchar(20); Email varchar(150); Timezone default; bool IsActive | Không sửa generated entity |
| EF Branch mapping | HotelOpsDbContext.cs:531, không RowVersion | Không sửa DbContext; đọc SQL projection không phải EF entity |
| SQL rowversion | Schema nguồn branches chưa có | B design-approved + SQL_APPROVAL.json; một cột đã cài trên đúng trial mới, không migrate schema nguồn/shared DB |
| Create/Update DTO | BranchMaintenanceDtos.cs đã tồn tại, JsonUnmappedMemberHandling.Disallow | Dùng lại, không sửa file cũ; update reason hợp đồng cũ; không cho client đổi Code/status/timezone |
| Permission | MASTER_DATA_MANAGE, catalog SYS_ADMIN/ALL duy nhất | EvaluateGlobalAsync bằng SQL data source; không role-name bypass. Branch-bound ALL/BRANCH/OWN không được coi là global grant |
| Runtime Branch writer | Không có Branch administration service/controller trong checkout | Thêm service/controller DEV1; DI một dòng A đã duyệt và áp dụng |
| Audit | audit_logs, JSON before/after/fields | SaveChanges trong cùng transaction mutation; UTC từ SYSUTCDATETIME |
| Deactivate | Chưa có safe writer phối hợp xuyên module | Chỉ dependency facts + fail closed; không Active→Inactive |

Approval A/B và SHA nằm ở A_APPROVAL_APPLIED.json / TASK86_SHARED_APPROVAL_REQUEST.md. B được duyệt thiết kế trước rồi cấp SQL execution riêng trong SQL_APPROVAL.json; SQL_RESUME_APPROVAL.json giới hạn fixture bổ sung. Không dùng approval của Task84/85.

## Ranh giới kỹ thuật

Transaction Serializable, UserEligibilityLock(actor) → SQL session/user locks → effective global permission → schema guard → feature create applock hoặc Branch UPDLOCK/HOLDLOCK → mutation → audit → session/permission recheck → commit. Non-2xx hoặc exception dispose transaction rollback. Deadlock 1205 trả lỗi 503, không retry rồi tính PASS.

Create sử dụng identity SQL, code trim/upper + check normalized code + unique SQL index. Update SQL predicate id+row_version, strong Base64 ETag 8 bytes; no-op không audit/UpdatedAt/version mutation. Schema thiếu rowversion/CI unique-code contract trả 503, không fallback hash.

Dependency reader là facts preview, không phải chứng nhận đủ điều kiện deactivate. Scope PM/orphan maintenance và terminal-state review chưa đủ. Không có quyền thay writer DEV2 hoặc triển khai protocol chung. Chi tiết trong TASK86_BRANCH_DEACTIVATION_MATRIX.md.

## Đã bảo toàn trước mọi ghi/build

PREFLIGHT.json: HEAD/index/branch/upstream/status/worktrees, 1095 file SHA256/mtime trong src/docs/scripts/tests, loại trừ bin/obj/artifacts/node_modules/packages. Build outputs cách ly dưới Task86/artifacts. Không tuyên bố đã hash các generated build outputs bị loại trừ.
