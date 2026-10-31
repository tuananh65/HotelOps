# Task03A — RBAC Deployment Preparation

**READY WITH NOTES. Dừng và chờ phê duyệt riêng Task03B. HotelOpsDB chưa Upgrade.**

Đọc [DEPLOYMENT_READINESS_REPORT.md](DEPLOYMENT_READINESS_REPORT.md) để xem full context, source state, compatibility, backup/rehearsal, exact expected changes, risks và approval boundary.

## Hồ sơ review

- [PREFLIGHT_RESULTS.md](PREFLIGHT_RESULTS.md), raw sanitized `preflight-results.txt`, `deployment-state.json`: identity/schema/data/personnel baseline và126 protected file hashes.
- [REVIEWED_MAIN_DEPLOYMENT_TASK03B_PENDING.sql](REVIEWED_MAIN_DEPLOYMENT_TASK03B_PENDING.sql): candidate chính, **chưa chạy trên main**, có approval/identity/drift guards trong cả4 GO batches.
- [EXPECTED_CHANGES_FOR_TASK03B.json](EXPECTED_CHANGES_FOR_TASK03B.json): exact semantic grant differences/target140 tuples/personnel; không pin new identity IDs/timestamps giống clone.
- [BACKUP_RECOVERY_PLAN.md](BACKUP_RECOVERY_PLAN.md), `backup-evidence.json`, `restore-proof.json`, `recovery-preflight-results.txt`: current backup, actual restore proof, **SIMPLE recovery** và giới hạn post-backup data recovery.
- [VERIFICATION_PLAN.md](VERIFICATION_PLAN.md), `postdeployment-readonly.sql`: kế hoạch sau03B; SQL postflight chỉ chuẩn bị, chưa chạy trên main.
- `rehearsal-result.json`, `rehearsal-verification.txt`, `rehearsal-after.json`: kết quả clone-only Dry Run/COMMIT, exact catalog và integrity.

Main candidate SHA256:

`C1FD3904871107F0284CDC8ADF5CFF8193972B408ECBCBC3F3EBE51AF29B3A64`

## Kết quả đã thực hiện

1. Main preflight chỉ đọc:72/72 tables khớp original Task02 source baseline, 11 roles/47 permissions/182 grants; phương án nhân sự còn đúng.
2. Backup main COPY_ONLY CHECKSUM mới, VERIFYONLY và actual restore vào clone độc lập `HotelOps_RBAC03A_Rehearsal_20261021_a236462a264f`.
3. Approval gate test PASS53001; Dry Run rollback trên clone khôi phục72 tables/schema; COMMIT clone đạt12 roles/10 active/2 inactive,62 permissions,140 grants; exact metadata/tuple comparison PASS; Owner2 ALL, personnel và integrity PASS.
4. Thêm6 migration-level audit records trong clone, giữ audit cũ; đây là phần candidate cần approve cùng catalog/personnel.
5. Lặp read-only drift guards ở tất cả4 batches; test53005 trên clone đã nâng cấp chứng minh mỗi batch từ chối trước writes. Mutation body không đổi so bản đã Dry Run/COMMIT. Không thực hiện COMMIT lần hai hoặc source Dry Run.
6. Main72 table contents/schema và126 protected files giữ nguyên. CurrentUserAccessor console14/14 PASS; không sửa C# hoặc chạy build/solution test. Authenticated HTTP smoke test chưa chạy.

## Phạm vi chưa hoàn tất

Department Retirement riêng vẫn BLOCKED:4 legacy departments Active, stocks/requests/shifts nguyên vẹn. Không mang cleanup Task02C sang source. DataScope EF mapping/PermissionService cần task riêng; chưa triển khai Authorization Core đầy đủ.

SIMPLE recovery không có bảo đảm PITR cho writes sau backup. Trước03B cần recheck backup/baseline và phê duyệt writer quiescence, kể cả session-touch requests. Không tự cập nhật hashes hoặc set approval marker để chạy main.

## Script orchestration và replay

`Prepare-Task03A.ps1` chỉ chuẩn bị và chạy clone; source write route duy nhất là authorized COPY_ONLY backup. `Verify-Rehearsal.ps1` phụ thuộc helper từ orchestrator và chỉ DBCC trên clone tên đã kiểm tra. Không chạy trực tiếp những script này để triển khai main.

Rehearsal đã hoàn tất, không replay Preflight/Rehearse/restore. Explicit resume đã dùng sau lỗi ACL Get-Item, không backup lại/overwrite. Các SQL/result trong `initial-rehearsal-evidence/` là **superseded historical evidence, không được dùng làm candidate**. Root main script có repeated guards là bản để duyệt.

Session-context marker là latch chống chạy nhầm, không phải credential hoặc thay approval con người. Script không tự set marker trên main; Task03A chưa execute candidate main dù chỉ một batch. Không hướng dẫn bật latch để chạy ngay trong Task03A.

Không backup/secrets/connection strings trong Git; backup ngoài repo, clone giữ lại. Chỉ tạo artifact Task03A, không sửa shared Release/source/config, không commit/push/merge/PR. **Chờ Task03B explicit approval cho exact SHA256 + changeset.**
