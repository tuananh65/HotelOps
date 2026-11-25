# Vòng đời vật tư tái sử dụng

Sổ riêng: ReusablePosition giữ branch/item/custody/status/quantity; ReusableEntry ghi chuyển động bất biến; ReusableWriteOff giữ đề nghị và quyết định. Đơn vị kho, nhân viên và phòng là các nơi giữ khác nhau, không cộng lặp.

READY được cấp, STAFF_HOLDING là nhân viên giữ, IN_USE là đang dùng, DIRTY cần giặt, LAUNDRY đang giặt, DAMAGED không khả dụng. IN_TRANSIT còn thuộc nguồn cho tới khi kho đích xác nhận thực nhận. WRITTEN_OFF không thuộc sở hữu sau giao dịch được duyệt và commit.

READY → IN_USE → DIRTY → LAUNDRY → READY bảo toàn tổng sở hữu. Cấp/hoàn phòng đồng bộ với sổ kho; đồ hoàn bẩn không tự thành READY. Một operation key không được hạch toán hai lần.

Theo D03, Thủ kho đề nghị ghi giảm hàng DAMAGED, OM khác người lập và cùng chi nhánh duyệt. Chờ duyệt vẫn thuộc sở hữu nhưng không khả dụng. Từ chối phải có lý do và lịch sử. Không dùng thao tác thay khăn làm giao dịch tiêu hao.

Legacy staff balance được đối soát qua ReclassifiedQuantity; giữ ledger cũ, không tạo phiếu hoàn giả. Bằng chứng HTTP hiện có bao gồm vòng giặt, ghi giảm, cấp/hoàn phòng, điều chuyển thực nhận một phần, duplicate/over-receipt. Chưa đủ bằng chứng browser cho cả ca làm việc và việc thay đồ của nhân viên.
