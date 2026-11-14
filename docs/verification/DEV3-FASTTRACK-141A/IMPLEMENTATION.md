# Kết quả triển khai FASTTRACK-141A

## Thay đổi đã triển khai

1. `frontend/src/auth/AuthRouteHost.tsx`: nối Users directory, user access review và branch structure vào host canonical bằng các read boundary có sẵn. `/users`, `/users/:id`, `/hotel` nay có thể truy cập từ production entrypoint; server vẫn quyết định quyền trên từng API.
2. `frontend/tests/shared-auth-host.test.mjs`: mount chính AuthRouteHost canonical, thêm 12 ca chặn business reads ở INITIALIZING/ANONYMOUS/RESTRICTED/ERROR_UNAVAILABLE, navigation Users/Structure, permission denial và thay authority cùng tài khoản không giữ dữ liệu cũ.
3. `frontend/tests/users-router-integration.test.mjs`: các ca thường dùng canonical host; ca kiểm chứng gate OFF dùng reusable SharedAuthHost với flag false rõ ràng. Không bỏ kiểm chứng đường dẫn bị tắt.
4. `frontend/package.json`: `npm test` giới hạn hai test files đồng thời, đặt option trước glob. Node/browser concurrency mặc định gây timeout trong lượt đầu; giữ nguyên timeout assertions và dependency versions.
5. `tests/HotelOps.Dev3Unblock.NoSql/`: sửa expectation của missing authority thành Dev2AccessDenied, kiểm tra inherited controller authorization, thực thi middleware CSRF, cung cấp capability fixture tương thích compiled action contracts cho tám writer-order probes. Các probe dùng DbConnection stub, không SQL thật, không mở release fuse trong production.

Không sửa EF entities/mappings, connection strings, permission records, business status vocabulary hoặc schema. Không merge/cherry-pick lại PR118–122. Không deploy và không merge main/develop.

Source/test paths tạo hoặc sửa:

- frontend/package.json
- frontend/src/auth/AuthRouteHost.tsx
- frontend/tests/shared-auth-host.test.mjs
- frontend/tests/users-router-integration.test.mjs
- tests/HotelOps.Dev3Unblock.NoSql/Program.cs
- tests/HotelOps.Dev3Unblock.NoSql/WriterOrderChecks.cs
- tests/HotelOps.Dev3Unblock.NoSql/WriterAuthorityFixture.cs (mới)
- tests/HotelOps.Dev3Unblock.NoSql/PlacementCsrfChecks.cs (mới)
- tests/HotelOps.Dev3Unblock.NoSql/README.md (mới)

Tài liệu và bằng chứng mới chỉ nằm trong docs/verification/DEV3-FASTTRACK-141A/. CHANGED_FILES.txt ghi đầy đủ từng path của đợt bàn giao. Không có file bị xóa.

## Phân loại lỗi

- **Environment:** sandbox chặn FETCH_HEAD/NuGet/log output; chạy lại với quyền phù hợp thành công. Đây không phải source compile failure.
- **Integration source:** canonical read routes chưa bật; NoSQL harness dựa vào exception/attribute/authority composition cũ; test runner chạy quá nhiều browser đồng thời. Đã sửa trong task này.
- **Test fixture:** permission-denial mock ban đầu thay fetch sau khi client đã capture; synthetic email ban đầu null trái DTO string; đã sửa fixture, giữ decoder chặt. Test gate OFF cũ truyền props vào wrapper canonical mới; đã chuyển đúng reusable host.
- **Business gaps:** FE05 conclusion/reinspection candidate chưa hợp nhất; FE11 generic task và FE13 internal-request workflow thiếu; FE14 dashboard/notification candidates chưa có trong canonical. UC006/010 runtime coordination chưa được chứng minh dù offline harness đã sửa. Chi tiết 141 UC nằm trong UC_STATUS_MATRIX.md.

## Giới hạn kiểm chứng

Backend solution chỉ chứa API project. `dotnet test HotelOps.slnx` trả exit 0 nhưng không discover tests; bằng chứng test thực tế đến từ xUnit projects và executable harness chạy riêng.

Frontend browser tests dùng local synthetic HTTP mocks, không chứng minh deployed API/SQL behavior. NoSQL writer probes chỉ dừng tại câu query User đầu tiên, không chứng minh thứ tự toàn bộ actor/receiver locks, race hay commit. Live SQL/HTTPS được ghi NOT_RUN.

Warnings được giữ nguyên: npm esbuild allowScripts; Vite bỏ qua `use client` của react-router và chunk >500 kB. Backend build: 0 warnings. Git có cảnh báo LF→CRLF theo cấu hình hiện hữu; không thay cấu hình EOL.

## Ba việc code tiếp theo

1. Reconcile FE05 legacy assignment/rework entrypoints với source authority/cycle/evidence và notification participants hiện tại; thêm deny/admission tests trước khi nối conclusion/reinspection workflows.
2. Hoàn thiện UC006/UC010 writer coordination và test actor/receiver lock ordering trên isolated fixture; không lấy first-command probe làm bằng chứng concurrency acceptance.
3. Chốt contract FE11 create/assign task và FE13 request history/list dựa trên UC v3/SRS/schema; triển khai read DTO/service/controller trước các transition chưa có quyết định quyền/trạng thái. FE14 read-only dashboard candidate đã được chỉ rõ để tiếp tục phục hồi source.
