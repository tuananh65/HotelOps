import { useSyncExternalStore } from "react";
import catalog from "./catalog.json";
import { fieldsVi } from "./locales/fields.vi";
import { metricsVi } from "./locales/metrics.vi";
import { workflowsVi } from "./locales/workflows.vi";
import { screenDescriptionsVi } from "./locales/screen-descriptions.vi";
import { viExtra } from "./locales/vi-extra";
export type Language = "en" | "vi";
let language: Language =
  localStorage.getItem("hotelops-language") === "vi" ? "vi" : "en";
document.documentElement.lang = language;
const listeners = new Set<() => void>();
export const getLanguage = () => language;
export function useLanguage() {
  return useSyncExternalStore((listener) => {
    listeners.add(listener);
    return () => {
      listeners.delete(listener);
    };
  }, getLanguage);
}
export function setLanguage(next: Language) {
  language = next;
  localStorage.setItem("hotelops-language", next);
  document.documentElement.lang = next;
  listeners.forEach((listener) => listener());
}
const screenNames = [
  "Đăng nhập",
  "Quên mật khẩu",
  "Đổi mật khẩu",
  "Hồ sơ cá nhân",
  "Tổng quan",
  "Trung tâm thông báo",
  "Chi tiết thông báo",
  "Danh sách nhân viên",
  "Chi tiết nhân viên",
  "Danh sách vai trò",
  "Chi tiết vai trò",
  "Ma trận quyền",
  "Danh sách chi nhánh",
  "Chi tiết chi nhánh",
  "Danh sách tầng / khu vực",
  "Chi tiết tầng / khu vực",
  "Danh sách loại phòng",
  "Chi tiết loại phòng",
  "Danh sách phòng",
  "Chi tiết phòng",
  "Danh sách bộ phận",
  "Chi tiết bộ phận",
  "Bảng trạng thái phòng",
  "Danh sách công việc vệ sinh",
  "Tạo công việc vệ sinh",
  "Chi tiết công việc vệ sinh",
  "Phân công vệ sinh",
  "Danh sách công việc khắc phục",
  "Chi tiết công việc khắc phục",
  "Lịch sử chuẩn bị phòng",
  "Danh sách bộ tiêu chí kiểm tra",
  "Chi tiết bộ tiêu chí kiểm tra",
  "Hàng đợi kiểm tra",
  "Kiểm tra phòng",
  "Kết quả kiểm tra",
  "Yêu cầu khắc phục",
  "Kiểm tra lại",
  "Lịch sử kiểm tra",
  "Danh sách sự cố",
  "Báo cáo sự cố",
  "Chi tiết sự cố",
  "Phân công bảo trì",
  "Danh sách công việc bảo trì",
  "Chi tiết công việc bảo trì",
  "Danh sách lịch bảo trì định kỳ",
  "Chi tiết lịch bảo trì định kỳ",
  "Dữ liệu tham chiếu",
  "Danh sách tài sản",
  "Chi tiết tài sản",
  "Lịch sử bảo trì tài sản",
  "Danh sách kho",
  "Chi tiết kho",
  "Tổng quan tồn kho",
  "Danh sách vật tư",
  "Chi tiết vật tư",
  "Đơn vị tính",
  "Danh sách giao dịch kho",
  "Chi tiết giao dịch kho",
  "Danh sách kiểm kê",
  "Chi tiết kiểm kê",
  "Số dư và sổ kho",
  "Danh sách yêu cầu vật tư",
  "Chi tiết yêu cầu vật tư",
  "Danh sách đề nghị bổ sung",
  "Chi tiết đề nghị bổ sung",
  "Danh sách công việc",
  "Chi tiết công việc",
  "Phân công công việc",
  "Khối lượng công việc và nhật ký thời gian",
  "Công việc quá hạn",
  "Quản lý ca làm việc",
  "Chi tiết ca làm việc",
  "Nhật ký ngày làm việc",
  "Danh sách bàn giao ca",
  "Chi tiết bàn giao ca",
  "Danh sách yêu cầu nội bộ",
  "Chi tiết yêu cầu nội bộ",
  "Báo cáo vận hành",
  "Nhật ký hoạt động",
  "Chi tiết nhật ký hoạt động",
];
export const vi: Record<string, string> = {
  Open: "Mở",
  "Copy link": "Sao chép liên kết",
  "Link copied": "Đã sao chép liên kết",
  Screens: "Màn hình",
  "Design system": "Hệ thống thiết kế",
  "Search screens": "Tìm kiếm màn hình",
  "All features": "Tất cả tính năng",
  "All types": "Tất cả loại màn hình",
  "All modules": "Tất cả phân hệ",
  "Screen library": "Thư viện màn hình",
  "Public frontend prototype": "Prototype giao diện công khai",
  "Review all 80 screens with synthetic hotel operations data.":
    "Xem đủ 80 màn hình với dữ liệu vận hành khách sạn mô phỏng.",
  Authentication: "Tài khoản",
  ADMINISTRATION: "QUẢN TRỊ",
  OPERATIONS: "VẬN HÀNH",
  HOUSEKEEPING: "BUỒNG PHÒNG",
  MAINTENANCE: "BẢO TRÌ",
  INVENTORY: "KHO VẬT TƯ",
  "TASKS & SHIFTS": "CÔNG VIỆC VÀ CA",
  REPORTING: "BÁO CÁO",
  Active: "Hoạt động",
  Inactive: "Ngừng hoạt động",
  Draft: "Nháp",
  Published: "Đã xuất bản",
  Retired: "Ngừng sử dụng",
  Pending: "Chờ xử lý",
  Assigned: "Đã phân công",
  "In Progress": "Đang thực hiện",
  Completed: "Hoàn thành",
  Closed: "Đã đóng",
  Cancelled: "Đã hủy",
  Rejected: "Đã từ chối",
  Approved: "Đã duyệt",
  Ready: "Sẵn sàng",
  Blocked: "Tạm khóa",
  "Pending Cleaning": "Chờ vệ sinh",
  "Pending Inspection": "Chờ kiểm tra",
  "Pending Assignment": "Chờ phân công",
  "Rework Required": "Cần khắc phục",
  "Pending Reinspection": "Chờ kiểm tra lại",
  Inspecting: "Đang kiểm tra",
  Passed: "Đạt",
  Failed: "Không đạt",
  Pass: "Đạt",
  Fail: "Không đạt",
  Fixed: "Đã khắc phục",
  Unread: "Chưa đọc",
  Read: "Đã đọc",
  Received: "Đã nhận",
  Posted: "Đã ghi sổ",
  Reconciled: "Đã đối soát",
  Submitted: "Đã gửi",
  Accepted: "Đã tiếp nhận",
  Working: "Đang làm việc",
  Ended: "Đã kết thúc",
  Recorded: "Đã ghi nhận",
  High: "Cao",
  Medium: "Trung bình",
  Low: "Thấp",
  Critical: "Nghiêm trọng",
  Normal: "Bình thường",
  "On Hold": "Tạm dừng",
  "Fully Issued": "Đã xuất đủ",
  "Partially Issued": "Đã xuất một phần",
  Save: "Lưu",
  "Saving…": "Đang lưu…",
  Cancel: "Hủy",
  Create: "Tạo mới",
  Edit: "Chỉnh sửa",
  Delete: "Xóa",
  Assign: "Phân công",
  Approve: "Duyệt",
  Reject: "Từ chối",
  Start: "Bắt đầu",
  Complete: "Hoàn thành",
  Search: "Tìm kiếm",
  Reset: "Đặt lại",
  Previous: "Trước",
  Next: "Tiếp",
  Overview: "Tổng quan",
  Activity: "Hoạt động",
  Evidence: "Minh chứng",
  Name: "Tên",
  Code: "Mã",
  Status: "Trạng thái",
  Branch: "Chi nhánh",
  Department: "Bộ phận",
  Priority: "Ưu tiên",
  Deadline: "Hạn hoàn thành",
  Created: "Ngày tạo",
  "Created at": "Ngày tạo",
  Updated: "Cập nhật",
  Assignee: "Người thực hiện",
  "Assigned to": "Người thực hiện",
  Description: "Mô tả",
  Reason: "Lý do",
  Notes: "Ghi chú",
  Quantity: "Số lượng",
  Unit: "Đơn vị",
  Item: "Vật tư",
  Room: "Phòng",
  Location: "Vị trí",
  Warehouse: "Kho",
  Type: "Loại",
  Total: "Tổng số",
  Balance: "Số dư",
  Available: "Khả dụng",
  Minimum: "Tối thiểu",
  "All branches": "Tất cả chi nhánh",
  "All statuses": "Tất cả trạng thái",
  "All priorities": "Tất cả mức ưu tiên",
  "All departments": "Tất cả bộ phận",
  Actions: "Thao tác",
  "Select item": "Chọn vật tư",
  "Select result": "Chọn kết quả",
  Results: "Kết quả",
  Checklist: "Bộ tiêu chí",
  Criteria: "Tiêu chí",
  History: "Lịch sử",
  Today: "Hôm nay",
  Overdue: "Quá hạn",
  "Due today": "Đến hạn hôm nay",
  "Start workday": "Bắt đầu ngày làm việc",
  "End workday": "Kết thúc ngày làm việc",
  "Start work": "Bắt đầu công việc",
  "Pause work": "Tạm dừng công việc",
  "Complete work": "Hoàn thành công việc",
  "Start cleaning": "Bắt đầu vệ sinh",
  "Complete cleaning": "Hoàn thành vệ sinh",
  "Start inspection": "Bắt đầu kiểm tra",
  "Finish inspection": "Hoàn tất kiểm tra",
  "Record results": "Ghi nhận kết quả",
  "Request rework": "Yêu cầu khắc phục",
  "Submit rework": "Gửi kết quả khắc phục",
  "Conclude reinspection": "Kết luận kiểm tra lại",
  "Post receipt": "Ghi sổ nhập kho",
  "Post return": "Ghi sổ hoàn trả",
  "Confirm receipt": "Xác nhận nhận hàng",
  Administrator: "Quản trị viên",
  "Operations Manager": "Quản lý vận hành",
  Owner: "Chủ khách sạn",
  "Housekeeping Staff": "Nhân viên buồng phòng",
  "Housekeeping Supervisor": "Giám sát buồng phòng",
  "Maintenance Staff": "Nhân viên bảo trì",
  "Maintenance Lead": "Trưởng nhóm bảo trì",
  "Department Head": "Trưởng bộ phận",
  Storekeeper: "Thủ kho",
  Operations: "Vận hành",
  Housekeeping: "Buồng phòng",
  Maintenance: "Bảo trì",
  "Front Office": "Lễ tân",
  Security: "An ninh",
  "Food & Beverage": "Ẩm thực",
  "No matching records": "Không có kết quả phù hợp",
  "Adjust your filters or search to find records.":
    "Thay đổi bộ lọc hoặc từ khóa để tìm bản ghi.",
  "A reason of at least 10 characters is required.":
    "Vui lòng nhập lý do ít nhất 10 ký tự.",
  "Passwords must match.": "Mật khẩu xác nhận phải trùng khớp.",
  "Progress must be between 0 and 100.": "Tiến độ phải từ 0 đến 100.",
  "Record changed. Reload before retrying.":
    "Bản ghi đã thay đổi. Vui lòng tải lại.",
};
Object.assign(vi, viExtra, fieldsVi, metricsVi, workflowsVi);
catalog.screens.forEach((screen, index) => {
  vi[screen.name] = screenNames[index];
  vi[screen.description] = screenDescriptionsVi[index];
  vi[screen.name + " · related activity"] =
    screenNames[index] + " · hoạt động liên quan";
});
export function t(value: string): string {
  if (typeof value !== "string") return value == null ? "" : String(value);
  if (language === "en" || !value) return value;
  if (vi[value]) return vi[value];
  const caseKey = Object.keys(vi).find(
    (key) => key.toLowerCase() === value.toLowerCase(),
  );
  if (caseKey) return vi[caseKey];
  if (value.endsWith(" records")) return `${t(value.slice(0, -8))} · bản ghi`;
  for (const prefix of ["Active ", "Assigned ", "Completed ", "Linked "])
    if (value.startsWith(prefix))
      return `${t(value.slice(prefix.length))} ${t(prefix.trim())}`;
  if (value.endsWith(" is required."))
    return `${t(value.slice(0, -13))} là bắt buộc.`;
  const ready = /^(\d+) of (\d+) rooms ready$/.exec(value);
  if (ready) return `${ready[1]} / ${ready[2]} phòng sẵn sàng`;
  if (/^Room \d+$/.test(value)) return value.replace("Room", "Phòng");
  if (/^Floor \d+$/.test(value)) return value.replace("Floor", "Tầng");
  // Dynamic labels retain record names and business identifiers unchanged.
  for (const prefix of ["Create ", "Edit ", "Select ", "All ", "Total "])
    if (value.startsWith(prefix))
      return `${t(prefix.trim())} ${t(value.slice(prefix.length))}`;
  return value
    .split(" · ")
    .map((part) => vi[part] || part)
    .join(" · ");
}
export function LanguageSwitcher() {
  const selected = useLanguage();
  return (
    <div className="language-switch" aria-label="Language / Ngôn ngữ">
      {(["en", "vi"] as const).map((lang) => (
        <button
          key={lang}
          type="button"
          aria-pressed={selected === lang}
          onClick={() => setLanguage(lang)}
        >
          {lang.toUpperCase()}
        </button>
      ))}
    </div>
  );
}
