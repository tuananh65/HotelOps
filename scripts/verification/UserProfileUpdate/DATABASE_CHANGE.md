# Trial-only rowversion patch

Files rowversion-forward.sql / rowversion-rollback.sql được tạo trước apply. Guards:DB không HotelOpsDB,name pattern Canonical Fresh,existing Task17 trial marker;rollback yêu cầu Task26 ownership marker+đúng SQL type189/nonnullable.

Forward thêm users.row_version rowversion NOT NULL,patch marker;replay cùngowned patch idempotent;refuse conflictingexisting column/type/another timestamp. Rollback drop đúngowned column vàmarker trong transaction. Guard thiếureceipt→throw trướcDDL. Không run guard test bằng kết nối HotelOpsDB.

## Fresh / upgrade rehearsal

Verify-Schema.ps1 tạo2 mới bằng original guarded setup (82tables,canonical references):
1.Fresh:empty personnel→forward→replay→rollback→reapply.
2.Upgrade:existing non-sensitive inactive User fixture trước forward→verify row preserved/8byte token→conditional write/stale reject/ABA detect→rollback preserve fixture→reapply.
Cả2 verify absent receipt rejects forward;tablecount82 preserved. **26 checks PASS**. Đây là upgrade current combined pre-rowversion schema,không chứng nhận mọi legacy DB version.

Schema receipt local ghi target names;DB names theo UTC (ngày có thể26 trong khi report local27). Trials retained,không reset/drop. Runtime fixture trial lượt fail được giữ riêng,kết quả final trên trial mới.

## EF / backward compatibility

User.ProfileConcurrency.cs thêm byte[] RowVersion trong partial,không sửa generated User.cs. DbContext1line IsRowVersion/column row_version. Profile UPDATE dùng explicit token predicate+affected rows vì ExecuteUpdate không auto áp EF optimistic check. SQL-generated token không client-supplied.

Existing consumers trên **patched schema** không cần gán RowVersion khi insert;SQL sinh token. Row_version có thể đổi khi DEV2/auth code update User;đó là expected conflict,chưa đổi workflow của họ. List/detail projection không expose thêm field;existing regressions chạy lại trên schema mới.

New EF model cần column trước binary deployment (SELECT User/FromSql users có thể cần column). **Không chạy binary mới với HotelOpsDB hiện chưa patch**. Task chỉ authorize trials,không production migration/deploy. Original release SQL/patches/seed giữ nguyên;không EF migration. Trước bất kỳ team rollout cần agreed forward release/DB-first regeneration procedure,review shared User writes.

Rollback chỉ khi dừng mọi binary phụ thuộc token,không với active traffic;issued ETags invalid,không thể khôi phục monotonic tokens qua drop/recreate. Không claim rollback giữ token values.
