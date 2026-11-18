import { Link, Navigate, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { UserDeactivationCandidate } from "./UserDeactivationCandidate";

export function DeactivateUserFlowBoundary() {
  const { state, actions } = useAuth();
  const { id } = useParams();
  if (state.status === "ANONYMOUS" || state.status === "EXPIRED_INVALID") return <Navigate replace to="/login" />;
  if (state.status === "RESTRICTED") return <Navigate replace to="/change-password" />;
  if (state.status !== "AUTHENTICATED" || !state.session) return <main>
    <p role="status">Xác minh phiên hiện tại trước khi thay đổi tài khoản.</p>
    <button type="button" onClick={() => void actions.revalidate()}>Kiểm tra phiên đăng nhập</button>
  </main>;
  const target = id && /^\d+$/.test(id) ? Number(id) : NaN;
  if (!Number.isSafeInteger(target) || target <= 0) return <p role="alert">Mã tài khoản không hợp lệ.</p>;
  return <><Link to={`/users/${target}`}>Quay lại tài khoản</Link><UserDeactivationCandidate userId={target} dispatchEnabled /></>;
}
