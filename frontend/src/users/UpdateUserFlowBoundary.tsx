import { Link, Navigate, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { UpdateUserPage } from "./UpdateUserPage";
import { useUpdateUserProfile } from "./useUpdateUserProfile";

export function UpdateUserFlowBoundary() {
  const { state, actions } = useAuth();
  const { id } = useParams();
  const numeric = id !== undefined && /^\d+$/.test(id) ? Number(id) : NaN;
  const target = Number.isSafeInteger(numeric) && numeric > 0 ? numeric : null;
  const flow = useUpdateUserProfile(target);
  if (state.status === "ANONYMOUS" || state.status === "EXPIRED_INVALID")
    return <Navigate replace to="/login" />;
  if (state.status === "RESTRICTED")
    return <Navigate replace to="/change-password" />;
  if (state.status !== "AUTHENTICATED" || !state.session)
    return (
      <main aria-label="Khôi phục phiên cập nhật hồ sơ">
        <p role="status">
          {state.status === "INITIALIZING"
            ? "Đang kiểm tra phiên đăng nhập của bạn..."
            : "Không xác minh được phiên đăng nhập của bạn."}
        </p>
        <p>
          Hồ sơ đã ẩn. Cập nhật đang chờ có thể đã lưu. Không gửi lại cập nhật.
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
  return <UpdateUserPage target={target} flow={flow} />;
}
