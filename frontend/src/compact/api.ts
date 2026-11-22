export type Row = Record<string, any>;
export type Resource = { type: string; fields: Row };
export type Page<T = Resource> = {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
};
const key = "hotelops.compact.session";
let token = sessionStorage.getItem(key) || "";
export const hasSession = () => !!token;
export function session(value: string) {
  token = value;
  value ? sessionStorage.setItem(key, value) : sessionStorage.removeItem(key);
}
export class ApiError extends Error {
  constructor(
    public status: number,
    public code: string,
    public traceId?: string,
  ) {
    super(code);
  }
}
export async function api<T = any>(
  path: string,
  body?: unknown,
  method = body === undefined ? "GET" : "POST",
  signal?: AbortSignal,
): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`/api/v2/${path}`, {
      method,
      signal,
      headers: {
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
        ...(body === undefined || body instanceof FormData
          ? {}
          : { "Content-Type": "application/json" }),
      },
      body:
        body === undefined
          ? undefined
          : body instanceof FormData
            ? body
            : JSON.stringify(body),
    });
  } catch (e) {
    if ((e as Error).name === "AbortError") throw e;
    throw new ApiError(0, "NETWORK_UNAVAILABLE");
  }
  const data = await response.json().catch(() => null);
  if (!response.ok) {
    if (response.status === 401 && path !== "auth/login") {
      session("");
      window.dispatchEvent(new Event("compact:expired"));
    }
    throw new ApiError(
      response.status,
      data?.title || `HTTP_${response.status}`,
      data?.traceId,
    );
  }
  return data as T;
}
export const action = (rowVersion: string, extra: Row = {}) => ({
  rowVersion,
  operationKey: crypto.randomUUID(),
  ...extra,
});
export async function downloadEvidence(id: number, name: string) {
  const response = await fetch(`/api/v2/evidence/${id}`, {
    headers: token ? { Authorization: `Bearer ${token}` } : {},
  });
  if (!response.ok) {
    const data = await response.json().catch(() => null);
    throw new ApiError(
      response.status,
      data?.title || `HTTP_${response.status}`,
    );
  }
  const url = URL.createObjectURL(await response.blob());
  const link = document.createElement("a");
  link.href = url;
  link.download = name;
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
const errors: Record<string, string> = {
  ACTIVE_INCIDENT_REQUIRED: "Chỉ tạo công việc bảo trì từ sự cố đang mở.",
  MAINTENANCE_ALREADY_ACTIVE:
    "Sự cố đã có công việc bảo trì đang xử lý hoặc chờ nghiệm thu.",
  MAINTENANCE_NOT_VERIFIED:
    "Cần nghiệm thu các công việc bảo trì liên quan trước khi đóng sự cố.",
  INDEPENDENT_VERIFIER_REQUIRED:
    "Người nghiệm thu phải độc lập với người đã thực hiện công việc.",
  TEMPLATE_IDENTITY_IMMUTABLE:
    "Không thể thay đổi mã, loại hoặc chi nhánh của kế hoạch đã tạo.",
  TEMPLATE_INACTIVE: "Kế hoạch đã dừng; vui lòng tải lại danh sách.",
  OCCURRENCE_ALREADY_GENERATED:
    "Lần bảo trì ở thời điểm này đã phát sinh công việc. Chọn thời điểm tiếp theo.",
  INVALID_SCHEDULE:
    "Kiểm tra chu kỳ, khoảng cách và thời điểm đến hạn của kế hoạch.",
  FIXED_OPERATIONAL_REFERENCE:
    "Mức độ ưu tiên và mức độ nghiêm trọng là danh mục cố định, không thể thêm hoặc chỉnh sửa.",
  TEMPORARY_PASSWORD_EXPIRED:
    "Mật khẩu tạm đã hết hạn. Liên hệ quản trị viên để nhận mật khẩu mới.",
  EMAIL_NOT_CONFIGURED:
    "Chưa cấu hình dịch vụ gửi email trên máy chủ. Mật khẩu chưa được thay đổi.",
  PASSWORD_EMAIL_FAILED:
    "Không gửi được email. Mật khẩu cũ vẫn được giữ; kiểm tra địa chỉ gửi, mật khẩu ứng dụng Gmail và kết nối rồi thử lại.",
  INVALID_GMAIL_ADDRESS:
    "Nhập địa chỉ Gmail hoặc Google Workspace hợp lệ của người gửi.",
  INVALID_GMAIL_APP_PASSWORD:
    "Nhập mật khẩu ứng dụng Gmail gồm 16 ký tự; không dùng mật khẩu đăng nhập Gmail.",
  PASSWORD_EMAIL_TIMEOUT:
    "Dịch vụ email phản hồi quá lâu. Mật khẩu cũ vẫn được giữ; vui lòng thử lại.",
  RESET_EMAIL_REQUIRED:
    "Tài khoản cần có email hợp lệ trước khi đặt lại mật khẩu.",
  ADMIN_PASSWORD_INVALID: "Mật khẩu xác nhận của bạn chưa đúng.",
  RESET_REQUIRES_ACTIVE_ACCOUNT:
    "Chỉ đặt lại mật khẩu cho tài khoản đang hoạt động và còn làm việc.",
  CANNOT_RESET_SELF:
    "Không thể dùng chức năng quản trị để đặt lại mật khẩu của chính mình.",
  PASSWORD_RESET_COOLDOWN:
    "Tài khoản vừa được gửi mật khẩu tạm. Vui lòng chờ một phút trước khi gửi lại.",
  EMAIL_ALREADY_USED: "Email này đã thuộc tài khoản khác.",
  INVALID_ACCOUNT_EMAIL: "Địa chỉ email không hợp lệ.",
  INVALID_ACCOUNT_NAME: "Họ tên không được bỏ trống.",
  LAST_ADMIN_PROTECTED: "Cần giữ ít nhất một quản trị viên có thể đăng nhập.",
  INVALID_TIMEZONE: "Múi giờ không hợp lệ. Ví dụ: Asia/Ho_Chi_Minh.",
  BRANCH_CODE_AND_NAME_REQUIRED: "Nhập mã và tên chi nhánh.",
  BRANCH_HAS_WORKDAY_RESPONSIBILITIES:
    "Chi nhánh còn ca đã phân hoặc đang làm; xử lý lịch trước khi đổi múi giờ.",
  ACTIVE_DEPENDENCIES_EXIST:
    "Còn dữ liệu đang sử dụng danh mục này. Xử lý các dữ liệu liên quan trước khi ngừng hoạt động.",
  DUPLICATE_RECORD: "Mã hoặc dữ liệu này đã tồn tại. Hãy kiểm tra lại.",
  OPERATION_ALREADY_APPLIED:
    "Thao tác này đã được ghi nhận. Kiểm tra lại chứng từ trước khi thử tiếp.",
  INSPECTION_CHECKLIST_REQUIRED:
    "Phiếu kiểm tra phải có checklist. Hãy chọn mẫu kiểm tra đã công bố.",
  CHECKLIST_INCOMPLETE:
    "Hoàn tất các mục checklist bắt buộc trước khi gửi hoặc kết luận đạt.",
  INVALID_CREDENTIALS: "Tên đăng nhập hoặc mật khẩu chưa đúng.",
  ACCOUNT_LOCKED: "Tài khoản tạm khóa. Vui lòng thử lại sau 15 phút.",
  STALE_ROW_VERSION: "Dữ liệu đã thay đổi. Tải lại trước khi thao tác tiếp.",
  SHIFT_CAPACITY_REACHED: "Ca này đã đủ người.",
  SHIFT_OVERLAP: "Nhân viên đã có lịch trùng giờ.",
  SHIFT_ALREADY_ACTIVE: "Bạn đang có một ca chưa kết thúc.",
  SHIFT_NOT_TODAY: "Chỉ bắt đầu ca được phân trong ngày hiện tại.",
  SHIFT_EMPLOYEE_NOT_ELIGIBLE: "Nhân viên không còn đủ điều kiện cho ca này.",
  USER_HAS_WORKDAY_RESPONSIBILITIES:
    "Nhân viên còn ca đã phân hoặc đang làm. Hãy xử lý lịch trước khi thay đổi quyền truy cập.",
  ROLE_HAS_WORKDAY_RESPONSIBILITIES:
    "Vai trò đang được dùng cho nhân viên còn lịch làm việc. Hãy xử lý lịch trước khi đổi quyền.",
  OWN_SHIFT_REQUIRED: "Bạn chỉ được thao tác ca của mình.",
  DOCUMENT_OWNER_REQUIRED:
    "Chỉ người tạo yêu cầu vật tư được sửa, gửi hoặc hủy yêu cầu này.",
  MATERIAL_REQUEST_NOT_STOCK_DOCUMENT:
    "Yêu cầu vật tư được cấp phát qua chứng từ kho bởi người phụ trách kho.",
  STOCK_INSUFFICIENT:
    "Tồn kho đã thay đổi hoặc không đủ số lượng. Kiểm tra lại trước khi ghi sổ.",
  SOURCE_QUANTITY_EXCEEDED:
    "Số lượng vượt phần còn lại của chứng từ nguồn. Hãy tải lại số liệu.",
  DOCUMENT_APPROVED_IMMUTABLE:
    "Chứng từ đã được duyệt; không được sửa dòng vật tư.",
  INDEPENDENT_APPROVAL_REQUIRED:
    "Cần người khác duyệt chứng từ trước khi ghi sổ.",
  DERIVATION_NOT_ALLOWED:
    "Chứng từ nguồn chưa đủ điều kiện hoặc bạn không có quyền tạo chứng từ này.",
  STOCK_CHANGED_RECOUNT_REQUIRED:
    "Tồn kho đã đổi sau kiểm đếm. Cần kiểm đếm lại trước khi điều chỉnh.",
  DOCUMENT_HAS_POSTED_DEPENDENTS:
    "Chứng từ đã có giao dịch liên quan được ghi sổ nên không thể đảo trực tiếp.",
  ITEM_UNIT_HAS_TRANSACTION_HISTORY:
    "Vật tư đã có lịch sử chứng từ, không thể thay đổi đơn vị cơ sở.",
  EXACT_QUANTITY_REQUIRED:
    "Điều chỉnh hoặc đảo giao dịch phải khớp chính xác số lượng gốc.",
  CURRENT_ASSIGNEE_REQUIRED:
    "Công việc không còn được giao cho bạn. Hãy kiểm tra lại danh sách công việc.",
  INVALID_WORK_TRANSITION:
    "Trạng thái công việc đã thay đổi hoặc chưa đủ điều kiện cho thao tác này.",
  ACCEPT_ASSIGNMENT_FIRST: "Bạn cần nhận việc trước khi bắt đầu.",
  RECEIVING_DEPARTMENT_AUTHORITY_REQUIRED:
    "Chỉ người được giao xử lý hoặc người điều phối bộ phận mới được thực hiện thao tác này.",
  PERMISSION_DENIED: "Bạn không có quyền thực hiện thao tác này.",
  NETWORK_UNAVAILABLE:
    "Không kết nối được máy chủ. Kiểm tra kết nối rồi thử lại.",
  REQUIRED_CHECKLIST_INCOMPLETE: "Hoàn tất checklist bắt buộc trước khi gửi.",
  PASSWORD_CHANGE_REQUIRED: "Bạn cần đổi mật khẩu trước khi tiếp tục.",
  HTTP_429: "Quá nhiều yêu cầu. Vui lòng chờ một phút.",
};
export function message(error: unknown) {
  return error instanceof ApiError
    ? errors[error.code] || `Thao tác chưa hoàn tất (${error.code}).`
    : "Có lỗi xảy ra. Vui lòng thử lại.";
}
export const fields = (items: Resource[] = []) => items.map((x) => x.fields);
export const dateKey = (d = new Date()) =>
  `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
export const timestamp = (s?: string) =>
  s
    ? new Date(/Z$|[+-]\d\d:\d\d$/.test(s) ? s : `${s}Z`).toLocaleString(
        "vi-VN",
      )
    : "—";
