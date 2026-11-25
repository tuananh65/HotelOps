# Dọn test và chuyển database chính thức

Owner xác nhận ngày 25/11/2026: đích HotelOpsCompactDB; chỉ xóa bản ghi có bằng chứng test, giữ tài khoản, danh mục và giao dịch thực tế. Không reset/ghi đè database. Production local cổng 5080; Development độc lập.

- [x] Kiểm kê database chính 49 bảng; constraints/triggers hợp lệ.
- [x] Backup COPY_ONLY CHECKSUM và VERIFYONLY; restore mới HotelOpsCompactWorkdayTestOfficial2611252122.
- [x] Rà thứ tự 11 delta migration, guards và phụ thuộc provenance.
- [x] Chạy đủ 11 migration trên clone trong transaction rollback: 57 bảng/234 FK/116 CHECK/38 trigger; credential, ledger, documents, history preserved; DBCC passed. Chưa commit rehearsal sau cleanup.
- [x] Đối soát bản ghi test và quan hệ tới giao dịch thực tế; manifest196 chứng từ, archive audit trước xóa.
- [x] Diễn tập rollback rồi commit trên clone2140; bảo toàn credential/schema/ledger/history. API thật SchemaGate mapping và health đạt, worker diễn tập tắt.
- [x] Backup mới VERIFYONLY; cleanup+nâng cấp HotelOpsCompactDB commit cùng transaction, fingerprint đầu vào khớp rehearsal.
- [x] Production https://localhost:5080 dùng HotelOpsCompactDB, health57 bảng và Edge/API smoke đạt. Development5173/API5081 giữ clone riêng và hoạt động. Backup database chính sau nâng cấp CHECKSUM/VERIFYONLY đạt.

Owner xác nhận “xóa nốt” ngày25/11 lúc21:39: xóa thêm MR440/ISSUE441 cùng194 fixture. Không còn câu hỏi phê duyệt. Đã backup mới và restore HotelOpsCompactWorkdayTestOfficial2611252140. Cleanup và migration cùng outer transaction, giữ archive của bản ghi xóa trong audit; giữ công việc và lịch sử xử lý, chỉ bỏ liên kết tới chứng từ bị xóa. Mapping ấm sau cleanup phải chứng minh nguồn0 trước khi đăng ký0 tài sản, không tạo lại200 tài sản test.

Không gửi email thật, không triển khai EC2, không commit/push. Không xóa danh mục chỉ vì mã chứa TEST/DEMO; không tự ghi mất/hoàn kho.
