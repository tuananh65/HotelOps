# FS-003 — Next actions / proposed FS-004

1. Developer/DEV3 xác nhận target SQL disposable, không production data, principal/account/grants và scope auth/session-touch/write testing. Sửa connectivity/TLS local bằng trusted certificate chain; không đổi shared connection string hoặc skip validation. Trial tạo mới/seed/schema installation không thuộc authorization hiện tại.
2. Cấu hình backend HTTPS port khớp API proxy, frontend PEM/CA trust ngoài Git. Hoàn thiện real login/session UI của DEV3 hoặc quy trình login API đã được phép trên trial. Không dùng prototype credentials.
3. **FS-004 đề xuất: Live room-read + UC032 SQL trial acceptance.** Chạy 10 cases trong REAL_API_VERIFICATION; đối chiếu SELECT SQL cùng filter/page; ghi read200/anonymous401/forbidden403 đúng thực tế. GET cũng cần cho phép session touch trên trial.
4. Khi write scope đã được xác nhận, nghiệm thu UC032: current owner/branch, actual Progress/Housekeeping schemas, If-Match/CSRF,100 remains IN_PROGRESS, items/note/private evidence, real revision/log/audit, stale412/rollback/no silent replay. Không cộng lại historical UC032 credit.
5. Sau live acceptance, ưu tiên approved CleaningMonitor activation/source-read để có task discovery; chưa tự bật recovery. UI current known-ID route cần manual authorized task/branch IDs. Tiếp theo timeline/private evidence view có thể nối independent read contract.
6. Các phase cleaning start/complete/inspection/rework/Room Ready chờ lifecycle/current-cycle/time/notification contracts; không dùng progress100 để thay workflow. Operational Tasks/IR giữ roadmap FS002, UC107 vẫn không có implementation authorization.

## Pending approvals/configuration

- Disposable target/data/account ownership + actual API/SQL testing rights chưa xác nhận.
- Database create/reset/migrate/seed/production write: chưa được phép, không tự làm.
- Cert/HTTPS/proxy/session integration của DEV3 chưa hoàn chỉnh.
- Shared/recovery route/security changes, Reception/DH matrix, notification/source-read/room coordinator/workday proposals: chưa áp dụng.
- Không cần thêm shared-auth/backend changes để giữ UC032 scoped frontend implementation hiện tại; historical active contract đã rõ. Runtime E2E vẫn blocked.

Không merge develop/main/integration tự động. FS002 đã được publish riêng; FS003 chỉ publish selected DEV1 changes sau review/tests/secrets/diff check. Không gửi dependencies ra ngoài nếu developer chưa yêu cầu.
