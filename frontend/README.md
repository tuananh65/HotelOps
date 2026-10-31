# HotelOps frontend kết nối ASP.NET Core API

Front-end Vite + React 19 + TypeScript cho 80 màn hình HotelOps. Phiên bản này gọi API thay cho lưu dữ liệu nghiệp vụ trong trình duyệt.

**Chưa xác nhận tích hợp API thật.** Hai GET sự cố có controller/DTO đã đọc tại nhánh `feature/BE2-incident`; các API khác là **API đề xuất – cần đối chiếu với back-end**. Xác thực và `GET /api/ui/screens/{screenId}/context` là các hợp đồng đề xuất cần thiết cho bản triển khai này.

```sh
npm ci
```

Sao chép `.env.example` thành `.env.local`. Điền `VITE_API_BASE_URL` và chọn `VITE_AUTH_MODE` theo cơ chế back-end đã thống nhất (`cookie` hoặc `bearer`). Sau đó:

```sh
npm run dev
```

Mở `http://127.0.0.1:5173`; đăng nhập bằng tài khoản do back-end cấp. Không có tài khoản demo hoặc dữ liệu nghiệp vụ tự sinh khi API lỗi.

Tài liệu bàn giao: [docs/api-integration/README.md](docs/api-integration/README.md). Bảng API: [api-mapping.md](docs/api-integration/api-mapping.md). Kiểm kê 80 màn hình: [screen-inventory.md](docs/api-integration/screen-inventory.md). Hợp đồng/DTO: [api-contract.md](docs/api-integration/api-contract.md). Chênh lệch đầu vào: [sources-and-conflicts.md](docs/api-integration/sources-and-conflicts.md).

```sh
npm run typecheck
npm run lint
npm test
npm run build
npx playwright install chromium
npm run test:e2e
```

Các bài test dùng HTTP fixtures theo hợp đồng, không chứng minh back-end thật đã hoạt động. Thư mục build là `dist/`; cấu hình SPA hosting cũ được giữ lại. Không đặt secret vào biến `VITE_*` vì các biến này được đưa vào bundle trình duyệt.
