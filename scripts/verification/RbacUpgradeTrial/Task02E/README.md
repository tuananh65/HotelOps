# Task 02E — Controlled Fixture Reset Manifest

Ngày 21/10/2026, branch `feature/BE1-authorization`.

**Manifest đã hoàn tất để review; RESET BLOCKED, chưa có phê duyệt thay đổi bất kỳ row nào.** Phương án B được chọn về định hướng, không phải quyền xóa/thay fixture cụ thể. Task02E chỉ SELECT và tạo tài liệu; không thay đổi nguồn/trial, không backup/restore/reset, không tạo script phá hủy.

## Hồ sơ cần đọc

1. [FIXTURE_RESET_MANIFEST.md](FIXTURE_RESET_MANIFEST.md): nguồn gốc, PK, các hành động đang đề xuất và dependency/approval profiles.
2. [FIXTURE_RESET_MANIFEST.json](FIXTURE_RESET_MANIFEST.json): 371 row có actual PK, projection không nhạy cảm, expected hash và trạng thái review riêng.
3. [EXECUTION_PLAN.md](EXECUTION_PLAN.md): replacement scenarios, preflight, archive, thứ tự phụ thuộc, guard, rollback và điều kiện mở khóa.
4. [FK_GRAPH.json](FK_GRAPH.json): metadata FK SQL Server được SELECT lại, không phải danh sách được phép xóa.
5. [FINGERPRINT_BASELINES.json](FINGERPRINT_BASELINES.json): tách Source→Source Baseline, Trial→Trial Baseline và Source↔Trial.

Không chứa connection string, password/hash mật khẩu, session credential, tên/email/điện thoại user hoặc bản archive dữ liệu đầy đủ. User rows chỉ có IDs và trạng thái/quan hệ cần review; archive thực tế nếu được duyệt phải ngoài Git.

## B. Kết quả phân loại nguồn gốc

| Phân loại | Số row duy nhất |
| --- | ---: |
| CONFIRMED_SYNTHETIC | **0** |
| UNCERTAIN | **28** |
| PRESERVE | **343** |
| Tổng trong manifest | **371** |

Đếm theo `(table, actual PK)`, không đếm lại manager relationship như một bản ghi thứ hai. Chỉ là phạm vi explicit manifest; protected sets ngoài phạm vi không cộng vào 343. PRESERVE là quyết định bảo tồn, không khẳng định provenance thật/giả. Catalog RBAC có nguồn định nghĩa đã biết nhưng là baseline được bảo vệ, không phải fixture được phép reset.

Đã tìm trong working tree và lịch sử Git truy cập được. Tài liệu lịch sử `c9d7d53:docs/database/README.md` nhắc `HotelOpsDB_SampleData_Final.sql`, đồng thời nói raw SQL không được đưa vào repo. Không tìm thấy raw sample script trong các file/commit names đã kiểm tra. `tests/fixtures/api-fixture.mjs` và `src/mocks/*` ở commit đó là frontend/mock data, IDs như req1/shift1/mr1, không chứng minh đã tạo numeric SQL rows hiện tại. Backup/restore chứng minh bản sao từ HotelOpsDB, không chứng minh nguồn gốc seed từng row.

Vì vậy **không có REMOVE**, không row nào execution-eligible. Có 12 đề xuất DEACTIVATE có điều kiện; 16 ứng viên REPLACE hiện vẫn có `proposedAction=PRESERVE` cho đến khi provenance/closure/rowset được duyệt. 343 row bảo tồn không có changeset đi kèm.

## C–F. Tóm tắt phạm vi, rủi ro và phần phải duyệt

- WH-FB: có ledger POSTED và completed request/fulfillment liên quan được PRESERVE. Không thể reset độc lập warehouse/movements mà giữ nguyên các row phụ thuộc này. Cần provenance và quyết định DEV2 về việc bảo tồn toàn component; không tự chọn kho nhận.
- Requests 8/9, fulfillment 8/9, request 13, shifts 4/5 và con: chưa đủ nguồn gốc; giữ nguyên chờ xác nhận. Không fake cancellation/completion/check-out.
- Users 11/17/18/20: không xóa hoặc tái tạo user. Đề xuất account retirement chỉ sau giải quyết work và duyệt rowset; credentials không đổi. Session row giữ nguyên; revocation khi account bị disable là changeset FE-01 riêng được duyệt, không reset session.
- Department manager relationships là fields trên departments 3/5/6/10, không row độc lập; dự kiến giữ làm history khi department Inactive. Không thay users 1/4/5/19 hoặc fixed roles.
- Những row đã Inactive ở Task02C được bảo tồn, không seed lại.
- DEV1/DEV2 cần ký provenance, exact rowset/archive/replacement mapping, disposition của protected history, fixtures/scenarios mới, account/access/session effects, recovery và quyền thực thi riêng. Bất kỳ thiếu phần nào thì component tương ứng BLOCKED.

## G. Làm rõ fingerprint

| Phép so sánh | Kết quả |
| --- | --- |
| Source hiện tại → Source Baseline sau Task02C | 72/72 bảng khớp count + SHA-256 |
| Trial hiện tại → Trial Baseline sau Task02C | 72/72 bảng khớp count + SHA-256 |
| Source hiện tại ↔ Trial hiện tại | **62 bảng khớp, 10 bảng khác; hai database không giống nhau** |

Snapshot mới có thời gian lấy từ SQL `SYSUTCDATETIME()`:

- Source: **07:43:47.0427537Z–07:43:54.3039509Z**, tức 14:43:47–14:43:54 giờ Việt Nam.
- Trial: **07:43:54.7142738Z–07:44:00.8451035Z**, tức 14:43:54–14:44:00 giờ Việt Nam.
- 371 safe row projections được SELECT revalidate riêng lúc **07:46:51.9020216Z–07:46:54.0740504Z**.

Task02C source-after.json/trial-after.json có file mtime UTC tương ứng 07:17:08.6798977Z / 07:17:03.3548513Z. **Mtime không phải thời điểm SQL snapshot.** Task02C không ghi start/end acquisition; không thể khôi phục chính xác giờ snapshot cũ từ những file đó. File paths, SHA-256 của file và giới hạn này đã lưu trong JSON baseline.

Các hash là so sánh row content theo PK, không bao gồm schema/identity-next-value, file layout, permissions hoặc toàn trạng thái engine. Các SELECT chạy tuần tự ReadCommitted, không phải snapshot nguyên tử toàn database hoặc cross-database. Thời gian từng lần đọc row gốc của manifest không được ghi riêng; cửa sổ 07:46 là independent revalidation, không phải acquisition time bị suy đoán.

10 bảng khác Source↔Trial: roles, permissions, role_permissions, users, user_roles, user_branch_access, warehouses, internal_request_types, work_categories, audit_logs. Ví dụ roles 11→12, permissions 47→62, grants 182→140, audit_logs 14→22. Khớp 72/72 với **baseline riêng** không có nghĩa Source và Trial giống nhau. Đây cũng là cách đọc đúng nhận định trong Task02D.

## H–I. Validation, Git và kết luận

Đã SELECT lại FK, PK, fingerprints; 371 safe projection hashes được đối chiếu với SQL và với UTF-16LE JSON đã lưu. Kiểm tra JSON uniqueness/counts/action constraints và `git diff --check` trước bàn giao. Build/test C#: Not Run, không có thay đổi code; không chạy lại DBCC vì nhiệm vụ chỉ lập manifest, không thực hiện migration.

Chỉ tạo thư mục Task02E cùng ba tài liệu Markdown và ba JSON nêu trên. Không chỉnh Task01, source code, EF mapping, Authentication, connection string, Release hay các changeset cũ. Không commit/push/merge/PR.

**Final verdict: REVIEW-READY MANIFEST / RESET BLOCKED.** Cần chứng cứ nguồn và phê duyệt per-row/closure trước khi viết hoặc thực thi destructive reset. Hoàn tất retirement chỉ sau zero active dependencies, bốn legacy department Inactive, HK/MT/WH/FO Active, RBAC/fixed personnel nguyên vẹn và integrity/session/history checks đạt.
