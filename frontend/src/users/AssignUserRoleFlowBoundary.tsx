import { Link, Navigate, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { useAssignUserRole } from "./useAssignUserRole";
import { AssignUserRolePage } from "./AssignUserRolePage";

export function AssignUserRoleFlowBoundary() {
  const { state, actions } = useAuth();
  const { id } = useParams();
  const numeric =
    id !== undefined && id.length > 0 && !/[^0-9]/.test(id) ? Number(id) : NaN;
  const target = Number.isSafeInteger(numeric) && numeric > 0 ? numeric : null;
  const flow = useAssignUserRole(target);
  if (state.status === "ANONYMOUS" || state.status === "EXPIRED_INVALID")
    return <Navigate replace to="/login" />;
  if (state.status === "RESTRICTED")
    return <Navigate replace to="/change-password" />;
  if (state.status !== "AUTHENTICATED" || !state.session)
    return (
      <main aria-label="Khôi phục phiên gán vai trò">
        <p role="status">
          {state.status === "INITIALIZING"
            ? "Đang kiểm tra phiên đăng nhập của bạn..."
            : "Không xác minh được phiên đăng nhập của bạn."}
        </p>
        <p>
          Thông tin tài khoản đã ẩn. Yêu cầu gán vai trò có thể đã lưu; không tự gửi lại.
        </p>
        {state.status !== "INITIALIZING" && (
          <button type="button" onClick={() => void actions.revalidate()}>
            Kiểm tra phiên đăng nhập
          </button>
        )}
        <Link to="/profile">Hồ sơ cá nhân</Link>
      </main>
    );
  if (target === null)
    return (
      <main>
        <h1>Mã người dùng không hợp lệ</h1>
        <p role="alert">Mã người dùng phải là số nguyên dương trong giới hạn cho phép.</p>
        <Link to="/profile">Hồ sơ cá nhân</Link>
      </main>
    );
  return <AssignUserRolePage target={target} flow={flow} />;
}
