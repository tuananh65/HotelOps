import { LoginRoute } from "./LoginRoute";
import { ChecklistAuthRoute } from "./ChecklistAuthRoute";
import { loginDestination } from "./returnUrl";
import { useRef } from "react";
import { type ReactNode } from "react";
import { Link, Navigate, Route, Routes, useLocation } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { AuthFormRoute, Recovery, ForgotPasswordScreen } from "./AuthForms";
import { OperationsRoutes } from "./OperationsRoutes";
// Disabled destinations deliberately have no owner business imports.
function OwnerDestination() {
  return <p role="alert">This destination is not activated.</p>;
}
function Protected({ children }: { children: ReactNode }) {
  const { state } = useAuth();
  const location = useLocation();
  const identity = useRef(state.authorityGeneration);
  const revision = useRef(0);
  if (identity.current !== state.authorityGeneration) {
    identity.current = state.authorityGeneration;
    ++revision.current;
  }
  if (state.status === "INITIALIZING" || state.status === "ERROR_UNAVAILABLE")
    return <Recovery />;
  if (state.status === "RESTRICTED")
    return <Navigate replace to="/change-password" />;
  if (state.status !== "AUTHENTICATED" || !state.session)
    return (
      <Navigate
        replace
        to={loginDestination(location.pathname, location.search)}
      />
    );
  return <div key={revision.current}>{children}</div>;
}
/** Local candidate only. Server APIs authorize resources independently of route admission. */
export function SharedAuthHost({
  checklistEnabled = false,
}: { checklistEnabled?: boolean } = {}) {
  return (
    <Routes>
      <Route
        path="/inspections/checklists"
        element={<ChecklistAuthRoute enabled={checklistEnabled} />}
      />
      <Route
        path="/inspections/:branch/:id/execution"
        element={
          <Protected>
            <OwnerDestination />
          </Protected>
        }
      />
      <Route path="/login" element={<LoginRoute />} />
      <Route
        path="/profile"
        element={<AuthFormRoute key="profile" kind="profile" />}
      />
      <Route
        path="/change-password"
        element={<AuthFormRoute key="changePassword" kind="changePassword" />}
      />
      <Route path="/forgot-password" element={<ForgotPasswordScreen />} />
      <Route
        path="/dashboard"
        element={
          <Protected>
            <Navigate replace to="/rooms/status" />
          </Protected>
        }
      />
      <Route
        path="/inspections/assignment"
        element={
          <Protected>
            <OwnerDestination />
          </Protected>
        }
      />
      <Route
        path="/incidents/new"
        element={
          <Protected>
            <OwnerDestination />
          </Protected>
        }
      />
      <Route
        path="*"
        element={
          <Protected>
            <Link to="/profile">Profile</Link>
            <OperationsRoutes />
          </Protected>
        }
      />
    </Routes>
  );
}
