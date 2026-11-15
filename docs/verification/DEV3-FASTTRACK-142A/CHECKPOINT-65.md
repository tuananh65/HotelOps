# FASTTRACK-142A checkpoint65 — UC112 Submit và bàn giao xác minh

**Hoàn thành mới: 1 UC — UC112 COMPLETE_VERIFIED trong SQL disposable.** Matrix hiện **13 COMPLETE_VERIFIED / 131 PARTIAL / 144 UC**. UC105 Verify/Return vẫn PARTIAL; chưa coi toàn bộ FE11 hoàn thành.

Đã triển khai API/UI Complete task: nhập kết quả tiêu chí, result note, handover note và bằng chứng; Submit chuyển authoritative workflow sang Pending Verification, ghi actual end và đóng work interval, lưu snapshot/history/audit cùng transaction. Mandatory criterion phải có PASS hoặc FAIL; thiếu kết quả trả MSG03 để sửa ngay. Giữ current assignee, branch/department, session/CSRF, ETag và workday guards. Task chưa được coi là Closed.

Workflow trực tiếp đã kiểm chứng: **nhân viên đăng nhập → Complete → sửa tiêu chí thiếu → nhập notes và PDF → Submit → MSG09/Pending Verification → reload; Department Head đăng nhập → inbox → Open task → xem snapshot và tải PDF → reload**. [Receipt cuối](checkpoint65-submission-final.json) có 53 records, gồm 37 submission assertions và 12 browser stages. Cả HK_STAFF, MAINTENANCE_STAFF và STOREKEEPER đều đã submit qua HTTPS/SQL. [Acceptance UC112](UC112_ACCEPTANCE.md) đối chiếu từng tiêu chí và giới hạn.

Notification intent gắn với transaction Submit; delivery kiểm tra lại quyền, có retry/deduplication, và source mở bằng canonical route được server xác định. [Worker thật](checkpoint65-worker-final.json) đã chạy với opt-in trên clone, khôi phục một delivery từ event đã commit và không tạo trùng. Cấu hình mặc định vẫn tắt worker; không triển khai lên môi trường dùng chung.

## Validation trực tiếp

- [Backend build](checkpoint65-backend-build-final.log.txt) và [probe/API build](checkpoint65-probe-build-final.log.txt): PASS, 0 warning/error.
- [Backend regression](checkpoint65-backend-tests-final.log.txt): **588 PASS, 11 SKIP, 0 FAIL**. `dotnet test HotelOps.slnx` exit0 nhưng solution không chứa test project; không tính thành coverage.
- [Focused frontend](checkpoint65-frontend-focused.log.txt): **28 PASS**; [Auth/Router/notification regression](checkpoint65-auth-router.log.txt): **841 PASS**, 0 fail/skip.
- [TypeScript](checkpoint65-typecheck.log.txt): PASS; [production build](checkpoint65-frontend-build.log.txt): PASS, bundle708.06kB. Giữ cảnh báo Vite use-client/chunk>500kB. npm ci/full frontend suite không chạy lại vì dependency/lockfile không đổi.
- SQL install/replay PASS. Chỉ thêm một bảng submission và mở rộng CHECK trên workflow hiện hữu trong disposable; không sửa generated EF, physical task CHECK hoặc permission catalog. [Rollback nguồn](checkpoint65-submission-final-rollback.txt) và [task cleanup](checkpoint65-task-cleanup-final.txt) PASS.
- Hai clone COPY_ONLY có identity riêng đều đã drop cùng backup, không tắt immutable criteria trigger. [Cleanup clone1](checkpoint65-clone1.cleanup.json), [clone2](checkpoint65-clone2.cleanup.json), [source](checkpoint65-source-cleanup-final.txt), [file/host counts](checkpoint65-counts-cleanup.json) xác nhận cleanup. Không mutation trên shared HotelOpsDB.

Runtime còn ba RoomType index warnings và unordered Take. Hai lỗi insert có chủ đích trong fault tests dùng temporary trigger: EF/SQL trả334 do OUTPUT với trigger, chứng minh rollback tại insert; không báo rằng THROW51865 đã chạy. Probe build ban đầu có một warning unused helper, đã sửa và build cuối zero warnings. Không có failed acceptance run của C65; [trial đầu](checkpoint65-submission-trial1.json) và trial mở rộng đều PASS.

## Lifecycle gaps và việc tiếp theo

1. **UC105 Verify/Return**: Department Head kiểm tra snapshot, separation of duties, verification note hoặc return reason; Closed hoặc Accepted, audit/notification, bảo toàn first actual start và lịch sử từng submission.
2. **UC107 Decline**: reason bắt buộc, kết thúc assignment, Pending Assignment và thông báo Department Head; sau đó nối create/assign/accept/start/submit/verify thành demo đầy đủ.
3. **UC006/010 và FE13**: tiếp tục writer coordination và read/lifecycle theo SRS, không bỏ bảo vệ để mở workflow.

Capability hiện chỉ được provision rồi rollback trong disposable. Snapshot giữ giới hạn evidence hiện hữu 100 entries; phân trang toàn bộ evidence history là việc riêng. UC105 chưa có mutation, nên đây là handoff đã chạy thật, chưa phải business cycle FE11 khép kín. Sprint vẫn ACTIVE, deadline không đổi.

TASK_ID=DEV3-FASTTRACK-142A
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_REMOTE_SHA=4e3a874b6b9614e6dc0dc8fad125c32f266497d6
NEWLY_COMPLETED_UCS=UC112
IMPLEMENTED_FUNCTIONALITY=UC112_SUBMIT;PENDING_VERIFICATION_SNAPSHOT;DEPARTMENT_HEAD_NOTIFICATION;HOSTED_WORKER;CANONICAL_HANDOFF
BACKEND_BUILD=PASS_0_WARNINGS_0_ERRORS
FRONTEND_NPM_CI=NOT_RERUN_LOCKFILE_UNCHANGED
FRONTEND_TYPECHECK=PASS
FRONTEND_BUILD=PASS_WITH_KNOWN_VITE_WARNINGS
AUTH_ROUTER_TESTS=841_PASS
SQL_HTTPS_STATUS=OWNED_CLONE_53_RECORDS_PASS;HOSTED_WORKER_PASS;BOTH_CLONES_AND_BACKUPS_REMOVED;SOURCE_GUARDS_RESTORED;HOSTS_STOPPED
CHANGED_FILES=checkpoint65-changed-files.txt
COMMIT_SHAS=logical_delivery_commit_containing_this_receipt
PUSH_STATUS=verify_remote_after_commit_and_report_actual_SHA
