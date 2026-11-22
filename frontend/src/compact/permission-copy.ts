// Display copy only. Permission codes and grants continue to come from the API.
export const permissionCopy: Record<string, [string, string]> = {
  USER_VIEW: [
    "Xem tài khoản",
    "Xem danh sách và thông tin tài khoản nhân viên.",
  ],
  USER_MANAGE: [
    "Quản lý tài khoản",
    "Tạo tài khoản, cập nhật trạng thái và phạm vi làm việc của nhân viên.",
  ],
  ROLE_MANAGE: [
    "Quản lý vai trò và phân quyền",
    "Quản lý vai trò, các quyền của vai trò và gán vai trò cho tài khoản.",
  ],
  MASTER_DATA_MANAGE: [
    "Quản lý cấu trúc và cấu hình",
    "Quản lý khách sạn, tầng, phòng, bộ phận và các danh mục nghiệp vụ dùng chung.",
  ],
  ROOM_VIEW: [
    "Xem phòng",
    "Xem danh sách phòng, tình trạng phòng và cấu trúc khách sạn trong phạm vi được giao.",
  ],
  ROOM_CHECKIN: [
    "Nhận phòng cho khách",
    "Ghi nhận khách nhận phòng theo điều kiện sẵn sàng của phòng.",
  ],
  ROOM_CHECKOUT: [
    "Trả phòng cho khách",
    "Ghi nhận khách trả phòng và chuyển phòng sang bước xử lý tiếp theo.",
  ],
  ROOM_OCCUPANCY_MOVE: [
    "Chuyển phòng cho khách",
    "Chuyển khách từ phòng hiện tại sang phòng khác đáp ứng điều kiện.",
  ],
  ROOM_BLOCK_MANAGE: [
    "Khóa hoặc mở sử dụng phòng",
    "Đặt hoặc gỡ chặn phòng khi có lý do cần tạm ngừng sử dụng.",
  ],
  CLEANING_VIEW: [
    "Xem công việc dọn phòng",
    "Xem danh sách, chi tiết và tiến độ các công việc dọn phòng được phép truy cập.",
  ],
  CLEANING_ASSIGN: [
    "Phân công dọn phòng",
    "Tạo và phân công công việc dọn phòng cho nhân viên phù hợp.",
  ],
  CLEANING_EXECUTE: [
    "Thực hiện dọn phòng",
    "Nhận việc, bắt đầu, tạm dừng và hoàn thành công việc dọn phòng được giao.",
  ],
  INSPECTION_VIEW: [
    "Xem kiểm tra phòng",
    "Xem danh sách và kết quả kiểm tra chất lượng phòng.",
  ],
  INSPECTION_PERFORM: [
    "Kiểm tra chất lượng phòng",
    "Thực hiện kiểm tra theo danh sách tiêu chí và ghi nhận kết luận.",
  ],
  DEFECT_RESOLVE: [
    "Xử lý lỗi sau kiểm tra",
    "Ghi nhận việc khắc phục các lỗi được phát hiện khi kiểm tra phòng.",
  ],
  TASK_VIEW: [
    "Xem công việc vận hành",
    "Xem nội dung, người phụ trách và tiến độ công việc trong phạm vi được giao.",
  ],
  TASK_MANAGE: [
    "Quản lý công việc vận hành",
    "Tạo, phân công và quản lý công việc vận hành.",
  ],
  TASK_EXECUTE: [
    "Thực hiện công việc",
    "Nhận và cập nhật quá trình thực hiện công việc được giao.",
  ],
  INCIDENT_VIEW: [
    "Xem sự cố",
    "Xem nội dung, mức độ và tình trạng xử lý sự cố.",
  ],
  INCIDENT_CREATE: [
    "Báo sự cố",
    "Tạo báo cáo sự cố để bộ phận phụ trách tiếp nhận xử lý.",
  ],
  INCIDENT_TRIAGE: [
    "Phân loại sự cố",
    "Đánh giá ban đầu và phân loại sự cố để chuyển xử lý phù hợp.",
  ],
  INCIDENT_MANAGE: [
    "Quản lý xử lý sự cố",
    "Cập nhật quá trình xử lý và quản lý trạng thái sự cố.",
  ],
  INCIDENT_ESCALATION_HANDLE: [
    "Xử lý sự cố được chuyển cấp",
    "Tiếp nhận và xử lý sự cố cần cấp phụ trách cao hơn giải quyết.",
  ],
  MAINTENANCE_VIEW: [
    "Xem công việc bảo trì",
    "Xem nội dung và tiến độ các công việc sửa chữa, bảo trì.",
  ],
  MAINTENANCE_ASSIGN: [
    "Phân công bảo trì",
    "Tạo và giao công việc bảo trì cho nhân viên kỹ thuật.",
  ],
  MAINTENANCE_EXECUTE: [
    "Thực hiện bảo trì",
    "Nhận việc và cập nhật quá trình sửa chữa, bảo trì được giao.",
  ],
  MAINTENANCE_REVIEW: [
    "Kiểm tra kết quả bảo trì",
    "Xem xét và xác nhận kết quả xử lý của công việc bảo trì.",
  ],
  PM_MANAGE: [
    "Quản lý bảo trì định kỳ",
    "Thiết lập và cập nhật lịch bảo trì phòng ngừa cho tài sản.",
  ],
  IR_VIEW: [
    "Xem yêu cầu nội bộ",
    "Xem nội dung và tiến độ các yêu cầu phối hợp trong khách sạn.",
  ],
  IR_CREATE: [
    "Tạo yêu cầu nội bộ",
    "Gửi yêu cầu hỗ trợ hoặc phối hợp đến người, bộ phận phụ trách.",
  ],
  IR_PROCESS: [
    "Xử lý yêu cầu nội bộ",
    "Tiếp nhận và cập nhật quá trình giải quyết yêu cầu được giao.",
  ],
  IR_OVERDUE_HANDLE: [
    "Xử lý yêu cầu quá hạn",
    "Can thiệp và xử lý các yêu cầu nội bộ đã quá thời hạn.",
  ],
  CHECKLIST_MANAGE: [
    "Quản lý mẫu kiểm tra",
    "Tạo và chỉnh sửa danh sách tiêu chí dùng cho công việc, dọn phòng và kiểm tra.",
  ],
  ASSET_VIEW: ["Xem tài sản", "Xem thông tin, vị trí và tình trạng tài sản."],
  ASSET_MANAGE: [
    "Quản lý thông tin tài sản",
    "Tạo và cập nhật hồ sơ tài sản trong phạm vi được giao.",
  ],
  ASSET_TRANSFER: [
    "Điều chuyển tài sản",
    "Chuyển vị trí hoặc nơi chịu trách nhiệm quản lý tài sản.",
  ],
  ASSET_RETIRE: [
    "Ngừng sử dụng tài sản",
    "Ghi nhận tài sản được đưa ra khỏi sử dụng theo điều kiện hệ thống.",
  ],
  ITEM_MASTER_MANAGE: [
    "Quản lý danh mục vật tư",
    "Tạo và cập nhật vật tư dùng chung, kho và định mức tồn kho theo quyền được giao.",
  ],
  INVENTORY_VIEW: [
    "Xem tồn kho",
    "Xem số dư vật tư, kho và thông tin chứng từ được phép truy cập.",
  ],
  STOCK_RECEIPT_POST: [
    "Ghi nhận nhập kho",
    "Lập và ghi sổ phiếu nhập để tăng số lượng vật tư trong kho.",
  ],
  STOCK_ISSUE_POST: [
    "Ghi nhận xuất kho",
    "Lập và ghi sổ phiếu xuất để cấp vật tư từ kho.",
  ],
  STOCK_RETURN_POST: [
    "Ghi nhận trả kho",
    "Lập và ghi sổ phiếu trả vật tư về kho.",
  ],
  STOCK_TRANSFER_POST: [
    "Ghi nhận chuyển kho",
    "Xử lý chứng từ điều chuyển vật tư giữa các kho theo phạm vi được cấp.",
  ],
  STOCKTAKE_PERFORM: [
    "Kiểm kê kho",
    "Ghi nhận số lượng kiểm đếm thực tế và kết quả kiểm kê vật tư.",
  ],
  STOCK_ADJUST_POST: [
    "Điều chỉnh tồn kho",
    "Ghi sổ chứng từ điều chỉnh số lượng tồn kho theo lý do được ghi nhận.",
  ],
  STOCK_REVERSE: [
    "Đảo chứng từ kho",
    "Đảo tác động của chứng từ đã ghi sổ theo điều kiện hệ thống.",
  ],
  MR_VIEW: [
    "Xem yêu cầu vật tư",
    "Xem vật tư được yêu cầu, tiến độ phê duyệt và cấp phát.",
  ],
  MR_CREATE: [
    "Tạo yêu cầu vật tư",
    "Lập yêu cầu cấp vật tư phục vụ công việc.",
  ],
  MR_APPROVE_L1: [
    "Duyệt yêu cầu vật tư",
    "Phê duyệt hoặc từ chối yêu cầu vật tư ở cấp duyệt thứ nhất.",
  ],
  MR_APPROVE_FALLBACK: [
    "Duyệt thay yêu cầu vật tư",
    "Xử lý duyệt thay khi yêu cầu đủ điều kiện chuyển sang người duyệt dự phòng.",
  ],
  MR_FULFILL: [
    "Cấp vật tư theo yêu cầu",
    "Chuẩn bị và cấp phát vật tư cho yêu cầu đã được duyệt.",
  ],
  REPLENISHMENT_VIEW: [
    "Xem bổ sung vật tư",
    "Xem đề nghị bổ sung vật tư và tiến độ tiếp nhận.",
  ],
  REPLENISHMENT_CREATE: [
    "Tạo đề nghị bổ sung vật tư",
    "Lập đề nghị bổ sung vật tư để đáp ứng nhu cầu kho.",
  ],
  REPLENISHMENT_RECEIVE: [
    "Nhận vật tư bổ sung",
    "Ghi nhận vật tư đã tiếp nhận theo đề nghị bổ sung.",
  ],
  REPLENISHMENT_CLOSE: [
    "Kết thúc bổ sung vật tư",
    "Đóng đề nghị bổ sung khi đã đáp ứng điều kiện hoàn tất.",
  ],
  DASHBOARD_VIEW: [
    "Xem tổng quan",
    "Xem thống kê vận hành thuộc các nghiệp vụ đã được cấp quyền.",
  ],
  REPORT_VIEW: [
    "Xem báo cáo",
    "Xem và xuất báo cáo trong phạm vi nghiệp vụ và khách sạn được giao.",
  ],
  AUDIT_VIEW: [
    "Xem nhật ký hệ thống",
    "Xem lịch sử thay đổi và người thực hiện thao tác trong hệ thống.",
  ],
  SHIFT_MANAGE: [
    "Quản lý lịch làm việc",
    "Cấu hình ca và phân công lịch làm việc trong phạm vi khách sạn được giao.",
  ],
  ATTENDANCE_RECORD: [
    "Bắt đầu và kết thúc ca",
    "Ghi nhận thời điểm bắt đầu, kết thúc ca làm việc được phân cho chính mình.",
  ],
  INVENTORY_POST: [
    "Ghi sổ kho tổng hợp",
    "Quyền danh mục dự phòng cho ghi sổ kho. Các thao tác hiện tại dùng quyền riêng cho từng loại chứng từ.",
  ],
  MR_APPROVE_L2: [
    "Duyệt vật tư cấp hai",
    "Quyền danh mục dự phòng cho cấp duyệt thứ hai. Chưa được dùng trong luồng duyệt vật tư hiện tại.",
  ],
  ROOM_RELEASE: [
    "Đưa phòng về sẵn sàng",
    "Quyền danh mục dự phòng cho việc trả phòng về trạng thái sẵn sàng. Hiện phòng được xử lý qua luồng nghiệp vụ tương ứng.",
  ],
  ROOM_STATUS_UPDATE: [
    "Cập nhật trạng thái phòng",
    "Quyền danh mục dự phòng cho cập nhật trạng thái phòng. Hiện các thay đổi dùng quyền nhận, trả, chuyển hoặc chặn phòng riêng.",
  ],
  SETTINGS_MANAGE: [
    "Quản lý thiết lập chung",
    "Quyền danh mục dự phòng cho thiết lập hệ thống. Màn hình cấu hình hiện tại dùng quyền quản lý cấu trúc và cấu hình.",
  ],
  STOCK_APPROVE: [
    "Duyệt chứng từ kho",
    "Quyền danh mục dự phòng cho phê duyệt chứng từ kho. Chưa được dùng trong các thao tác kho hiện tại.",
  ],
  STOCKTAKE_MANAGE: [
    "Quản lý đợt kiểm kê",
    "Quyền danh mục dự phòng cho quản lý đợt kiểm kê. Thao tác kiểm kê hiện tại dùng quyền kiểm kê kho.",
  ],
};
export function permissionText(code: string, description?: string) {
  const copy = permissionCopy[code];
  return {
    name: copy?.[0] || description || "Quyền chưa có tên hiển thị",
    description:
      copy?.[1] ||
      description ||
      "Chưa có mô tả cho quyền này. Liên hệ quản trị hệ thống để bổ sung.",
  };
}
