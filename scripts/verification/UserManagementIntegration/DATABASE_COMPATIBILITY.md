# Database compatibility — verified trials and blocker

## Đã thực thi trong Task27

Fresh/upgrade canonical pre-rowversion trials mới,82tables,forward/replay/rollback/reapply:26/26 checks PASS. Upgrade có inactive non-sensitive user trước patch;identity/profile preserved,token8bytes được cấp;stale UPDATE affected0 và ABA token không lặp. Guard thiếuTask17 receipt bị reject trướcDDL. Trials theo UTC name,không production target.

Task26 forward/rollback dùng transaction+XACT_ABORT,ownership marker/type guard;rollback đúngowned column,không rows. Replay hỗ trợ owned current shape,không hứa concurrent DDL runners hoặc mọi legacy version. Rollback làm issued ETags vô hiệu,phải drain/stop new binary trước.

Original release/canonical SQL giữ nguyên. Patches hiện chỉ chấp nhận Canonical Fresh trial+receipt,**không phải deployable production migration**. Cần reviewed release artifact với expected target/signature và approval riêng;không bỏ guard để chạy HotelOpsDB.

## Runtime fail-fast — requirement FAILED

Đã khởi động actual API binary trên fresh canonical trial chưa patch:
- /api/auth/csrf phục vụHTTP200,nghĩa Kestrel nhận traffic.
- /api/health/database trả200 vì chỉ CanConnectAsync.
- New EF User.RowVersion projection thực thi SQL lỗi207 Invalid column.
- Program.cs không check schema trước app.Run.

Source Authentication/CredentialLockoutService materialize User qua SELECT* nên schema mismatch còn có thể gây thiếu required mapped column trước credential verification. Không tuyên bố đã gửi real credential login tới old schema;runtime proof ở trên là metadata/EF/HTTP startup. Patched trial login/cookie/session/list/detail đã thực thi thành công.

Không thể chọn health200 làm deployment gate khi thiếu column. Đây là DEV1 source fix nhỏ được **đề xuất**,không tự sửa trong Task27.

## Exact proposed application diff — chưa áp dụng

Chỉ Program.cs,ngay sau var app=builder.Build() và trước middleware/listen:
```diff
 var app = builder.Build();
+await using (var readinessScope = app.Services.CreateAsyncScope())
+using (var readinessBudget = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
+{
+    var readinessDb = readinessScope.ServiceProvider.GetRequiredService<HotelOpsDbContext>();
+    var matchingColumns = await readinessDb.Database.SqlQueryRaw<int>(
+        "SELECT COUNT(*) AS [Value] FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.users') " +
+        "AND name=N'row_version' AND system_type_id=189 AND max_length=8 AND is_nullable=0")
+        .SingleAsync(readinessBudget.Token);
+    if (matchingColumns != 1)
+        throw new InvalidOperationException("User schema is not ready for this application version.");
+}
 app.UseMiddleware<AuthenticationExceptionMiddleware>();
```

Dùng existing EF namespace/DI;không User entity query,không auto-migrate/seed/ALTER,không log connection string. DB unavailable/permission/cancellation cũng phải fail startup (không listen). 30s là proposed bounded startup budget,không thay permission-policy5s hoặc disable security. Metadata query không có trailing semicolon để composable SingleAsync. Cần review/apply và test:new binary old/malformed/unavailable schema exit trước listen;patched schema startup thành công;read-only metadata principal có quyền thực tế. Diff hiện chưa được build/run như production source.

## EF / DEV2 compatibility

Model RowVersion byte[] nằm partial User.ProfileConcurrency.cs,mapping IsRowVersion đúngtype column;profile ExecuteUpdate dùng explicit WHERE token+affected-row,không giả EF auto áp predicate cho ExecuteUpdate.

Đã execute82 EF entity SQL projections TOP0 trên patched trial,gồm DEV2 tables,không materialize business rows hoặc write. Solution build và actual auth+User API regressions PASS. Không sửa module DEV2. Đây chứng minh mapping SQL compatibility,không chứng nhận tất cả nghiệp vụ Inventory/Incident/Maintenance/Assets/MR/Replenishment workflows.

Any User UPDATE(auth hoặc future DEV2 writer) đổi token,profile412 là expected. EF tracked User updates sẽ mang concurrency token;cần team review detached/stale User writers trước rollout. Old binary thường bỏ qua extra column;không chạy old binary build từgit riêng nên old-binary runtime backwards compatibility chưa được chứng nhận. New binary→old schema đã chứng minh unsafe.

## SQL permissions cần review

- Setup tạo trial cần CREATE DATABASE/admin tương ứng;DDL patch cần ALTER dbo.users và quyền extended properties theo DBA policy.
- Runtime không cần ALTER/CREATE DATABASE/drop schema. Profile cần SELECT metadata/fields/users,SELECT role/grant/branch/membership,UPDATE full_name/email/phone/updated_at,INSERT audit_logs.
- Whole app Authentication còn cần existing users credential/lockout reads/writes,user_sessions create/touch/revoke,audit;profile-only grants không đủ cho whole application. Không thay permissions SQL hiện hữu.
- Metadata sys.columns visibility cần principal có metadata visibility của dbo.users(thường được cấp qua SELECT object);không mặc định cần VIEW SERVER STATE.
- Trials dùng available integrated developer principal;least-privilege SQL login end-to-end chưa test,không suy từ dbo thành production permission certification.
