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
      <main aria-label="Create account session recovery">
        <p role="status">
          {state.status === "INITIALIZING"
            ? "Checking your session..."
            : "Your session could not be confirmed."}
        </p>
        <p>
          Account creation has not been retried. Any pending creation outcome
          may be unknown. Credentials are hidden and cannot be restored here.
        </p>
        {state.status !== "INITIALIZING" && (
          <button type="button" onClick={() => void actions.revalidate()}>
            Check session
          </button>
        )}
        <Link to="/profile">Profile</Link>
      </main>
    );
  // A new PublicSession reference replaces the entire secret-bearing leaf, even for the same user ID.
  return <CreateUserPage key={revision.current} flow={flow} />;
}
