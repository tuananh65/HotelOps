import { Link, Navigate, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { useActivateUser } from "./useActivateUser";
import { ActivateUserPage } from "./ActivateUserPage";

export function ActivateUserFlowBoundary() {
  const { state, actions } = useAuth();
  const { id } = useParams();
  const numeric = id !== undefined && /^\d+$/.test(id) ? Number(id) : NaN;
  const target = Number.isSafeInteger(numeric) && numeric > 0 ? numeric : null;
  const flow = useActivateUser(target);
  if (state.status === "ANONYMOUS" || state.status === "EXPIRED_INVALID")
    return <Navigate replace to="/login" />;
  if (state.status === "RESTRICTED")
    return <Navigate replace to="/change-password" />;
  if (state.status !== "AUTHENTICATED" || !state.session)
    return (
      <main aria-label="Activation session recovery">
        <p role="status">
          {state.status === "INITIALIZING"
            ? "Checking your session..."
            : "Your session could not be confirmed."}
        </p>
        <p>
          Account data is hidden. A pending activation may already have
          committed. No activation has been retried.
        </p>
        {state.status !== "INITIALIZING" && (
          <button type="button" onClick={() => void actions.revalidate()}>
            Check session
          </button>
        )}
        <Link to="/profile">Profile</Link>
      </main>
    );
  if (target === null)
    return (
      <main>
        <h1>Invalid user ID</h1>
        <p role="alert">A positive safe integer user ID is required.</p>
        <Link to="/profile">Profile</Link>
      </main>
    );
  return <ActivateUserPage target={target} flow={flow} />;
}
