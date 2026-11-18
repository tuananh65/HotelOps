import { Link, Navigate, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { RevokeUserRoleCandidate } from "./RevokeUserRoleCandidate";

export function RevokeUserRoleFlowBoundary() {
  const { state, actions } = useAuth();
  const { id, assignmentId } = useParams();
  if (state.status === "ANONYMOUS" || state.status === "EXPIRED_INVALID") return <Navigate replace to="/login" />;
  if (state.status === "RESTRICTED") return <Navigate replace to="/change-password" />;
  if (state.status !== "AUTHENTICATED" || !state.session) return <main>
    <p role="status">Xác minh phiên trước khi xem quyền vai trò. Không gửi lại thay đổi đang chờ.</p>
    <button type="button" onClick={() => void actions.revalidate()}>Kiểm tra phiên đăng nhập</button>
  </main>;
  const number = (v: string | undefined) => v && /^\d+$/.test(v) ? Number(v) : NaN;
  const user = number(id), assignment = number(assignmentId);
  if (![user, assignment].every(v => Number.isSafeInteger(v) && v > 0)) return <p role="alert">Mã phân công vai trò không hợp lệ.</p>;
  return <><Link to={`/users/${user}/roles/assign`}>Quay lại vai trò</Link>
    <RevokeUserRoleCandidate userId={user} assignmentId={assignment} dispatchEnabled /></>;
}
