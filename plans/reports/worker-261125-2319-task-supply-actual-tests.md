# Kiểm thử SQL thật: cấp theo công việc

Đã restore HotelOpsCompactWorkdayTestTaskSupplies2611252325 từ backup chính thức CHECKSUM/VERIFYONLY và backup clone trước fixture. Không sửa HotelOpsCompactDB.

Thêm `tests/HotelOps.Compact.Tests/TaskSupplyDatabaseTests.cs`. Test gọi StaffSupplyService thật với EF/SQL Server, transaction serializable và rollback toàn bộ fixture. Xác nhận bắt đầu ca hai lần không xuất kho hoặc phát cảnh báo đầu ca; allocation ban đầu bằng 0; tạo công việc/assignment fixture chưa xuất kho; khi kho hết tồn khả dụng, Reserve trả STOCK_INSUFFICIENT và không tạo movement/reservation; nhận việc cấp đúng capacity × perGuest; gọi Reserve lại không cấp trùng; kiểm đếm chỉ bổ sung 1 đơn vị và Consume chỉ ghi dùng 1, không trừ kho lần hai. Kiểm tra số chứng từ sau rollback khớp ban đầu, không lưu công việc fixture.

Lệnh: `dotnet test tests/HotelOps.Compact.Tests/HotelOps.Compact.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~TaskSupplyDatabaseTests -m:1 -p:UseSharedCompilation=false --logger 'console;verbosity=normal'`, cấu hình HOTELOPS_TASK_SUPPLY_TEST trỏ clone qua User Secrets, không in connection string.

Kết quả cuối: 1 Passed, 0 Failed, 20.24 giây; build API/tests thành công. `node --check scripts/verification/task-supply-contract.mjs`: Passed.

Đã gặp deadlock khi tự chạy hai runner cùng fixture song song; thêm applock fixture trước đọc/ghi và chạy lại một runner đạt. Dừng build trùng của chính sub-agent. Không sửa retry/locking production.

Giới hạn: xác minh service supply hook thật, chưa chạy HTTP WorkService create/accept hoặc browser. Không khẳng định đã kiểm thử rollback sau lỗi giữa nhiều dòng hoặc handover. Các integration tests mặc định skip khi thiếu biến môi trường; parent cần giữ đúng bằng chứng lần chạy có clone.
