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
      <main aria-label="Update profile session recovery">
        <p role="status">
          {state.status === "INITIALIZING"
            ? "Checking your session..."
            : "Your session could not be confirmed."}
        </p>
        <p>
          Profile data is hidden. Any pending update may already have been
          saved. No update has been retried.
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
  return <UpdateUserPage target={target} flow={flow} />;
}
