import type { EditableBranch } from "./contracts";
import { Building2, MapPin, Phone, Mail, Clock3 } from "lucide-react";
import "./branch-management.css";

export const branchLabels = {
  code: "Mã chi nhánh",
  name: "Tên chi nhánh",
  address: "Địa chỉ",
  phone: "Số điện thoại",
  email: "Email",
  reason: "Lý do thay đổi",
  timezone: "Múi giờ",
  isActive: "Trạng thái",
};

export function BranchSummary({ branch }: { branch: EditableBranch }) {
  return (
    <dl className="branch-summary">
      {(
        [
          "code",
          "name",
          "address",
          "phone",
          "email",
          "timezone",
          "isActive",
        ] as const
      ).map((field) => (
        <div
          key={field}
          className={field === "address" ? "branch-summary-wide" : undefined}
        >
          <dt>
            {field === "address" ? (
              <MapPin size={15} aria-hidden="true" />
            ) : field === "phone" ? (
              <Phone size={15} aria-hidden="true" />
            ) : field === "email" ? (
              <Mail size={15} aria-hidden="true" />
            ) : field === "timezone" ? (
              <Clock3 size={15} aria-hidden="true" />
            ) : field === "name" ? (
              <Building2 size={15} aria-hidden="true" />
            ) : null}
            {branchLabels[field]}
          </dt>
          <dd>
            {field === "isActive" ? (
              <span
                className={`branch-status ${branch.isActive ? "is-active" : ""}`}
              >
                {branch.isActive ? "Đang hoạt động" : "Ngừng hoạt động"}
              </span>
            ) : (
              branch[field] || "Chưa cung cấp"
            )}
          </dd>
        </div>
      ))}
    </dl>
  );
}

/** Only known public error codes are translated; never render raw server details. */
export async function branchReadError(response: Response): Promise<string> {
  const problem = await response.json().catch(() => null);
  if (response.status === 401)
    return "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
  if (response.status === 403) return "Bạn không có quyền quản lý chi nhánh.";
  if (response.status === 404)
    return "Không tìm thấy chi nhánh hoặc chức năng chưa khả dụng.";
  if (problem?.title === "BRANCH_ROWVERSION_SCHEMA_REQUIRED")
    return "Cơ sở dữ liệu chưa được cập nhật cho chức năng quản lý chi nhánh. Cần bổ sung cấu trúc phiên bản theo source trước khi tiếp tục.";
  return "Chưa tải được dữ liệu chi nhánh. Vui lòng thử lại; nếu lỗi tiếp tục, liên hệ quản trị viên kiểm tra cấu hình dữ liệu.";
}
