import { useRef } from "react";
import { Link, Navigate } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { CreateUserPage } from "./CreateUserPage";
import { useCreateUser } from "./useCreateUser";

export function CreateUserFlowBoundary() {
  const { state, actions } = useAuth();
  const flow = useCreateUser();
  const previousOwner = useRef(state.session);
  const revision = useRef(0);
  if (previousOwner.current !== state.session) {
    previousOwner.current = state.session;
    ++revision.current;
  }
  if (state.status === "ANONYMOUS" || state.status === "EXPIRED_INVALID")
    return <Navigate replace to="/login" />;
  if (state.status === "RESTRICTED")
    return <Navigate replace to="/change-password" />;
  if (state.status !== "AUTHENTICATED" || state.session === null)
    return (
      <main aria-label="Khôi phục phiên tạo tài khoản">
        <p role="status">
          {state.status === "INITIALIZING"
            ? "Đang kiểm tra phiên đăng nhập của bạn..."
            : "Không xác minh được phiên đăng nhập của bạn."}
        </p>
        <p>
          Chưa gửi lại yêu cầu tạo tài khoản. Kết quả đang chờ có thể chưa xác định. Mật khẩu đã ẩn và không thể lấy lại tại đây.
        </p>
        {state.status !== "INITIALIZING" && (
          <button type="button" onClick={() => void actions.revalidate()}>
            Kiểm tra phiên đăng nhập
          </button>
        )}
        <Link to="/profile">Hồ sơ cá nhân</Link>
      </main>
    );
  // A new PublicSession reference replaces the entire secret-bearing leaf, even for the same user ID.
  return <CreateUserPage key={revision.current} flow={flow} />;
}
