import { Link, Navigate, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { RevokeUserRoleCandidate } from "./RevokeUserRoleCandidate";

export function RevokeUserRoleFlowBoundary() {
  const { state, actions } = useAuth();
  const { id, assignmentId } = useParams();
  if (state.status === "ANONYMOUS" || state.status === "EXPIRED_INVALID") return <Navigate replace to="/login" />;
  if (state.status === "RESTRICTED") return <Navigate replace to="/change-password" />;
  if (state.status !== "AUTHENTICATED" || !state.session) return <main>
    <p role="status">Confirm your session before reviewing role access. Pending changes are never retried.</p>
    <button type="button" onClick={() => void actions.revalidate()}>Check session</button>
  </main>;
  const number = (v: string | undefined) => v && /^\d+$/.test(v) ? Number(v) : NaN;
  const user = number(id), assignment = number(assignmentId);
  if (![user, assignment].every(v => Number.isSafeInteger(v) && v > 0)) return <p role="alert">Invalid role assignment identifier.</p>;
  return <><Link to={`/users/${user}/roles/assign`}>Back to roles</Link>
    <RevokeUserRoleCandidate userId={user} assignmentId={assignment} dispatchEnabled /></>;
}
