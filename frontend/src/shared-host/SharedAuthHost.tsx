import { lazy, Suspense } from "react";
import { type ReactNode } from "react";
import { Link, Navigate, Route, Routes, useLocation } from "react-router-dom";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { AuthFormRoute, Recovery, ForgotPasswordScreen } from "./AuthForms";
import { OperationsRoutes } from "./OperationsRoutes";
import { parseAssignmentContext } from "../features/inspectionAssignment/navigation";
import { parseIncidentContext } from "../incidents/navigation";
const Assignment = lazy(() =>
  import("../features/inspectionAssignment/InspectionAssignmentDestination").then(
    (m) => ({ default: m.InspectionAssignmentDestination }),
  ),
);
const Execution = lazy(() =>
  import("../inspection/ExecutionPage").then((m) => ({
    default: m.ExecutionPage,
  })),
);
const Incident = lazy(() => import("../incidents/ReportIncidentRoute"));
// Release constants, not authorization. No environment or URL override in this candidate.
const destinationFeatures: Readonly<{
  inspection: boolean;
  incident: boolean;
}> = Object.freeze({ inspection: false, incident: false });
function OwnerDestination({
  kind,
}: {
  kind: "inspection" | "incident" | "execution";
}) {
  const navigate = useNavigate();
  if (kind === "inspection")
    return (
      <Suspense fallback={<p role="status">Loading destination...</p>}>
        <Assignment
          enabled={false}
          onOpenInspection={(branchId, inspectionId) =>
            navigate(`/inspections/${branchId}/${inspectionId}/execution`)
          }
        />
      </Suspense>
    );
  if (kind === "execution" && destinationFeatures.inspection)
    return (
      <Suspense fallback={<p role="status">Loading destination...</p>}>
        <Execution />
      </Suspense>
    );
  if (kind === "incident" && destinationFeatures.incident)
    return (
      <Suspense fallback={<p role="status">Loading destination...</p>}>
        <Incident />
      </Suspense>
    );
  return (
    <DisabledDestination
      kind={kind === "incident" ? "incident" : "inspection"}
    />
  );
}
function Protected({ children }: { children: ReactNode }) {
  const { state } = useAuth();
  if (state.status === "INITIALIZING" || state.status === "ERROR_UNAVAILABLE")
    return <Recovery />;
  if (state.status === "RESTRICTED")
    return <Navigate replace to="/change-password" />;
  if (state.status !== "AUTHENTICATED" || !state.session)
    return <Navigate replace to="/login" />;
  return (
    <div key={state.session.user.id + ":" + state.session.expiresAt}>
      {children}
    </div>
  );
}
function DisabledDestination({ kind }: { kind: "inspection" | "incident" }) {
  const { search } = useLocation();
  let valid = true;
  try {
    valid =
      kind === "inspection"
        ? parseAssignmentContext(search) !== null
        : !!parseIncidentContext(search);
  } catch {
    valid = false;
  }
  return (
    <p role="alert">
      {valid
        ? "This destination is not activated."
        : "Invalid destination context."}
    </p>
  );
}
/** Local candidate only. Server APIs authorize resources independently of route admission. */
export function SharedAuthHost() {
  return (
    <Routes>
      <Route
        path="/inspections/:branch/:id/execution"
        element={
          <Protected>
            <OwnerDestination kind="execution" />
          </Protected>
        }
      />
      <Route
        path="/login"
        element={<AuthFormRoute key="login" kind="login" />}
      />
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
            <OwnerDestination kind="inspection" />
          </Protected>
        }
      />
      <Route
        path="/incidents/new"
        element={
          <Protected>
            <OwnerDestination kind="incident" />
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
