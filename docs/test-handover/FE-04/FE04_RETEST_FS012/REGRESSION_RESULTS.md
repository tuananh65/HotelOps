# Regression FS-012 và giới hạn evidence

Executable C# và browser harness tại d1260425206a953086107eb8de97f845405fc782; checkpoint retest0a4139fa3520577e82c388fab0893b508da9b820 chỉ bổ sung portable hash validation cho18 SQL source files, không đổi runtime sau các lượt kiểm thử. Không ghép focused/component/mocked/failed runs thành một full PASS.

| Kiểm chứng | Kết quả thực tế | Evidence / giới hạn |
| --- | --- | --- |
| `dotnet build HotelOps.slnx` | PASS,0 warnings/0 errors | BACKEND_BUILD.log |
| `dotnet test HotelOps.slnx` | Exit0, solution API-only không chứa xUnit project | SOLUTION_TEST.log; không coi là đã chạy137 tests |
| xUnit project riêng | **137/137 PASS**,0 failed/skipped | evidence/fs012.trx; NU1900 do không lấy được vulnerability metadata NuGet |
| FS012 helper build | PASS,0 errors | HELPER_BUILD_FINAL.log; NU1900 còn tồn tại |
| Frontend production build | PASS | FRONTEND_BUILD.log; inherited React Router use-client và Vite chunks>500KB warnings |
| Node | **46/46 PASS** | NODE_TESTS_FINAL.log; first sandbox attempt fail do Access denied khi esbuild ghi existing .test-output, giữ NODE_TESTS.log |
| Browser interception | **47/47 PASS** | INTERCEPTION.log; mocked API, không gọi đây là live SQL |
| Full live01 | **Không hoàn tất clean**:7 PASS,4 FAIL,1 ca không có receipt | full01_LIVE_RESULTS.json run bb931f6c-d956-4e76-acd1-a1227146e785; không gọi7/12 PASS đầy đủ |
| Full live02 | **12/12 PASS**, độc lập | full02_LIVE_RESULTS.json run32216057-ebdd-42eb-af3a-24e2655966be, LIVE_FULL02.log; real auth/HTTPS/API/SQL,9.4 phút |
| Actual owned process restart | **1/1 PASS** | ACTUAL_RESTART_RESULTS.json; original key/body/ETag, result equal, one publication, no source/notification delta, không reset DB |
| Actual default-off | **1/1 PASS** | defaultoff_DEFAULT_OFF_RESULTS.json; HTTP503, capability UNAVAILABLE, React Save disabled, no mutation |
| SQL write boundary |23 bảo vệ/compatibility outcomes +1 privileged bypass dự kiến | BOUNDARY_ATTEMPT07.json;24 checks, không phải24 universal protections |
| Commit faults |4/4 PASS | COMMIT_FAULT_RESULTS.json; SQL thật + simulated provider acknowledgement/cancellation; gate doubles, không phải mất mạng thật |
| EF physical guard |8 protected behavior outcomes +1 unsupported bypass | PHYSICAL_GUARD_RESULTS.json; privileged raw provider commit không bị EF interception bảo vệ toàn cục |
| SQL proposal constraints |6/6 PASS, test rows rolled back | NOTIFICATION_SQL_RESULTS.json |
| Tester preflight | Read-only schema/fixtures PASS |4 users,27 rooms,3 tasks; không login/deliver/fault/write trên Tester DB trong FS012 |
| Frozen package | PASS30 files,75 unit cases/34 methods,38 functional NOT_RUN | HANDOVER_FREEZE.json;1.686 workbook cells, exact ZIP hash, scoped secret scan |
| Setup portability | Plan-only PASS current workspace + fresh Git archive | PORTABLE_*PLAN.json; normalized LF source identity, historical raw schema markers giữ nguyên; không tạo trial thứ hai |
| Formatting / syntax | PASS | git diff --check; Prettier6 new TS files; JS syntax và PowerShell AST parse; không refactor style module khác |

## Live01 thất bại được giữ nguyên

Report ghi7 PASS/4 FAIL và không có kết quả ca12. Hai helper candidate-deliver/candidate-block-unused-room timeout; timing ghi1.503.834ms và31.620.727ms mặc dù configured child timeout vẫn60.000ms. Hai ca sau nhận503/400 thay vì200. Error contexts gốc được giữ ở evidence/full01-error-context. Lượt này bị gián đoạn kéo dài qua đêm; host suspension/scheduling là suy đoán, chưa có bằng chứng để chốt nguyên nhân gốc hoặc loại bỏ mọi lỗi transport. Không thay timeout để đổi FAIL thành PASS.

Khi tiếp tục,3 owned processes đều đã thoát; stop script xác nhận0 stopped/3 alreadyExited, không kill theo port. Trial không còn fault constraints. Shift synthetic mới được append vì fixture cũ hết thời gian, thêm25 phòng khi thiếu capacity; không chỉnh/reset shift/task/assignment lịch sử và không reseed. Lượt startup đầu cold trả ECONNREFUSED trước readiness rồi tự stop owned services; retry cùng timeout thành công. Có một compile lỗi variable-shadowing khi thêm helper append shift, đã sửa trước khi execute; helper build cuối PASS. Inspector lần đầu false alarm null-array được sửa bằng SQL scalar count=0.

Full02 chạy đủ12 ca mới, không trộn outcomes của live01. Bao gồm UC028 Create/options/cycle/readiness/audit, duplicate/concurrent create/rollback, assignment staging/idempotency/key conflict/concurrency, explicit publication retry/lease recovery, recipient/actor revoke/source-read/inactive/branch/superseded protection, Monitor/Detail React navigation, UC032 PUT100% vẫn IN_PROGRESS/ownership/ETag, anonymous/forbidden/cross-branch/CSRF/invalid input và source persistence. Không reset/reseed trong full02.

“RestartRetry” trong full suite còn có helper exit/durable claim + lease expiry simulation; **actual API/gateway/Vite process restart** được kiểm riêng bằng spec/receipt ACTUAL_RESTART. Không gọi lease expiry là kill worker bất ngờ. Inbox delivery trong live dùng privileged developer connection; permission prototype chỉ kiểm assignment bundle và core UC028/UC032, chưa cấp INSERT inbox/attachments.

## Warnings và chưa xác minh

Giữ rõ NU1900, frontend use-client/chunk warnings, inherited EF20602 room-type model warnings lúc startup, NO_COLOR/FORCE_COLOR và Git CRLF normalization notices. Không sửa unrelated mappings/build settings để giấu chúng. Các lỗi/assertion attempts boundary01–06 được liệt kê riêng ở EXTERNAL_WRITER_TESTS.md và receipts, không xóa để gọi toàn lịch sử PASS.

Chưa xác minh deployed dedicated login bằng authentication thực, full-backend grants, restricted inbox/photo delivery, actual network partition/SQL outage trong recovery, abrupt worker kill, accepted A01/A02 hoặc Tester acceptance. A01/A02 MISSING_EXECUTABLE_SOURCE; UC029 PROVISIONAL_NOT_ACCEPTED; shared/production OFF. Không có measured coverage%, UC credit hoặc full FE04 acceptance.
