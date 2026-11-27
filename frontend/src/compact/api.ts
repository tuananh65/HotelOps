import { p0Errors } from "./p0-error-messages";
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
export const sessionToken = () => token;
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
  if(response.ok && method!=="GET" && /^notifications\/\d+\/(read|acknowledge)$/.test(path))
    window.dispatchEvent(new Event("compact:notification-read"));
  if (!response.ok) {
    if (data?.title === "ACTIVE_SHIFT_REQUIRED") window.dispatchEvent(new Event("compact:shift-required"));
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
  HOUSEKEEPING_HANDOVER_BEFORE_ACCEPT_ONLY: "Chỉ được đề nghị bàn giao trước khi nhận hoặc bắt đầu công việc.",
  HANDOVER_REQUEST_PENDING: "Đang chờ giám sát tiếp nhận bàn giao. Bạn không thể nhận hoặc bắt đầu công việc này.",
  HANDOVER_REQUEST_ALREADY_OPEN: "Đã gửi đề nghị bàn giao. Vui lòng chờ giám sát xử lý.",
  HANDOVER_ALREADY_REQUESTED: "Bạn đã đề nghị bàn giao công việc này. Vui lòng chờ giám sát xử lý.",
  USE_SUPERVISOR_HANDOVER: "Hãy dùng Đề nghị bàn giao để chuyển công việc cho giám sát xử lý.",
  INTEGER_QUANTITY_REQUIRED: "Vật tư này chỉ chấp nhận số lượng nguyên.",
  SUPPLY_CONFIRMATION_SNAPSHOT_INVALID: "Không đọc được lịch sử xác nhận vật tư. Liên hệ quản lý để kiểm tra.",

  ROOM_ISSUE_EXCEEDS_ASSESSED_MISSING: "S? l??ng b?o thi?u v??t ph?n ??nh m?c c?n thi?u sau ti?u hao v? c?c b?o c?o ?? ghi nh?n.",
  REMEDIATION_EXCEEDS_ASSESSED_MISSING: "S? l??ng y?u c?u v??t ph?n ??nh m?c c?n thi?u sau ti?u hao v? c?c y?u c?u c?p ?? ghi nh?n.",
  REMEDIATION_EXCEEDS_REPORTED_MISSING: "S? l??ng y?u c?u v??t s? l??ng c?n thi?u c?a b?o c?o s? c?.",
  CLEANING_PHOTO_REQUIRED: "Cần ít nhất một ảnh phòng sau khi dọn để gửi hoàn thành.",
  INVALID_CLEANING_PHOTOS: "Ảnh không còn hợp lệ cho công việc này. Xóa ảnh lỗi và chọn ảnh mới.",
  CLEANING_PHOTO_EXECUTION_MISMATCH: "Ảnh không thuộc lần thực hiện hiện tại. Chọn ảnh mới rồi gửi lại.",
  CLEANING_NOT_IN_PROGRESS: "Công việc không còn đang thực hiện. Đóng và mở lại chi tiết để kiểm tra.",
  INVALID_CLEANING_IMAGE: "Nội dung ảnh không hợp lệ. Chọn ảnh PNG hoặc JPEG khác.",
  CLEANING_IMAGE_PNG_JPEG_ONLY: "Báo cáo dọn phòng chỉ chấp nhận ảnh PNG hoặc JPEG.",
  CLEANING_IMAGE_DIMENSIONS: "Ảnh không được vượt quá 40 triệu điểm ảnh.",
  SUBMITTED_PHOTO_IMMUTABLE: "Ảnh đã gửi báo cáo được giữ nguyên trong lịch sử.",
  FILE_SIZE_MAX_10_MB: "Mỗi ảnh phải lớn hơn 0 và không quá 10 MB.",

  REQUEST_DESCRIPTION_REQUIRED: "Loại yêu cầu này bắt buộc nhập mô tả chi tiết theo hướng dẫn.",
  REQUEST_SENDER_REQUIRED: "Chọn ít nhất một vai trò được gửi yêu cầu.",
  REQUEST_SENDER_INVALID: "Vai trò được chọn không còn quyền gửi yêu cầu nội bộ.",
  REQUEST_SENDER_NOT_ALLOWED: "Vai trò của bạn không được gửi loại yêu cầu này.",
  REQUEST_RECIPIENT_REQUIRED: "Chọn ít nhất một người hoặc bộ phận nhận.",
  REQUEST_RECIPIENT_NOT_ALLOWED: "Nơi nhận không nằm trong danh sách được cấu hình cho loại yêu cầu này.",
  REQUEST_RECIPIENT_UNAVAILABLE: "Người nhận không còn đủ điều kiện xử lý yêu cầu trong chi nhánh.",
  ROOM_ISSUE_IMPACT_CONFIGURATION_INVALID: "Mỗi loại báo cáo đang bật cần ít nhất một mức ảnh hưởng được phép chọn, có tên hợp lệ.",
  ROOM_ISSUE_IMPACT_UNAVAILABLE: "Mức ảnh hưởng này đã bị tắt. Tải lại và chọn mức đang được phép sử dụng.",
  CLEANING_INCIDENT_REVIEW_REQUIRED: "Phòng còn sự cố cần xác nhận và khắc phục. Hoàn tất kiểm tra sự cố trước khi kết luận vệ sinh thông thường.",
  BRANCH_WAREHOUSE_REQUIRED: "Chi nhánh cần có đúng một kho đang hoạt động để tiếp nhận yêu cầu bổ sung.",
  ROOM_NOT_RESERVED: "Phòng chưa được đặt. Vui lòng đặt phòng trước khi xác nhận khách đến.",
  TARGET_ROOM_MUST_DIFFER: "Phòng chuyển đến phải khác phòng hiện tại.",
  ACTIVE_WORK_IN_PROGRESS: "Bạn đang thực hiện một công việc khác. Mở việc đang chạy và hoàn thành trước khi bắt đầu công việc này.",
  CLEANING_CREATOR_NOT_ON_SHIFT: "Bạn cần được phân ca và đang trong giờ làm việc tại chi nhánh này để tạo yêu cầu dọn phòng.",
  RECEPTION_NOT_ON_SHIFT: "Bạn cần được phân ca và đang trong giờ làm việc tại chi nhánh này để thao tác phòng hoặc báo sự cố.",
  SHIFT_ALREADY_ENDED: "Ca đã hết giờ. Vui lòng chọn ca còn giờ làm hoặc ngày tiếp theo.",
  REQUEST_VISIBILITY_DENIED: "Bạn chỉ được xem yêu cầu nội bộ do mình tạo hoặc được giao cho mình.",
  CLEANING_ASSIGNEE_REQUIRED: "Vui lòng chọn nhân viên dọn phòng đang trong ca.",
  CLEANING_STAFF_REQUIRED: "Chỉ được phân công cho nhân viên buồng phòng hợp lệ tại chi nhánh này.",
  CLEANING_ASSIGNEE_NOT_ON_SHIFT: "Nhân viên được chọn hiện không trong ca. Vui lòng chọn lại nhân viên đang làm việc.",
  CLEANING_ASSIGNEE_HAS_UNACCEPTED_WORK: "Nhân viên còn công việc chờ nhận. Chỉ giao thêm sau khi nhân viên nhận công việc đó.",
  INVALID_TASK_PERIOD:
    "Khoảng thống kê không hợp lệ. Chọn ngày, tuần hoặc tháng.",
  USE_BRANCH_CONTACT_FORM:
    "Chỉnh sửa người đại diện trong màn thông tin chi nhánh.",
  DUPLICATE_NAME: "Tên đã tồn tại trong danh mục này. Vui lòng chọn tên khác.",
  INVALID_PHONE:
    "Số điện thoại không hợp lệ. Nhập từ 8 đến 30 ký tự, bắt đầu bằng số hoặc dấu +.",
  NAME_REQUIRED: "Vui lòng nhập tên.",
  PERMISSION_USED_BY_ACTIVE_USER:
    "Không thể ẩn quyền đang được cấp cho tài khoản hoạt động.",
  HOUSEKEEPING_WORK_ALREADY_ACTIVE:
    "Đã có phiếu cùng loại đang xử lý. Mở phiếu hiện tại để tiếp tục.",
  REWORK_NOT_SUBMITTED:
    "Cần hoàn thành công việc làm lại trước khi kiểm tra lại hoặc xác nhận sửa lỗi.",
  SUBMITTED_HOUSEKEEPING_IMMUTABLE:
    "Công việc đã gửi kiểm tra hoặc kết luận; không thể phân công lại ở bước này.",
  FINDING_ALREADY_RESOLVED:
    "Lỗi đã được xử lý. Vui lòng tải lại phiếu kiểm tra.",
  CHECKLIST_CODE_NAME_REQUIRED: "Nhập mã và tên mẫu checklist.",
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
  HOUSEKEEPING_PROGRESS_NOT_SUPPORTED: "Công việc dọn phòng không sử dụng cập nhật tiến độ.",
  HOUSEKEEPING_HANDOVER_BEFORE_START_ONLY: "Chỉ được bàn giao trước khi bắt đầu công việc.",
  HOUSEKEEPING_STAFF_CANNOT_REPORT_INCIDENT: "Nhân viên dọn phòng không báo cáo sự cố. Giám sát sẽ kiểm tra và ghi nhận sau khi hoàn thành.",
  HOUSEKEEPING_EVIDENCE_NOT_SUPPORTED: "Công việc dọn phòng không yêu cầu nhân viên gửi minh chứng.",
  CLEANING_DURATION_REQUIRED: "Chọn 20, 25 hoặc 30 phút, tính từ lúc giao việc.",
  ROOM_REPORT_EXECUTION_REQUIRED: "Chỉ báo sự cố trong lúc thực hiện dọn phòng hoặc kiểm tra phòng được giao.",
  INSPECTION_VERSION_REQUIRED: "Kết quả kiểm tra đã thay đổi. Đóng và mở lại chi tiết công việc.",
  INSPECTION_ASSIGNED_TO_ANOTHER: "Công việc kiểm tra đang được giao cho giám sát khác.",
  PAPER_FINDINGS_CONFIRMATION_REQUIRED: "Cần hoàn thành việc làm lại và xác nhận các lỗi đã được khắc phục.",
  INSPECTION_CHECKLIST_REQUIRED:
    "Phiếu kiểm tra phải có checklist. Hãy chọn mẫu kiểm tra đã công bố.",
  CHECKLIST_INCOMPLETE:
    "Hoàn tất các mục checklist bắt buộc trước khi gửi hoặc kết luận đạt.",
  INVALID_CREDENTIALS: "Tên đăng nhập hoặc mật khẩu chưa đúng.",
  ACCOUNT_LOCKED: "Tài khoản tạm khóa. Vui lòng thử lại sau 15 phút.",
  STALE_ROW_VERSION: "Dữ liệu đã thay đổi. Tải lại trước khi thao tác tiếp.",
  ROOM_NO_LONGER_NEEDS_CLEANING:
    "Phòng không còn đủ điều kiện tạo yêu cầu dọn. Hãy chọn lại phòng từ danh sách mới.",
  SHIFT_CAPACITY_REACHED: "Ca này đã đủ người.",
  SHIFT_OVERLAP: "Nhân viên đã có lịch trùng giờ.",
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
  ROOM_ISSUE_WRONG_STAGE: "Sự cố chưa đến bước này hoặc vừa được người khác xử lý. Vui lòng kiểm tra lại.",
  ROOM_ISSUE_USE_WORKFLOW: "Mở Sự cố phòng để duyệt, khắc phục và kiểm tra theo đúng quy trình.",
  ROOM_ISSUE_MATERIAL_NOT_FULFILLED: "Phiếu của lần khắc phục này chưa được kho cấp đủ.",
  ROOM_ISSUE_OBJECT_NOT_IN_ROOM: "Đồ vật không còn được ghi nhận thuộc phòng này.",
  ROOM_ISSUE_BROKEN_ASSET_REQUIRED: "Hỏng đồ chỉ áp dụng cho tài sản. Vật tư thay thế chỉ được báo mất hoặc thiếu đồ.",
  ROOM_ISSUE_ROOM_IN_USE: "Phòng đang có khách hoặc đã đặt, chưa thể thực hiện kiểm tra sự cố này.",
  ROOM_ISSUE_TYPE_INVALID: "Loại báo cáo chưa được cấu hình hoặc đã ngừng sử dụng.",
  ROOM_ISSUE_MATERIAL_REQUIRED: "Chọn bộ phận, kho, vật tư, đơn vị và số lượng cần bổ sung.",
  NETWORK_UNAVAILABLE:
    "Không kết nối được máy chủ. Kiểm tra kết nối rồi thử lại.",
  REQUIRED_CHECKLIST_INCOMPLETE: "Hoàn tất checklist bắt buộc trước khi gửi.",
  PASSWORD_CHANGE_REQUIRED: "Bạn cần đổi mật khẩu trước khi tiếp tục.",
  HTTP_429: "Quá nhiều yêu cầu. Vui lòng chờ một phút.",
};
export function message(error: unknown) {
  return error instanceof ApiError
    ? p0Errors[error.code] || errors[error.code] ||
        (error.status === 403
          ? "Bạn không có quyền thực hiện thao tác này."
          : error.status === 409
            ? "Dữ liệu đã thay đổi hoặc đang được sử dụng. Vui lòng tải lại và kiểm tra."
            : error.status === 400
              ? "Thông tin chưa hợp lệ. Vui lòng kiểm tra các trường đã nhập."
              : "Thao tác chưa hoàn tất. Vui lòng thử lại.")
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
