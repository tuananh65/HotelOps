import { TimeLogFailure } from "./timeLogApi";

export function correctionMessage(error: unknown): string {
  const messages: Record<string, string> = {
    MSG03: "Kiểm tra thời gian đề xuất và lý do. Thời gian phải nằm trong ca, đã xảy ra và không trùng công việc khác.",
    TIME_STALE: "Dữ liệu đã thay đổi. Tải lại trước khi gửi đề nghị hoặc xét duyệt.",
    CORRECTION_ALREADY_PENDING: "Khoảng thời gian này đã có đề nghị đang chờ duyệt. Hãy xem đề nghị đã gửi.",
    CLOSED_INTERVAL_REQUIRED: "Chỉ có thể đề nghị điều chỉnh khoảng thời gian đã kết thúc.",
    CORRECTION_ALREADY_DECIDED: "Đề nghị đã được xử lý. Tải lại để xem kết quả.",
    ORIGINAL_INTERVAL_CHANGED: "Thời gian gốc đã thay đổi. Tải lại để kiểm tra trước khi xử lý.",
    CORRECTION_FORBIDDEN: "Bạn không có quyền thực hiện thao tác này.",
    CORRECTION_UNAVAILABLE: "Đề nghị không còn khả dụng hoặc nằm ngoài quyền truy cập.",
    INTERVAL_UNAVAILABLE: "Khoảng thời gian không còn khả dụng hoặc không thuộc tài khoản của bạn.",
  };
  if (error instanceof Error && messages[error.message]) return messages[error.message];
  if (error instanceof TimeLogFailure && error.status === 401) return "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
  if (error instanceof TimeLogFailure && error.status === 403) return "Bạn không có quyền thực hiện thao tác này.";
  return "Chưa xác nhận được kết quả. Tải lại danh sách để kiểm tra trước khi gửi lại.";
}
