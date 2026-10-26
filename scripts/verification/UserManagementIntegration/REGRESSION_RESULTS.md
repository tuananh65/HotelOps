# Task27 actual execution

| Check | Kết quả | Giới hạn |
|---|---|---|
| dotnet build HotelOps.slnx | PASS,0warnings/0errors | Existing source Tasks22/26,chưa có proposed startup diff |
| dotnet test HotelOps.slnx --no-restore | Exit0 | Console harnesses không test discovery |
| Foundations |167/167 PASS |43+14+61+49,no --live |
| Fresh/upgrade/rollback/replay |26/26 PASS,trials mới | Current combined canonical pre-rowversion,không mọi legacy schema |
| Combined list/detail/profile/runtime grants |362 assertions/observations completed | Bao gồm4 old-schema diagnostics;không gọi362 release-readiness checks PASS |
| All EF entity zero-row projections |82/82 execute PASS | SQL metadata/model compatibility,không business workflow certification |
| Startup schema readiness requirement |**FAIL/BLOCKER** | Actual API listen/health200 dù EF User projectionSQL207 |
| Cleanup |0 unrevoked sessions,0 test-onlyroles trên3trials | Trials retained,không reset/drop |
| Integration harness build |PASS vớiNU1900 | Không truy cập NuGet vulnerability data;không disable audit |

Integration receipt success=true,checks362,releaseReady=false. 358 checks còn lại phục vụ functional/security regression;4diagnostics gồm target verification,EF207 và2unsafe startup/health observations. Không dùng exit0 của harness để phê duyệt release.

List/Detail assertions chạy trước mutations và giữ contract;PATCH strict fields/omitted/null/CSRF/401/403/404,ETag/read-token,428/412/409,no-op,duplicate deleted email,audit before/after,real SQL two-writer stale và audit rollback đều chạy lại. Credential/status/role/scope invariance sau profile mutation được kiểm tra;session revoke chỉ fixture cleanup hoặc fixture auth tests.

## Lỗi tooling / cold-start / warnings riêng

- New integration harness compile đầu fail vì block placement và local variable naming;đã sửa trong harness mới,built/runs thành công. Không sửa app để làm test pass.
- Initial sandbox Git network fail;ls-remote escalated xác minh refs thành công.
- NU1900 vẫn còn trên new harness;solution build0warnings. Không disable NuGet audit,không thay dependency để che cảnh báo.
- Protected401 readiness warmup vẫn dùng harness Task22;Task27 không thu latency/attemptcount cho từng cold attempt,không claim first-request timeout đã được giải quyết. Task22 historical cold timeout/fail độc lập với missing schema blocker. Proposed startup diff cầncold-start measurement riêng sau approval.

No HotelOpsDB connection/migration;runtime chỉ verified trial targets. Evidence *.local.json giữ local,không stage. Git/hash validation cuối ghi trong README/FILE_CLASSIFICATION.

Final build --no-restore cũng PASS0warnings/0errors;git whitespace/cached checks PASS;riêng7 Markdown mới+harness/project không có trailing whitespace/dòng trống thừa EOF. Baseline617files giữ nguyên;không sửa6tracked diffs có sẵn của Tasks22/26.
