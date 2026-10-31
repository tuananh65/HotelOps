# Verification Plan — after separately approved Task03B

## Gate trước execution

- Candidate đúng root package file `REVIEWED_MAIN_DEPLOYMENT_TASK03B_PENDING.sql`; SHA256 khớp `rehearsal-result.json` và approved report. Archived initial variants không phải candidate.
- Exact server, DB_NAME=HotelOpsDB, DB_ID22, createStamp2026-10-18 02:41:14.627; schema/fingerprints và personnel baseline còn khớp. Có drift thì stop/review, không bỏ guard.
- Approval context chỉ được operator đặt trong Task03B sau khi có explicit approval. Marker không phải credential/auth mechanism, không thay quyền SQL hoặc approval con người; file không tự set marker. Task03A chưa set marker trên main và chưa execute main file.
- Writer quiescence được phê duyệt và backup/recovery checkpoint phù hợp. Không chạy source Dry Run trong Task03A.

## Catalog — không chỉ đếm

Read-only script `postdeployment-readonly.sql` được chuẩn bị để chạy sau approval/COMMIT, không chạy trên source ở Task03A. Nó kiểm tra exact target và trả quantities, states, scope, personnel, audits và bidirectional EXCEPT với canonical seed tuples. Không INSERT/UPDATE/DELETE hoặc seed table variables.

| Check | Expected |
| --- | --- |
| roles | 12 total,10 active,2 inactive |
| inactive | chỉ HK_INSPECTOR và STAFF |
| permissions / grants | 62 /140 |
| scopes | ALL8,BRANCH81,OWN51; DEPARTMENT0 |
| exact codes/metadata | role and permission names/descriptions/module/resource/action/flags khớp Release |
| exact mappings | EXCEPT hai chiều trên role_code,permission_code,data_scope đều0 |
| OWNER | chỉ DASHBOARD_VIEW:ALL và REPORT_VIEW:ALL |
| SYS_ADMIN | chỉ6 administrative grants, không mặc nhiên operations bypass |

Standard Release04 vẫn phải chạy read-only trên correct main target. Cảnh báo4 noncanonical active department là expected known migration debt, không coi là full-model PASS hoặc tự sửa cảnh báo.

## Personnel và expected changes

- User4 DH B1, user19 DH B2, mỗi branch một active DH; extra DH5/17/18/23 B1 và20/25 B2 inactive assignments.
- User5 Storekeeper B1, không DH; user19 không Storekeeper.
- User1 OM B1 duy nhất, role branch1; Branch2 MANAGER access revoked, Branch1 giữ nguyên.
- Inspector8 B1 và14 B2 assignments inactive; new HK_SUPERVISOR assignments đúng branches, giữ supervisor2/12.
- Users4/19 department_id NULL phù hợp branch-wide DH. User rows26 giữ nguyên, không account/password/temp/lockout/primary-branch changes.
- Expected grants after alias normalization:18 added keys,60 removed keys,33 changed scopes; exact lists trong EXPECTED_CHANGES_FOR_TASK03B.json. New numeric IDs/timestamps không được ép giống clone do rollback identity gaps.
- Role alias updates giữ existing role IDs; thêm OWNER;15 new permission codes; metadata sync theo reviewed Release.

## Preservation / audit / integrity

- Main before→after chỉ7 tables đổi: roles,permissions,role_permissions,users,user_roles,user_branch_access,audit_logs. 65 bảng khác giữ count/full-row fingerprints, đặc biệt departments,stocks,requests,shifts và10 sessions.
- Riêng users, kiểm tra hash toàn bộ columns ngoài department_id khớp before; chỉ2 department changes đã duyệt. Không xuất bí mật vào báo cáo.
- Old14 audit rows phải hash-identical; append đúng6 migration-level UPDATE records, system actor NULL, entity_id deployment tag, JSON valid/no credential payload. Review actual old/new semantic diffs, không coi audit count là đủ.
- Schema metadata columns/FK/CHECK/default/index giữ nguyên. FK/CHECK enabled/trusted, unique indexes enabled; DBCC CHECKDB/CHECKCONSTRAINTS không findings. Chạy main integrity checks trong window được duyệt; Task03A DBCC chỉ trên clone.
- Department flags, warehouse/item quantities, request/shift lifecycle không đổi. Không copy Task02C cleanup từ trial cũ sang main.

## Authentication / API checks

Trong Task03A: source inspection PASS, console CurrentUserAccessor14/14 PASS; clone users ngoài department/session contents unchanged. **Authenticated HTTP/API integration chưa chạy**, không có credential được cung cấp và không reset credential để thử.

Sau Task03B, nếu có credential hợp lệ do chủ tài khoản cung cấp riêng và phê duyệt smoke test: login/session/logout/change-password flows theo phạm vi được cho phép; restricted identity giữ restricted; cookie/CSRF/session revocation vẫn đúng. Test tạo/touch/revoke sessions là writes thực tế, phải ghi riêng so với deployment preservation baseline, không gọi tất cả khác biệt là migration lỗi. Không thử change-password để test nếu chưa được cho phép tác động credential đó.

DataScope EF mapping chưa có: cần task riêng trước PermissionService; test role inactive/branch/OWN/SoD thuộc Authorization Core tương lai, không báo đã kiểm thử vì catalog có đúng62 permissions.

## Kết luận verification và receipt

Lưu exact target/script digests, approval, backup checkpoint, before/after snapshots, semantic changes/audits, schema/integrity outputs và smoke-test effects riêng. Source trước upgrade đối chiếu Source Baseline; main sau upgrade đối chiếu approved expected changes; rehearsal clone không phải source-equality target. Bất kỳ ngoài-scope difference nào phải đánh giá, không silently overwrite baseline.

READY WITH NOTES của Task03A không phải COMMIT approval. Dừng ở Task03A cho đến khi DEV1 duyệt riêng candidate/changeset Task03B.
