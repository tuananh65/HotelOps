import { type ReactNode } from "react";
import { Link, Navigate, Route, Routes, useLocation } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { LoginRoute } from "./LoginRoute";
import { loginDestination } from "./returnUrl";
import { AuthFormRoute, Recovery, ForgotPasswordScreen } from "./AuthForms";
import { OperationsRoutes } from "./OperationsRoutes";
import { ChecklistAuthRoute } from "./ChecklistAuthRoute";

/** Isolated source candidate, never mounted by current production main.tsx. */
function Protected({ children }: { children: ReactNode }) {
  const { state } = useAuth();
  const location = useLocation();
  if (state.status === "INITIALIZING" || state.status === "ERROR_UNAVAILABLE")
    return <Recovery />;
  if (state.status === "RESTRICTED")
    return <Navigate replace to="/change-password" />;
  if (state.status !== "AUTHENTICATED" || !state.session)
    return <Navigate replace to={loginDestination(location.pathname, location.search)} />;
  return <div key={state.authorityGeneration}>{children}</div>;
}
export function SharedAuthHost({ checklistEnabled = false }: { checklistEnabled?: boolean } = {}) {
  return (
    <Routes>
      <Route path="/login" element={<LoginRoute />} />
      <Route path="/profile" element={<AuthFormRoute key="profile" kind="profile" />} />
      <Route path="/change-password" element={<AuthFormRoute key="changePassword" kind="changePassword" />} />
      <Route path="/forgot-password" element={<ForgotPasswordScreen />} />
      <Route path="/dashboard" element={<Protected><Navigate replace to="/rooms/status" /></Protected>} />
      <Route path="/inspections/checklists" element={<ChecklistAuthRoute enabled={checklistEnabled} />} />
      <Route path="*" element={<Protected><Link to="/profile">Profile</Link><OperationsRoutes /></Protected>} />
    </Routes>
  );
}
