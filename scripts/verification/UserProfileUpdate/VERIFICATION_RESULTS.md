# Task26 verification

- Solution build PASS0warnings/0errors;dotnet test solution exit0 nhưng không discover console harnesses.
- Profile harness build PASS,còn NU1900 do NuGet vulnerability endpoint không truy cập được;không tắt audit.
- Foundation regressions167/167 PASS (43+14+61+49),không --live.
- Schema trials fresh/upgrade26/26 PASS;guard/replay/rollback/ABA/stale/row preservation/table count.
- Runtime lượt đầu:List/Detail+strictPATCH/CSRF/update/stale PASS tới duplicate-email case;case đó FAIL vì scaffold constraint name không khớp fresh SQL-generated name. Đã sửa translation bỏ name dependency;không ghi lượt đầu là full PASS,trial được giữ.
- Sau fix,lượt full308/308 PASS;thêm edge cases và chạy trial mới:final329/329 PASS,evidence profile-verified-results.local.json. Đây là combined List/Detail/profile checks,không329 mutation-only tests. Harness thừa kế labelUC141 ở console.
- HTTP thật:management snapshot/no-store/ETag,positive profile PATCH,omitted/null/trim/strict unknown+duplicate/type/length rules,invalid IDs/cookies,CSRF,anonymous PATCH có valid anonymous CSRF401,missing If-Match428,malformed/weak/wildcard/list tag400,stale412,duplicate active/deleted email409,no-op token/audit invariance. Actor no-permission/branchboundALL/OWNER/restricted/inactive assignment không grant management. Read-token revoked/expired session401.
- SQL thật:2 contexts cùng token→1commit+1stale;injected audit failure→rollback profile/token/audit;old/new actor/target verified;roles/scope/credential/status unchanged và không revoke bình thường. Foundation scoped tests với real memberships+grant-path doubles:common branch kể cả primary khác,multi-branch không borrow grant,OWN self-only,inactive role/assignment,expired/future grants,revoked target membership failclosed. Không claim live catalog đã có USER_MANAGE/BRANCH/OWN grants.
- Original List/Detail assertions được chạy trước mutations trong combined harness;source Task22 được giữ nguyên. Catalog/master fingerprint cuối lượt giữ nguyên.
- Mutation fixtures chỉ disposable patched trial. No HotelOpsDB connection,writes,real session revoke,catalog edits hoặc migration.
- SELECT hậu kiểm cả4 trial Task26:0 unrevoked sessions,kể cả trial lượt fail;fixtures/DB retained,không drop/reset.
- Final solution build PASS0warnings/0errors;dotnet test solution exit0 (console suites không được test discovery).git diff --check/cached check PASS,index trống;Git chỉ thông báo LF/CRLF normalization cho existing Task22 files.
- Hash audit baseline598 files:chỉ2 pre-existing files thay đổi đúng approval(Program.cs2 DI lines,HotelOpsDbContext.cs1mapping);596 files còn lại nguyên SHA-256,bao gồm Task22/20A–25/local artifacts. Thêm5 source files và14 files trong UserProfileUpdate(6docs,2SQL,1PS,project,harness,3localreceipts). Không stage/commit/push/merge.
- Worktree listing có thêm HotelOps-AI-Loop ở cùngHEAD xuất hiện trong task từ external state;không tạo/sửa/xóa. Existing rehearsal worktrees cũng giữ nguyên.
