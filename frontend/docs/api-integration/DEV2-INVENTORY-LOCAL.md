# Frontend fron-end → DEV2 inventory: hướng dẫn chạy local

## Phạm vi bản bàn giao

- Frontend: nhánh `fron-end`, commit `7ef64727290389edb4788bcf96c0a1e18ac39bb0`.
- Backend tham chiếu: `feature/DEV2-inventory-authz-clean`, commit `a2e6d68924ddc47ba855aeac90c8959b713f175e`.
- Chỉ tích hợp phần ĐỌC của 11 màn inventory (51–61): kho, vật tư, đơn vị tính, cảnh báo tồn, chứng từ, kiểm kê và sổ biến động.
- Chưa tích hợp tạo/sửa/post/reverse, nhập số kiểm kê, chuyển kho; các nút ghi vẫn bị khóa. Các module ngoài inventory không được mở thêm.
- Không sửa backend, database, cấu hình backend, package dependencies, login/logout/password/CSRF transport hoặc session adapter.
- Không tạo nhánh, checkout, commit, push, merge hoặc PR.

## Áp dụng file

ZIP chỉ chứa 10 file mới/thay đổi, có tiền tố thư mục `frontend/`. Không chứa backend, node_modules, dist hoặc toàn bộ dự án.

Dùng source frontend của `fron-end`. Trước khi chép, sao lưu các file cùng tên hoặc so sánh nếu máy bạn có chỉnh sửa riêng. Chép đúng đường dẫn; không xóa các file khác.

Danh sách:

1. `frontend/src/controllers/inventoryController.ts` — mới: gọi API, phân trang và chuyển DTO sang dữ liệu UI.
2. `frontend/src/services.ts` — đưa controller vào luồng loadScreen, xóa dữ liệu cũ trước khi reload; quản lý các bản ghi vừa được backend cho đọc.
3. `frontend/src/components/ApiGate.tsx` — mở màn đọc inventory cho phiên không hạn chế; giữ chặn người chưa login và buộc đổi mật khẩu; trạng thái loading/error có sẵn tiếp tục được dùng.
4. `frontend/src/ui.tsx` — menu dùng cùng điều kiện mở màn như route.
5. `frontend/src/screens/inventory/UnitofMeasurementScreen.tsx` — không mở form sửa khi không có quyền/chưa hỗ trợ sửa.
6. `frontend/src/screens/inventory/WarehouseListScreen.tsx` — thay các chỉ số tên chi nhánh cố định bằng số chi nhánh và kho ngừng hoạt động.
7. `frontend/src/screens/inventory/ItemDetailScreen.tsx` — không hiển thị NaN hoặc tổng giả khi thiếu số tồn.
8. `frontend/src/screens/inventory/StocktakeDetailScreen.tsx` — số chưa đếm và chênh lệch chưa xác định hiển thị N/A, không coi là 0.
9. `frontend/tests/inventory-contract.test.mjs` — mới: 11 test inventory/auth regression dùng mock theo contract thật.
10. `frontend/docs/api-integration/DEV2-INVENTORY-LOCAL.md` — tài liệu này.

## API và quyền

Controller gọi GET dưới `/api/inventory/`: `items`, `item-categories`, `units`, `warehouses`, `stock`, `documents`, `stocktakes`, `movements` và endpoint chi tiết tương ứng.

Không gọi `/api/ui/screens/{id}/context` hoặc `/api/dashboards/operations` cho các màn inventory. Không yêu cầu snapshot token vì backend không trả token này.

Session backend không có grants/branches. Bản sửa không tự tạo grants, roles hoặc quyền ghi. Menu inventory hiển thị để thử đọc; API backend quyết định quyền và phạm vi. Người thiếu quyền nhận 403. Chỉ những object do controller vừa tải thành công mới được hiển thị theo phạm vi backend; không bỏ kiểm tra scope toàn hệ thống.

401 xóa phiên và cache; 403 giữ phiên nhưng không giữ dữ liệu kho cũ. Restricted session vẫn phải đổi mật khẩu trước. Response về muộn sau logout không được nạp lại cache.

Giới hạn dữ liệu: tên chi nhánh chưa được API trả nên hiển thị `#ID`; ngưỡng tồn là theo kho, không giả lập một mức chung cho vật tư; cặp tồn không có trong response và số dư lịch sử không có từ API hiển thị N/A. Phân trang kiểm tra thay đổi tổng số, trùng ID và trang thiếu nhưng không thể bảo đảm snapshot nguyên tử khi backend không hỗ trợ.

## Chuẩn bị

Node.js 22+; .NET SDK theo `global.json` backend (10.0.402, rollForward latestFeature); SQL Server và database tương thích; tài khoản test hợp lệ với quyền INVENTORY_VIEW và scope phù hợp.

Backend có kiểm tra schema trước khi mở cổng. Database sai schema sẽ làm startup thất bại. Tham khảo `scripts/database/release/README.md` và `scripts/verification/StartupSchemaGate/SCHEMA_CONTRACT.md` trên đúng nhánh backend. Không chạy script cài mới lên DB đang có. Không có mật khẩu test chung trong gói này.

## Chạy backend (PowerShell tại thư mục gốc backend)

```powershell
dotnet --version
dotnet restore HotelOps.slnx
dotnet dev-certs https --trust
dotnet user-secrets set "ConnectionStrings:HotelOpsDb" "Server=localhost;Database=HotelOpsDB;Trusted_Connection=True;TrustServerCertificate=True" --project src/HotelOps.Api/HotelOps.Api.csproj
dotnet build HotelOps.slnx
dotnet run --project src/HotelOps.Api/HotelOps.Api.csproj --launch-profile https
```

Thay SQL server/database bằng thông tin máy bạn. User Secrets nằm ngoài source backend. Không cần sửa appsettings.json.

## Chạy frontend (tại thư mục frontend)

```powershell
npm ci
npm run build
node --test tests/inventory-contract.test.mjs tests/auth-be1-contract.test.mjs tests/http.test.mjs
npm run dev
```

Cấu hình frontend hiện có phải là:

```dotenv
VITE_API_BASE_URL=
VITE_AUTH_MODE=cookie
VITE_CSRF_HEADER=X-CSRF-TOKEN
```

Vite HTTPS proxy hiện có chuyển `/api` tới `https://localhost:7019`. Mở `https://localhost:5173/login`, đăng nhập rồi chọn menu kho/vật tư. Nếu trình duyệt hỏi chứng chỉ local, chỉ chấp nhận chứng chỉ development trên máy bạn.

## Kiểm tra

DevTools → Network:

- GET `/api/auth/csrf` → 200; POST `/api/auth/login` → 200.
- Cookie do trình duyệt quản lý. Request ghi auth gửi X-CSRF-TOKEN, không thêm Bearer token.
- Refresh → GET `/api/auth/session` → 200.
- Mở kho/vật tư → GET `/api/inventory/...` → 200, dữ liệu khớp response; bảng rỗng khi API trả rỗng.
- Tài khoản thiếu quyền → 403, không hiển thị lại dữ liệu cũ.
- Đăng xuất → POST `/api/auth/logout` → 204; mở lại màn kho phải về login.
- Kiểm tra phiên buộc đổi mật khẩu chuyển đúng trang đổi mật khẩu.

OpenAPI Development: `https://localhost:7019/openapi/v1.json` (có thể nhập vào Postman). Không mặc định có Swagger UI `/swagger`.

Lỗi thường gặp: CORS → kiểm tra đang dùng proxy và base URL trống; 401 → kiểm tra phiên/cookie; 403 → xem CSRF, restricted session, quyền và scope; 404 → kiểm tra URL/ID; proxy 500 ECONNREFUSED → backend chưa nghe cổng 7019; backend 500 → đọc log backend/DB, không sửa frontend để che lỗi.

## Kết quả kiểm thử trong workspace

- PASS: frontend TypeScript + production build; cảnh báo bundle lớn hơn 500 KB vẫn còn.
- PASS: 28/28 test inventory + auth contract + HTTP (mock, không phải SQL end-to-end).
- FAIL có sẵn: toàn bộ npm test có 48 test, 37 đạt và 11 workflow test lỗi ở setup login. Fixture workflow cũ thiếu route CSRF và dùng session DTO cũ. Không sửa fixture để giả lập quyền backend chưa cung cấp.
- NOT TESTED: backend startup, đăng nhập thật, quyền thật, UI trình duyệt và SQL end-to-end. Môi trường không có dotnet/database/tài khoản test.

Đây là bản triển khai phần đọc inventory, chưa phải tích hợp đầy đủ HotelOps. Test mock không chứng minh kết nối SQL thật thành công.

## Hosting

Các lệnh trên chỉ chạy local. Vite proxy không tồn tại trong dist khi deploy. Hosting cần HTTPS, reverse proxy `/api` tới backend và cấu hình môi trường riêng được duyệt. Chưa deploy/công khai hệ thống.
