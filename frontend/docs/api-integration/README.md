# Bàn giao tích hợp API cho HotelOps

Bản sửa này thay lớp dữ liệu nghiệp vụ trong trình duyệt bằng lời gọi HTTP cho toàn bộ 80 màn hình hiện có. Giao diện React, bố cục, CSS, routing và các thành phần dùng chung được giữ lại. Back-end, SQL và nội dung Business Rules/System Messages gốc không bị sửa.

**Đây là triển khai front-end theo hợp đồng API đề xuất, chưa phải xác nhận kết nối được với back-end thật.** Hai API đọc sự cố có controller/DTO thật trong repository. Những API còn lại, bao gồm xác thực và context của màn hình, cần nhóm back-end đối chiếu và cung cấp.

## Đầu vào đã đọc

| Nguồn | Phiên bản và kết quả kiểm tra |
|---|---|
| `feature/react-frontend` | Commit `9d220a94509e650464406a9b83099d24cb568220`: Vite, React 19, TypeScript 5.7, React Router 7, 80 screen components, `services.ts`, schema biểu mẫu, routing và tests |
| `feature/BE2-incident` | Commit `c6fcb6d00289c4d0ccbb20c16031b1d7ed1a5ccd`: `IncidentsController`, ba DTO sự cố, service, Program và cấu trúc EF |
| SRS đính kèm | `Report-3_Software Requirement Specification (1)(1).docx`: phần II, III và V; đã đọc cả các bảng trong content controls, tổng cộng 224 bảng XML; 144 UC, 165 GBR, 35 message, 27 nhóm trạng thái và 106 dòng chuyển trạng thái |
| SQL đính kèm | `HotelOpsDB_SQLServer.sql`: 72 bảng, 201 khóa ngoại, kiểu cột, PK, unique index và CHECK; kết quả cấu trúc trong `database-schema-audit.json` |
| Hướng dẫn đính kèm | `Sửa code frontend.docx`: hướng dẫn kết nối tổng quát; URL và `RoomsController` trong file là ví dụ, không phải controller đã triển khai |

Không nhận được bản Swagger/OpenAPI đã chạy, thông tin xác thực được chốt hoặc URL API đang hoạt động. Có cơ chế `AddOpenApi` trong source back-end, nhưng chưa có tài liệu OpenAPI được xuất từ một server thật để kiểm tra.

## Hiện trạng kiến trúc và thay đổi

Trước khi sửa, `src/services.ts` tự tạo database giả, ghi vào `hotelops-public-prototype-v1`, tự chọn người dùng đầu tiên và trả `true` cho quyền/phạm vi. File này còn tự chuyển trạng thái phòng và tính biến động kho. Các form đang có giá trị demo và một số thông báo thành công xuất hiện ngay sau lời gọi đồng bộ.

Sau khi sửa, `src/services.ts` là facade cho các màn hình cũ: cache dữ liệu đã được API trả về, điều phối request, quyền hiển thị và cập nhật dữ liệu sau thao tác. `src/api/` chứa hợp đồng, transport, adapter và service theo resource. `useApiScreen` cùng các gate thực hiện tải/cancel dữ liệu cho từng route. Không thêm package vào dependencies hoặc devDependencies.

Phân quyền sử dụng grants do server trả về theo UC và cặp phạm vi chi nhánh/bộ phận. Action còn phải nằm trong `allowedActions` của bản ghi. Tên vai trò phục vụ hiển thị và lựa chọn nhân viên; trình duyệt không tự suy diễn toàn bộ quyền từ tên vai trò. Bản ghi sửa phải có `rowVersion` server trả về. Mã trạng thái và nhãn UI được tách trong adapter; `AVAILABLE`/`DIRTY` không bị tự đổi thành `READY`.

Các API tạo, phân công, bắt đầu, gửi kiểm tra, duyệt, từ chối, xuất, nhận và đảo chứng từ có endpoint riêng. Không gửi `Action.to` thành một lệnh cập nhật `status`. Sau khi ghi thành công, client lấy kết quả/GET bản ghi và tải lại context. Nếu ghi đã thành công nhưng refresh liên quan thất bại, UI thông báo tình trạng refresh; không yêu cầu gửi lại giao dịch vừa lưu.

Form chờ request, chặn gửi trùng, giữ giá trị khi validation thất bại, ánh xạ `ValidationProblemDetails.errors` về trường nhập liệu. Mất mạng/timeout khi ghi hoặc HTTP 202 được coi là kết quả chưa xác nhận và chặn submit lại cho tới khi tải lại. HTTP 401/403/404/409/412/422/5xx được phân biệt. Message code đã biết dùng nội dung SRS được sao nguyên văn trong `src/api/system-messages.json`, với tham số do server cung cấp.

Upload gửi multipart tới API và giữ attachment ID/URL server trả về; không nhúng base64 vào hồ sơ. Báo cáo và export sử dụng snapshot do server tạo. Không còn tạo audit, thông báo, số dư kho hay phòng Ready trong trình duyệt. Theme, ngôn ngữ và bộ lọc chi nhánh vẫn có thể được lưu để phục vụ giao diện.

## Bảng ánh xạ và hợp đồng

- `screen-inventory.md` và `.json`: đủ 80 màn hình, route, UC, API tải dữ liệu và trạng thái thực hiện.
- `api-mapping.md`: bảng Module | Màn hình/thao tác | UC | Hàm | Method | Endpoint | Request | Response | Quyền | Nguồn; gồm cả các thao tác bổ sung ngoài resource CRUD.
- `api-contract.md`: hợp đồng transport, session, context, phân trang, DTO, upload, command và export.
- `api-dto-fields.json`: tên trường JSON được đề xuất theo module, tách khỏi tên cột SQL và tên trường biểu mẫu.
- `contract-examples.json`: dữ liệu ví dụ có cấu trúc, không phải tài khoản hoặc ID thật của hệ thống.
- `sources-and-conflicts.md`: vị trí và ảnh hưởng của các mâu thuẫn giữa source, SRS và SQL.
- `use-case-specs.json`: phần II được trích xuất đầy đủ để đối chiếu; không đưa toàn bộ văn bản này vào bundle trình duyệt.

## File đã tạo hoặc sửa

| File hoặc nhóm file | Mục đích |
|---|---|
| `.env.example` | Base URL, lựa chọn cookie/bearer chưa được chốt và tên CSRF header |
| `src/api/config.ts`, `http.ts` | HTTP client dùng chung: query, JSON, multipart, body rỗng/204, Blob, abort, timeout, ProblemDetails và bỏ kết quả của phiên cũ |
| `src/api/types.ts`, `endpoints.ts`, `system-messages.json` | DTO có kiểu, resource routes, 103 command contract riêng và message SRS |
| `src/api/adapters.ts`, `modules.ts` | Chuyển DTO sang model UI, DTO sự cố hiện có, ID BIGINT an toàn, mốc giờ UTC+7, các kiểu số lượng riêng cho từng thao tác và tải đầy đủ phân trang |
| `src/services.ts`, `services/readModels.ts` | Facade API/cache; session, grants, điều phối ghi và làm mới dữ liệu; bỏ engine nghiệp vụ mock |
| `src/controllers/useApiScreen.ts`, `components/ApiGate.tsx` | Loading/error/retry, bảo vệ route, đổi phạm vi và hủy request cũ |
| `src/main.tsx`, `components/ScreenBoundary.tsx`, `routes/ScreenIndex.tsx` | Áp dụng cơ chế chung cho mọi route; bỏ chế độ public tự cấp quyền và thông tin demo gây hiểu nhầm |
| `src/ui.tsx`, `domain.ts`, `catalog.json` | Form async, khóa submit, lỗi từng trường, capability của action, chọn ID danh mục, thông tin người dùng thật và đồng bộ 4 UC đọc danh sách còn thiếu |
| Các màn hình auth, phân công, tạo task/incident/rework, reinspection, time correction, workday và generate lịch | Chờ kết quả server, xử lý lỗi trước khi đóng form/chuyển trang hoặc báo thành công |
| Dashboard, màn tồn kho, báo cáo và `report-engine.ts`, `report-export.ts`, `evidence.ts` | Snapshot/KPI/số dư/file/attachment từ API; dữ liệu thiếu hiển thị N/A |
| `playwright.config.ts`, `tests/` | Thay các assertions prototype công khai bằng tests API/permission và HTTP fixtures; cấu hình browser không phụ thuộc đường dẫn Edge trên Windows |
| `README.md`, `docs/api-integration/` | Cấu hình chạy, bảng đối chiếu, hợp đồng và bàn giao |

Danh sách chính xác từng file nằm trong `changed-files.md` và patch. Những màn hình không cần sửa JSX nhận API qua facade và gate chung, không còn nhận dữ liệu mock. Bộ `src/mocks/` cũ chưa bị xóa để tránh mất tài liệu prototype; không có import runtime từ ứng dụng API sang các file đó và không có cơ chế fallback. Dữ liệu HTTP giả lập chỉ ở `tests/fixtures/`.

## Cấu hình và chạy

Tại thư mục `frontend`, chạy `npm ci`, sao chép `.env.example` thành `.env.local`, rồi điền base URL thực tế. Giá trị `https://localhost:7019` trong ví dụ lấy từ launchSettings của repository, không xác nhận API đã chạy ở máy người dùng.

Chọn `VITE_AUTH_MODE=cookie` **nếu nhóm back-end xác nhận dùng cookie**, hoặc `bearer` nếu nhóm xác nhận bearer. Giá trị mặc định `unconfigured` cố ý yêu cầu thống nhất cơ chế trước khi đăng nhập. Không có refresh-token endpoint được tự giả định. Với bearer, token chỉ ở bộ nhớ: tải lại trang cần đăng nhập lại. Với cookie, GET session khôi phục phiên và nhận CSRF token theo hợp đồng; hoạt động người dùng kiểm tra lại sliding session có giới hạn tần suất.

Chạy `npm run dev`, mở `http://127.0.0.1:5173`, đăng nhập bằng tài khoản thật được back-end cấp. Dùng cùng một hostname ổn định để tránh nhầm `localhost` với `127.0.0.1` khi cấu hình cookie/CORS. Backend cần tin cậy HTTPS development certificate trên máy chạy trình duyệt. Front-end không tắt xác minh chứng chỉ.

Kiểm tra local: `npm run typecheck`, `npm run lint`, `npm test`, `npm run build`. Kiểm tra UI bằng `npx playwright install chromium` rồi `npm run test:e2e`; có thể đặt `PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH` nếu máy đã có Chromium riêng. Các bài browser test tự cấu hình auth cookie và intercept HTTP, không dùng API thật.

## Kết quả và phần chưa xác nhận

Xem `verification.md` để phân biệt kiểm tra source/build, fixtures và API thật. Mọi màn hình trong phạm vi source đã được nối vào luồng tải API; những UC chưa có thao tác trong UI gốc không được tự biến thành các màn hình mới. Hai tiện ích Screen Index/Design System nằm ngoài 80 màn hình SRS và yêu cầu phiên hợp lệ.

Những phần còn phụ thuộc nhóm back-end là triển khai/chốt endpoint và DTO đề xuất, context đọc của từng màn hình, xác thực, concurrency, CORS, mapping trạng thái và xử lý các thiếu hụt schema được ghi riêng. Không có back-end hoặc database được sửa trong bản bàn giao. Không commit, push hoặc merge tự động; patch áp dụng trên nhánh `feature/react-frontend` tại commit nền nêu trên.

## Thông tin nhóm back-end cần xác nhận

| Nội dung | Cần chốt |
|---|---|
| URL | Origin/prefix local và môi trường triển khai; HTTP/HTTPS và URL OpenAPI thực |
| Endpoint | Đối chiếu 201 dòng mapping; xác nhận aggregate context, lookup, history và các command riêng |
| DTO | Wrapper phân trang, EntityDto/attributes, user provisioning, tên trường, nullability, enum, ID không trùng loại nguồn và chuỗi cho BIGINT |
| Auth | Cookie hay bearer, SessionDto, hết hạn/sliding, restricted session khi mật khẩu tạm; không giả định JWT/refresh |
| Quyền | UC grants theo branch/department, ownOnly, canEdit, allowedActions và quyền work_interval.correct |
| CORS/CSRF | Origin chạy front-end, credentials, Authorization, header CSRF và cookie flags |
| Concurrency | Token rowVersion/ETag hoặc cơ chế khác mà back-end thực sự chọn; format request/response và mã conflict |
| Lỗi | ProblemDetails/errors, code/parameters của message SRS, rollback xác nhận và kết quả chưa biết |
| Kho/phòng | Atomic transaction, chống xử lý lặp, chuyển trạng thái trên server, số dư snapshot và kiểm tra độc lập người duyệt |
| File | Upload staging khi entity chưa có ID, attachment binding, protected/signed URL, định dạng và giới hạn |
| Báo cáo | SnapshotId gắn quyền/filter, retention, export cùng snapshot, row limit và audit export |
| Schema lệch SRS | Các điểm trong sources-and-conflicts.md; nhóm quyết định phương án backend/database, front-end không sửa thay |

Các tài liệu prototype cũ trong `docs/` được giữ làm lịch sử thiết kế. Nội dung về public demo/local database trong các báo cáo cũ đã được thay thế bởi bản bàn giao này.

## Áp dụng bản sửa

Patch được tạo với đường dẫn tương đối từ gốc repository, có cả file mới và file sửa. Áp dụng trên nhánh front-end có commit nền `9d220a94509e650464406a9b83099d24cb568220`:

```sh
git switch feature/react-frontend
git apply --check HotelOps_frontend_API.patch
git apply HotelOps_frontend_API.patch
cd frontend
npm ci
```

Sau đó cấu hình `.env.local` như hướng dẫn. `git apply --check` giúp phát hiện nhánh hoặc file đã khác nền trước khi áp dụng; không có thao tác force/commit/push trong bản bàn giao. File ZIP chứa thư mục `frontend/` hoàn chỉnh cùng tài liệu, không bao gồm node_modules hoặc build cache.
