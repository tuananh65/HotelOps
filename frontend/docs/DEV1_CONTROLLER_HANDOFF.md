> Cập nhật: bộ màn hình DEV1 đã được nối trong phiên bản UI. Xem DEV1_UI_HANDOFF.md để biết trạng thái hiện tại. Phần dưới lưu mô tả của gói controller ban đầu.

# Bàn giao controller frontend DEV1 — chỉ thêm file

## Phạm vi và kết luận

- Frontend gốc: `FE+BE/inventory-authz`, commit `1cf8d71692207e01f34b57d1daf5cec3b5b1e40b`.
- Backend đối chiếu: `integration/dev1-fe-handoff`, commit `cf8fb020362f8f64192e7f2f14a10547cebe5b91`.
- Thêm adapter có kiểu dữ liệu theo controller/DTO backend cho module 2–7. Không thay auth, Inventory, service, route, screen hoặc controller hiện có.
- **Đây là gói controller để gọi API, chưa phải gói hoàn tất tích hợp giao diện.** Màn hình cũ chưa import controller mới nên vẫn giữ hành vi cũ. Chỉ chép gói này không tự sửa các lỗi API cũ trên màn hình.
- Không tạo nhánh, commit, push, merge, reset hoặc deploy. Không chỉnh backend/database/configuration. Không đóng gói backend, node_modules, dist hay toàn bộ frontend.
- User Management không thuộc phạm vi yêu cầu lần này.

## File mới

Tất cả đường dẫn dưới đây tính từ thư mục `frontend/`:

| File | Công dụng |
|---|---|
| `src/controllers/dev1/contracts.ts` | DTO native và query của DEV1; không dùng EntityDto giả lập |
| `src/controllers/dev1/transportController.ts` | HTTP dùng session cũ; đọc ETag; kiểm tra đúng tài nguyên/branch trước khi sửa |
| `src/controllers/dev1/hotelStructureController.ts` | Xem cấu trúc; đọc/thêm/sửa tầng và khu vực |
| `src/controllers/dev1/roomTypeController.ts` | List/detail/create/update/deactivate loại phòng |
| `src/controllers/dev1/roomController.ts` | List/detail/history/create/edit/update phòng |
| `src/controllers/dev1/departmentController.ts` | Đọc danh sách từ structure, detail/create/update phòng ban |
| `src/controllers/dev1/checklistController.ts` | Create/detail/update criteria/publish/retire checklist |
| `src/controllers/dev1/housekeepingController.ts` | Planning, phân công, thực thi inspection, ghi defect, yêu cầu rework |
| `src/controllers/dev1/progressController.ts` | Khởi tạo/đọc/lưu tiến độ UC032, UC109; tải evidence |
| `src/controllers/dev1/useDev1QueryController.ts` | Hook tùy chọn loading/error/empty/ready; hủy request và bỏ dữ liệu khi đổi session |
| `src/controllers/dev1/index.ts` | Export riêng, không đăng ký hay thay controller cũ |
| `tests/dev1-controller-entry.ts` | Entry chỉ dùng trong test, dùng chung instance auth với controller |
| `tests/dev1-controller-contract.test.mjs` | 11 ca test contract/security mới |
| `docs/DEV1_CONTROLLER_HANDOFF.md` | Tài liệu này |
| `docs/DEV1_CONTROLLER_MANIFEST.json` | Commit đối chiếu, kiểm tra nguyên trạng, checksum file trong gói |

Các module mới đều phụ thuộc `src/api/http.ts` và `src/services.ts` hiện hữu; không có login/logout/token store mới. Hai file test và tài liệu không chạy trong production.

## API đã ánh xạ

`b` là branchId; `id` là ID thật, không phải mã hiển thị.

| Module | Routes và thao tác |
|---|---|
| Hotel Structure | GET `/api/branches/{b}/structure`; POST `/floors`, `/areas` dưới branch; GET/PUT `/floors/{id}`, `/areas/{id}` dưới branch |
| Room Type | GET/POST `/api/room-types`; GET/PUT `/api/room-types/{id}?branchId=b`; POST `/api/room-types/{id}/deactivate?branchId=b` |
| Room | GET `/api/rooms` với `branchId` hoặc `allBranches=true`; POST `/api/rooms`; GET detail/history/edit và PUT `/api/rooms/{id}?branchId=b` |
| Department | GET structure với `collection=departments`; POST `/api/branches/{b}/departments`; GET/PUT `/api/branches/{b}/departments/{id}` |
| Checklist | POST `/api/branches/{b}/inspection-checklists`; GET/PUT `/{id}`; POST `/{id}/publish`, `/{id}/retire` |
| Housekeeping | GET cleaning-task assignment-options/inspection-options; POST assign/inspections; GET inspection execution; POST start/finish/defects/reworks; PUT results, tất cả dưới `/api/branches/{b}` |
| UC032/UC109 | GET/PUT `/api/branches/{b}/cleaning-tasks/{id}/progress` hoặc `/operational-tasks/{id}/progress`; POST progress-initialization; GET progress/evidence/{attachment}; POST room-types/{id}/cleaning-items |

Đọc các file controller để xem chữ ký TypeScript và request body đầy đủ. Không có request âm thầm gửi tới API mock hoặc fallback.

## Xung đột đã ghi nhận — chưa sửa file cũ

| File/luồng hiện hữu | Xung đột với DEV1 | Cách xử lý khi được phép nối giao diện |
|---|---|---|
| `src/api/endpoints.ts`, `src/api/modules.ts` | `/api/locations`, `/api/departments`, `/api/cleaning-tasks`, `/api/inspection-checklists` không phải routes branch-scoped của DEV1 | Cho các screen DEV1 gọi adapter mới; không thay endpoint dùng chung cho Inventory |
| `src/services.ts: loadScreen`, `src/controllers/useApiScreen.ts` | Cần screen context, `snapshot`, `EntityDto`, `allowedActions`; DEV1 trả DTO khác | Tạo state/view adapter riêng theo DTO thật; không bịa quyền hoặc snapshot |
| `src/api/http.ts` | `request()` chỉ trả body, làm mất ETag | Giữ nguyên; `transportController` dùng GET có cookie để lấy ETag, mọi write vẫn đi qua request() cũ và CSRF cũ |
| Room Type/Room/Department screens hiện có | Form chung có thể gửi trường không có trong DTO và thiếu branch/If-Match | Form mới phải ánh xạ đúng trường rồi gọi controller riêng |
| `RoomTypeDetailScreen` | Backend update không nhận standardCapacity; code tùy chọn; không vô hiệu hóa khi có phòng active | Không cung cấp nút sửa capacity qua update; hiển thị lỗi 409 từ backend |
| Hotel Structure | UpdateFloor không nhận floorNumber | Không gửi floorNumber trong update; vẫn cho phép khi create |
| Department | UpdateDepartment không nhận code | Không gửi code khi update |
| Checklist list/detail screens | Backend không có endpoint list-all checklist; criteria/version khác model cũ | Chỉ mở bằng ID thật hoặc nguồn ID được backend hỗ trợ; không tạo danh sách giả |
| Cleaning/Inspection screens | Generic actions cũ không đồng nghĩa với workflow DEV1 | Ánh xạ từng action; finish inspection không phải approve; rework có state riêng |
| Menu và route authorization hiện có | Session adapter không cấp đủ branch/role/grant để suy ra quyền cho UI DEV1 | Không tự cấp quyền, không bỏ route guard; cần quyết định contract UI authorization trước khi mở các màn hình |

Không có trùng tên file/controller mới với frontend gốc: namespace thư mục `controllers/dev1` riêng. Tuy nhiên xung đột contract trên vẫn tồn tại nếu screen tiếp tục dùng service cũ.

## Authentication, ETag và xử lý lỗi

- Cần cấu hình hiện hữu `VITE_AUTH_MODE=cookie`, HTTPS và session đăng nhập hợp lệ, không ở trạng thái bắt buộc đổi mật khẩu.
- Dùng proxy cùng origin hiện hữu là phù hợp nhất. Không tự chỉnh `.env`, Vite hoặc CORS backend. Nếu cấu hình cross-origin, browser phải đọc được ETag qua CORS; thiếu ETag sẽ chặn write với 428.
- Đọc detail/edit trước khi sửa. Giữ nguyên `Versioned<T>` gồm `data`, `etag`, `resource`, rồi đưa vào update. Không dựng ETag từ rowVersion, không tự đọc phiên bản mới trước write để bỏ qua xung đột.
- Sau write thành công, đọc lại detail để lấy ETag mới. Controller không tự retry write vì request có thể đã được ghi vào database.
- Snapshot của branch/resource khác bị chặn 412 trước khi gửi. Thiếu hoặc weak ETag bị chặn 428.
- Write dùng cookie và CSRF từ HTTP cũ. 401 từ HTTP cũ giữ nguyên xử lý auth hiện hữu. GET riêng lấy ETag trả ApiError 401 cho screen xử lý, không tự sửa global auth handler; screen phải hiển thị đăng nhập lại, không tiếp tục sửa.
- 400/422: hiển thị validation. 403: không đủ quyền. 404: tài nguyên không có/không hiển thị. 409: xung đột nghiệp vụ. 412: dữ liệu đã đổi, yêu cầu người dùng tải lại và xem trước khi gửi. 428: thiếu ETag. 503: kiểm tra backend/schema. Không biến lỗi thành danh sách rỗng.
- `ApiError.outcomeUnknown=true`: chưa biết write đã thành công hay chưa; đối chiếu dữ liệu trước khi gửi lại.
- ID đường dẫn phải là số nguyên dương trong giới hạn safe integer JS. Không hỗ trợ đầy đủ mọi SQL bigint ngoài giới hạn đó; không ép số bị làm tròn thành ID hợp lệ.
- Gói này không khai báo rằng authorization đã được kiểm chứng end-to-end. Backend vẫn là nơi quyết định quyền và data scope.

## Cách dùng trong màn hình tương lai

Ví dụ dưới đây là cách gọi controller, chưa được chèn vào screen cũ:

```ts
import { hotelStructureController } from "./controllers/dev1";

const original = await hotelStructureController.floor(branchId, floorId);
await hotelStructureController.updateFloor(branchId, floorId, original, {
  code: "F02",
  name: "Tầng 2",
  reason: "Điều chỉnh tên tầng",
});
const refreshed = await hotelStructureController.floor(branchId, floorId);
```

Với `useDev1QueryController`, memoize `load` và `isEmpty` bằng `useCallback`, key phải chứa branch và ID/filter. Ví dụ isEmpty cho room list là `result => result.items.length === 0`. Không dùng thiếu dữ liệu hoặc lỗi API làm điều kiện empty. Giữ notification/state gốc backend, không hiển thị “đã gửi thông báo” khi `notification=NOT_INTEGRATED`.

## Giới hạn backend chưa tự xử lý

- Không có list-all checklist trong controller đã đọc.
- Không tự tạo API tạo/start/pause/resume/complete cleaning task nếu backend chưa có route tương ứng. Controller chỉ cung cấp các operation thực sự tồn tại.
- UC032 chỉ lưu trong trạng thái backend cho phép và đúng assignee; UC109 hỗ trợ các trạng thái được backend cho phép. 100% progress không tự hoàn thành nghiệp vụ.
- Finish inspection chuyển sang chờ kết luận, không mặc nhiên phê duyệt.
- Các workflow trả `NOT_INTEGRATED` ở notification vẫn giữ nguyên, không bịa thành công.
- Database package của checklist/housekeeping/progress có guard cho disposable canonical trial; package progress từ chối HotelOpsDB/shared/production. Không sửa guard, không tự chạy migration hoặc ALTER TABLE. Database `HotelOpsDB` từng dùng để test Inventory không mặc nhiên đủ schema DEV1.
- Phiên bản backend đang chạy trên máy cần đúng nhánh/commit DEV1. Chạy backend Inventory cũ không chứng minh controller DEV1 hoạt động.

## Cách chép và kiểm thử local

1. Giải nén ZIP vào thư mục tạm. Xem manifest và tài liệu này trước.
2. Trong source frontend gốc đã có `frontend/src/api/http.ts`, `frontend/src/services.ts`, chép đúng các file mới, giữ nguyên cấu trúc `frontend/...`. Nếu một đường dẫn đã tồn tại trên máy bạn, dừng để đối chiếu, không chọn Replace all.
3. Không chép backend từ nhánh khác vào workspace trong bước này; không chạy Git merge/checkout/push.
4. Mở CMD:

```bat
cd /d D:\HotelOps-WebTest\frontend
npm ci
npm run build
node --test tests/dev1-controller-contract.test.mjs tests/auth-be1-contract.test.mjs tests/http.test.mjs tests/inventory-contract.test.mjs
```

5. Để xem giao diện cũ vẫn chạy: dùng lệnh dev hiện hữu `npm run dev`. Các controller mới chưa nối screen nên không tự xuất hiện thêm nút/trang mới. Không dùng việc trang cũ mở được làm chứng cứ DEV1 live PASS.
6. Live test sau khi screen được nối: đăng nhập thật, chọn branch có quyền, gọi GET lấy ETag, gửi sửa với If-Match và CSRF; xem Network/response/database. Test thêm tài khoản không quyền (403), logout (401), dùng ETag cũ (412), và tải lại kiểm tra dữ liệu đã lưu.

## Kết quả thực sự đã chạy

| Kiểm tra | Kết quả |
|---|---|
| TypeScript + Vite production build | PASS; có cảnh báo bundle JS >500 kB, không phải build failure |
| 11 test DEV1 contract mới | PASS; fetch giả lập để kiểm tra URL/method/body/cookie/CSRF/ETag/session/error |
| 28 test auth/http/inventory hiện hữu được chọn | PASS |
| Tổng test đã chạy | 39 PASS, 0 FAIL |
| So sánh 306 file frontend gốc theo byte với commit FE | PASS; không file cũ nào thay đổi |
| So sánh 347 file src/HotelOps.Api với commit BE | PASS; không file nào thay đổi |
| Live backend DEV1 + SQL Server | NOT TESTED; môi trường này không có dotnet/SQL Server và database trial tương ứng |
| Trình duyệt end-to-end cho UI module mới | NOT TESTED; chưa được nối screen cũ theo giới hạn phạm vi |
| Toàn bộ npm test, Playwright và runtime React hook | NOT TESTED trong đợt này; không coi 39 test chọn lọc là toàn bộ test suite |

Các mock test không xác nhận quyền thực tế, dữ liệu thực tế hoặc migration SQL. Gói này chỉ hoàn tất lớp controller mới trong phạm vi được phép.
