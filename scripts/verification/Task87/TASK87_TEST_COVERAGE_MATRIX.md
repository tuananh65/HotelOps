# TASK87 — Coverage matrix

Đây là coverage hành vi, không phải tỷ lệ line coverage. 91 framework cases thực thi: unit tests/controller calls và service tests với fake permission facts; không có framework HTTP server/SQL integration cases.

| Feature | Framework cases PASS | Hành vi đã chạy | Chưa chạy |
|---|---:|---|---|
| Authentication / authorization | 20 | Anonymous handler; missing/restricted identity; inactive-user fact; missing grant; ALL/BRANCH/OWN; cross-branch; ownership; role-name alone rejected; cancellation | SQL revoked/expired session, actual employment eligibility: NOT_RUN_SQL_GATE |
| UC012 Create/Update | 22 | Required/length/code/contact validation; update fields/reason; rowversion length; strict DTO rejects server fields/code mutation | Duplicate code normalization/conflict; stale rowversion; concurrent create/update; actual authorization; transaction/audit rollback: NOT_RUN_SQL_GATE |
| UC037 Monitor | 22 | Invalid list filters/date/IDs/page/overflow; timeline boundary pagination; missing identity returns no data | Actual branch filtering, ordering/ties, empty result/count/page, data isolation: NOT_RUN_SQL_GATE |
| UC038 Turnover history | 15 | Query restrictions/UTC/date/result/pagination; valid page boundaries deny missing identity | SQL scope/permission intersection; response mapping; empty result; deterministic ordering: NOT_RUN_SQL_GATE |
| UC142 Room type | 10 | Page/size/overflow/sort/control search validation; valid active/inactive/sort/page inputs retain identity gate | Actual active/inactive/search/sort/page query results, response mapping, empty result: NOT_RUN_SQL_GATE |
| Shared controller checks | 2 | Each case calls UC012/038/142 controllers: plaintext 400, anonymous HTTPS 401 | Hosted routing/model binding/CSRF/real credentials: NOT_RUN (no framework host fixture) |

UC012 Deactivate / UC037 Cancel: ngoài phạm vi, không có test tuyên bố hoàn thành. Các UC DEV1 khác chưa có framework tests trong Task87. Không dùng 91 để biểu thị toàn bộ 26 UC được regression coverage. Các SQL cases được giữ trong acceptance backlog, không phải placeholder tests và không cộng vào skipped count.

Synthetic paths chỉ kiểm tra PermissionService thật; không xác nhận catalog có MASTER_DATA_MANAGE BRANCH/OWN hay operational ALL. Reception/Department Head permission revisions còn pending, không triển khai thêm rule.

SQL gate: cần approval riêng cho disposable trial mới, định danh DB/fixtures/schema prerequisites và cleanup. Không sử dụng HotelOpsDB hoặc trial Task86. Coverage tooling chưa cấu hình; TRX đã lưu.
