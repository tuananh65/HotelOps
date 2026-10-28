# Security & Data Safety Review

## UC-141 review

`UsersController` có global USER_VIEW policy trước action, response no-store và chỉ GET. PermissionService/policy/handler hiện có kiểm tra validated SQL-backed cookie identity, user/role/assignment activity/expiry. Chỉ ALL với null branch assignment cấp Global; BRANCH/OWN/branch-bound ALL không được query filters nâng thành toàn hệ thống. SYS_ADMIN/OWNER names không là bypass. Scoped directory visibility chưa có contract nên bị từ chối.

Service chỉ đọc User/Branch/Department/UserRole/Role, AsNoTracking và async/cancellation. Count và page dùng cùng predicate/UTC instant. Primary-branch filter không bị nhầm với arbitrary client authority. Sort FullName/UserId xác định; bounds page20/max100, positive IDs, overflow/status/search validation. SQL thực hiện filters/OFFSET; correlated role DTO projection không tạo N+1.

DTO chỉ chứa allowed identity/contact/branch/department/active-time-valid role-assignment fields. Không có password/hash, reset material, temporary credential, session hash/token, cookie secret, lock counters hoặc EF entity serialization. Role display không tuyên bố là effective-permission evaluator của target user.

Session validation/touch middleware không đổi. User-directory service không ghi users/assignments/catalog hoặc tạo sessions; session activity touch vẫn là security behavior có từ trước. SQL-backed verification dùng real API assembly và real credentials/session flow; không thay production security bằng permissive mock. Scope doubles bổ sung BRANCH/OWN cases mà không thay catalog SQL.

## Canonical setup review

Original release/patch sources giữ nguyên; in-memory target substitution giữ guards. Independent target regex, server identity checks, existing-database refusal và Task17 receipt/empty-personnel preconditions ngăn harness ghi nhầm main/old trial. Không có reset/drop/main restore hay auto grants. Canonical PRIORITY theo GBR-085, SEVERITY tách riêng. Trial-only Auth-upgrade synthetic role alias/grant fixture không được coi là migration production.

Reviewed historical final evidence xác nhận 82 tables, 9 rowversions, 238 FKs, 54 checks, 80 defaults, 201 indexes; catalog 12/10/62/140; fresh/RBAC/Auth fixture schemas bằng nhau; EF reads/persistence/concurrency pass. Raw snapshots/receipts không stage. Intermediate convention-index failure và URGENT trial được giữ local, được ghi nhận superseded; không giấu failure hoặc gọi mọi historical artifact là pass.

## Material/secret scan và classification

- Pattern scan candidate sources/docs: 0 private-key/static-long-password/API-key/token-material hits; manual review phân biệt tên biến password/hash với credential thật.
- Fixture passwords/CSRF/cookies chỉ ở memory. PFX password sinh runtime và truyền qua child environment; PFX unique nằm ngoài repo, exact-file cleanup sau khi dừng child. Không có credential literal, SQL password hoặc User Secrets được đưa vào package.
- Không stage bất kỳ JSON execution receipt/schema snapshot, WAV, local CREATED_FILES manifest, backup/recovery state, src/main.cs, ignored binaries hoặc artifacts trước Task15. Không khẳng định file ngoài scope đã được scrub sạch: cách xử lý là exclude và giữ nguyên.
- Global gitignore hiện có giữ bin/obj; Task18 không sửa shared ignore/config. Raw regression mới cố ý nằm dưới obj và git check-ignore xác nhận ignored.
- Đã scan staged blobs và đối chiếu exact allowlist sau staging: đúng 15 file A, 0 material-secret pattern hits, không có file thừa/thiếu. Không có .json/.wav/.bak/.pfx/.dll/.pdb hoặc local evidence path trong index. Cached whitespace check pass.

## Findings / limits

| Mức | Finding | Quyết định |
|---|---|---|
| No package blocker found | Global policy/query/DTO/DI scope đúng contract, regressions pass | Cho phép stage exact source/docs allowlist |
| Scope limitation | Scoped directory + target effective permissions chưa triển khai | Ghi rõ; không cấp quyền suy đoán |
| Deployment limitation | Trial scripts/harness pin reviewed local instance và dùng available developer principal | Không có secret; chưa portable/least-privilege deployment; không tự nới guards |
| Database limitation | Upgrade trials synthetic, không chứng minh upgrade restored real-data clone | Không deploy main dựa riêng các thử nghiệm này |
| Performance advisory | 165 EF-inferred nonunique indexes không khớp physical SQL exactly | Giữ advisory; không bulk-add indexes |
| Documentation/local artifacts | Một số links Task15/17 trỏ raw evidence cố ý exclude | Kết quả Markdown + reproduce commands là package; artifacts cung cấp riêng nếu được duyệt |
| Verification distinction | Existing console regressions không được solution discover | Chạy explicit harness, không suy test pass từ dotnet test zero-discovery |

## Task18 data impact

Chỉ setup và fixture writes trên một **new independent UC141 trial**; real API login/session/audit được phép cho fixtures, cleanup revoke đúng owned fixture users. Role/catalog/reference rows và user/assignment state trong read phase có fingerprint checks. Main DB và các trial Task15/17 không bị thay đổi. Không cấp SQL permissions hoặc tắt guard. Rehearsal worktrees và toàn bộ local/untracked artifacts có từ trước được giữ nguyên.
