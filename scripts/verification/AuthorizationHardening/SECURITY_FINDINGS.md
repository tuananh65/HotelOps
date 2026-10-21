# Findings — trạng thái sau Task08

## F01 — OPEN / MEDIUM: least privilege SQL chưa được áp dụng

SELECT audit thực tế trên HotelOpsDB vẫn trả 1 cho INSERT/UPDATE/DELETE/ALTER của credential local cũ. Không có ConnectionStrings:AuthorizationReadOnly được cấu hình; không tự tạo hoặc sửa principal/permissions. Least privilege chưa hoàn thành.

Runners trực tiếp hiện đại: DataScope; PermissionService --live; Authorization --live; ResourceAuthorization --live. Trước đây lấy ConnectionStrings:HotelOpsDb từ API User Secrets/env. Nay cả bốn dùng Shared/ReadOnlyVerification.cs: User Secrets ID HotelOps.Authorization.Verification, key ConnectionStrings:AuthorizationReadOnly hoặc env ConnectionStrings__AuthorizationReadOnly; tuyệt đối không fallback API credentials.

Guard kiểm tra DB target HotelOpsDB, AttachDBFilename bị cấm; dùng cùng connection đã preflight để chạy EF. sys.fn_my_permissions kiểm tra server/database/object grants và HAS_PERMS_BY_NAME kiểm tra column UPDATE, từ chối write/DDL/execute/admin permissions; ApplicationIntent=ReadOnly chỉ là hint, không được gọi là enforcement. Context chặn SaveChanges; interceptor chặn nonquery, batch, SELECT INTO, DML/DDL/EXEC và các cú pháp nguy hiểm; raw ADO reads trong runners gọi AssertSelect trước execute. Đây là lớp bảo vệ bổ sung cho principal tối thiểu, không phải SQL parser tổng quát hoặc DB permission replacement. Chỉ chạy SELECT commands được review.

Probe đã đưa credential cũ vào config read-only tạm của process (không lưu/in giá trị), SQL preflight từ chối READ_ONLY_PRIVILEGE_REJECTED trước live assertions. Không dùng cờ bypass để lấy lại kết quả Passed.

Phân loại còn lại:

- CurrentUserAccessor: offline, không SQL.
- Hardening health/policy/guard tests: doubles/offline, không SQL; --readonly-probe chỉ SELECT preflight.
- HttpSqlIntegration executable: writes users/assignments/fixtures/sessions/audit chỉ ở HotelOps_Authorization_Test, target guard mỗi command; credentials ứng dụng riêng. SQL connection hiện vẫn dùng quyền local rộng, chưa được hạn chế bởi SQL principal test-only.
- Prepare-Database: provisioning explicit, master chỉ cho CREATE database test chưa tồn tại với -CreateIfMissing; schema/seed batch bị guard vào test DB. Prepare-Secrets: chỉ local secret store; Verify-Preservation hiện chỉ SELECT test state.
- RbacUpgradeTrial/Deployment: procedural deployment/history, có quyền ghi theo stages; **không phải runners regression** và không được chạy trong Task08. Run-Regressions chỉ orchestrate build/test/live read, sẽ dừng khi setup read-only thiếu. Task08 dùng Run-Validation để ghi riêng BLOCKED.
- Audit quyền/fingerprint một lần trong Task08 sử dụng SELECT của review context; không phải cấp phép runner dùng credential admin hoặc chứng minh read-only principal đã tồn tại.

DBA cần duyệt provisioning draft, principal server không admin, grants tối thiểu, inherited/column/server permissions và kiểm tra lại bằng principal thật. Draft chưa thực thi, positive acceptance dưới principal đó và live SQL assertion còn BLOCKED.

## F02 — REMEDIATED trong public response

HealthController GET api/health/database trước đây trả status/database/message và ex.Message khi exception. Nay healthy chỉ {"status":"ok"} 200, false/exception chỉ {"status":"error"} 503. Không ghi provider details hoặc connection string vào log. Cancellation được truyền xuống CanConnectAsync và abort được propagate.

Anonymous host tests dùng controller production + DatabaseFacade doubles, phủ healthy, unhealthy và exception chứa sentinel metadata; assert JSON chỉ có status, không database/sentinel/redirect. Không phá health monitoring dựa route/status code; external clients parse database/message cần cập nhật. Không có consumer tương ứng trong repository, không thể xác nhận consumer ngoài repo.

## F03 — REMEDIATED dependency; fresh provisioning chưa chạy lại

Setup không còn import deployment helpers hoặc đọc deployment-state.json/main secrets. Integration connection lấy từ Integration User Secrets hoặc env riêng; cấu hình absent/wrong DB sẽ dừng. Expected server canonical được kiểm tra ở SQL và receipt, không hard-code máy DEV1 trong source setup/runtime.

Prerequisite release là SQL Server 2022+, runtime kiểm tra major>=16; actual server 16.0.1000.6 Express, compatibility 160. Existing owned DB xác minh receipt identity/date/marker/release hashes, không seed/reset. Existing DB không receipt dừng. Missing DB cần -CreateIfMissing rõ ràng, Full Schema + canonical seed; partial checkpoint không replay. Không tự adopt/copy credentials của database có dữ liệu lạ.

Source-only isolated copy (không deployment-state/receipt) đã kết nối SQL và từ chối DB có sẵn đúng dự kiến, không ghi. Không tạo DB thứ hai chỉ để chứng minh clean checkout. Fresh create path được review/source-parse và kế thừa release đã kiểm thử, chưa được thực thi mới trong Task08. DEV2 cần instance/test DB riêng được phép hoặc provenance đã được duyệt, credentials local của chính mình.

## F04 — REMEDIATED / LOW

PermissionPolicyProvider nhận optional IHttpContextAccessor (giữ constructor hai argument tương thích); linked RequestAborted + CancelAfter(5s) chỉ quanh catalog lookup. Contract IPermissionPolicyCatalog không đổi; provider vẫn no cache, không fail-open. Tests phủ request đã abort, non-HTTP lookup và catalog bị stall được hủy thực tế bởi budget 5s. Timeout không tự cấp quyền; HTTP có thể trả generic error theo middleware hiện có.

## Rủi ro khác giữ nguyên

- BLOCKER cho live read verification: chưa có approved read-only principal/config; F01 vẫn mở.
- MEDIUM: chưa validate external health consumers; SQL runtime integration principal vẫn quyền rộng dù scope guard; fresh provisioning ở máy DEV2 chưa thực chạy.
- LOW: SELECT whitelist bảo thủ có thể cần điều chỉnh khi query hợp lệ đổi cú pháp; không nới để cho phép arbitrary SQL. Process kill/SQL outage có thể ngăn cleanup finally; cần audit sessions test và không reset tự động.
- Production business API protection, resolver dispatch cho DEV2, SoD, Start/Resume, authorization–write concurrency và Legacy Department Retirement không thuộc task. Không tuyên bố authorization rollout hoàn chỉnh.
