# Task83 — BLOCKED trước implementation

Mục tiêu hoàn tất thêm một DEV1 UC không đạt. Cả hai lựa chọn đã COMPLETE_VERIFIED_V1 trong
accepted snapshot/ledger; current checkout thiếu các service/controller/DI dependency tương ứng.
Không chọn UC nào để tái triển khai. Xem TASK83_UC_SELECTION.md và blob/receipt evidence.

## Những gì thực sự đã làm

- Đọc requirements, ledger, Task82 và actual Git source/accepted receipts Task70/72.
- Xác nhận UC142 earned1, UC038 earned2 đã nằm trong60 điểm. Không cộng lại.
- Phân biệt accepted historical implementation với current working-tree integration.
- Kiểm source contracts: UC142 SQL-scoped list/detail; UC038 relational history/private attachments,
  filters/session/grant boundaries, recorded cancellation evidence; không dựng writer mới.
- Kiểm kê trial bằng SELECT trong master.sys.databases sau khi xác minh instance. Có310 canonical
  trial metadata records tại thời điểm đọc; trial Task82 tồn tại/ONLINE. Không mở business database.
- Git/path baseline and Task82 source SHA256 verified before build; full physical SHA capture overlapped isolated build outputs under Task83. This timing limitation is retained in validation.
- Chỉ tạo Task83 reports/manifests/logs; production files created/modified/deleted:0.

## Cần có trước khi tích hợp runtime

Current Program thiếu scoped RoomTypeCreationService. Snapshot UC142/038 dùng partial service
với shared BatchAsync và RoomTypeSchema. Các file RoomTypeSchema.cs, HotelOpsDbContext.RoomTypes.cs
và associated optional-model dependencies chưa có ở checkout này. Không copy wholesale snapshot
hoặc thay mapping legacy global để giả branch ownership. Không đăng ký DI/route thiếu service.
Các shared changes cần được review theo file trong task integration riêng trước khi áp dụng.
Không có shared proposal nào được áp dụng hoặc ngầm coi approved ở Task83.

## Verification và giới hạn

Business HTTPS/SQL UC142/038: NOT_RUN vì không chọn/triển khai endpoint. Không claim runtime PASS.
Không tạo/reset trial, không sửa Task82 database. Schema metadata của trial không được suy từ EF;
chỉ đọc inventory name/state trong master, không query tables của bất kỳ business database nào.
Build/security/framework/diff/preservation kết quả thực thi xem TASK83_VALIDATION.json và logs.
Accepted Task70/72 receipts là historical evidence, không cộng vào Task83 test counts.

Không lỗi implementation được phát sinh vì không có production edits. Blocker chính là task
selection đã trùng accepted UCs và missing integration cần shared approval. NU1900 nếu build chạy
phải ghi đúng, không đổi packages để che warning. Framework0 cases không được ghi PASS.

## Task84 đề xuất

**Khôi phục/tích hợp production UC142 từ accepted snapshot, với dependency closure và exact shared
diff được duyệt riêng, rồi fresh HTTPS/SQL acceptance.** Đây là integration recovery, không phải
một UC mới để cộng điểm. Ưu tiên đóng khoảng trống accepted-vs-usable trước khi giao thêm completion
sprint từ một checkout thiếu baseline. Không thể hứa một unfinished UC tiếp theo hoàn tất chỉ từ hai
ứng viên đã complete này. Không bắt đầu Task84 hoặc mở rộng scope trong Task83.

## Kết quả kiểm tra mới trong Task83

- Baseline build: PASS, exit0,0 errors,2 NU1900 warnings (vulnerability feed unavailable).
- Security regression:93 Authorization +43 Permission +14 CurrentUser +49 Resource =199 PASS,
  chạy lại executable Task82 đã có; không rebuild từng harness, không business SQL acceptance.
- `dotnet test` exit0 nhưng0 framework cases; NOT AVAILABLE, không tính PASS.
- `git diff --check`: PASS. Full postflight25.397 files:0 changed,0 read errors.
- UC142/038 HTTPS/SQL: NOT_RUN. Không có endpoint mới để nghiệm thu; không tạo trial cho test giả.
- Git/path baseline và ba Task82 source hashes có trước build; full SHA capture hoàn tất trong
  lúc baseline build chạy với output riêng Task83. Không tuyên bố full pre-build byte snapshot.

Chi tiết historical UC142:49 labels đúng prefix UC142;40 HTTP/persistence +9 final SQL boundaries.
Một nhãn UC020 có chữ UC142 chỉ là integration check, không cộng thành assertion UC142 thứ50.
Xem HISTORICAL_RECEIPT_COUNT_CHECK.json. UC038/048208 combined checks không tách giả thành208 UC038.
