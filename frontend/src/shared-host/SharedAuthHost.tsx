import { InitialInspectionAssignmentPage } from "../features/inspectionExecution/InitialInspectionAssignmentPage";
import { PendingInspectionsPage } from "../features/inspectionExecution/PendingInspectionsPage";
import { AssignedReworkRead } from "../features/inspectionExecution/AssignedReworkRead";
import { ReinspectionAssignmentPage } from "../features/inspectionExecution/ReinspectionAssignmentPage";
import { ReinspectionExecutionPage } from "../features/inspectionExecution/ReinspectionExecutionPage";
import { ReinspectionOriginalPage } from "../features/inspectionExecution/ReinspectionOriginalPage";
import { NotificationCenter } from "../notifications/NotificationCenter";
import { TaskReadPage } from "../operationalTasks/TaskReadPage";
import { UsersRouteBoundary } from "../users/UsersRouteBoundary";
import { UserDirectoryPage } from "../users/UserDirectoryPage";
import { UserAccessReviewPage } from "../users/UserAccessReviewPage";
import { BranchStructureTree } from "../hotel/BranchStructureTree";
import { AuditPage } from "../audit/AuditPage";
import { DashboardPage } from "../insights/DashboardPage";
import { AssignUserRoleFlowBoundary } from "../users/AssignUserRoleFlowBoundary";
import { ActivateUserFlowBoundary } from "../users/ActivateUserFlowBoundary";
import { LoginRoute } from "./LoginRoute";
import { ChecklistAuthRoute } from "./ChecklistAuthRoute";
import { InspectionExecutionAuthRoute } from "./InspectionExecutionAuthRoute";
import { InspectionHistoryAuthRoute } from "./InspectionHistoryAuthRoute";
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
  usersReadEnabled = false,
  structureReadEnabled = false,
  auditReadEnabled = false,
  roleAssignmentEnabled = false,
  accountActivationEnabled = false,
  dashboardEnabled = false,
  notificationsEnabled = false,
  inspectionExecutionEnabled = false,
  operationalTasksEnabled = false,
}: {
  checklistEnabled?: boolean;
  usersReadEnabled?: boolean;
  structureReadEnabled?: boolean;
  auditReadEnabled?: boolean;
  roleAssignmentEnabled?: boolean;
  accountActivationEnabled?: boolean;
  dashboardEnabled?: boolean;
  notificationsEnabled?: boolean;
  inspectionExecutionEnabled?: boolean;
  operationalTasksEnabled?: boolean;
} = {}) {
  return (
    <Routes>
      {(notificationsEnabled || inspectionExecutionEnabled) && (
        <Route
          path="/inspection-history"
          element={<Protected><InspectionHistoryAuthRoute /></Protected>}
        />
      )}
      {(notificationsEnabled || inspectionExecutionEnabled) && (
        <Route
          path="/inspection-history/:id"
          element={
            <Protected>
              <InspectionHistoryAuthRoute />
            </Protected>
          }
        />
      )}
      {operationalTasksEnabled && (
        <Route
          path="/operational-tasks"
          element={
            <Protected>
              <TaskReadPage />
            </Protected>
          }
        />
      )}
      {accountActivationEnabled && (
        <Route
          path="/users/:id/activate"
          element={<ActivateUserFlowBoundary />}
        />
      )}
      {roleAssignmentEnabled && (
        <Route
          path="/users/:id/roles/assign"
          element={<AssignUserRoleFlowBoundary />}
        />
      )}
      {auditReadEnabled && (
        <Route
          path="/audit"
          element={
            <Protected>
              <AuditPage />
            </Protected>
          }
        />
      )}
      {usersReadEnabled && (
        <Route element={<UsersRouteBoundary />}>
          <Route path="/users" element={<UserDirectoryPage />} />
          <Route
            path="/users/:id"
            element={
              <UserAccessReviewPage
                roleAssignmentEnabled={roleAssignmentEnabled}
                accountActivationEnabled={accountActivationEnabled}
              />
            }
          />
        </Route>
      )}
      {structureReadEnabled && (
        <Route path="/hotel" element={<BranchStructureTree />} />
      )}
      <Route
        path="/inspections/checklists"
        element={<ChecklistAuthRoute enabled={checklistEnabled} />}
      />
      <Route
        path="/inspections/:branch/:id/execution"
        element={
          inspectionExecutionEnabled ? (
            <InspectionExecutionAuthRoute />
          ) : (
            <Protected>
              <OwnerDestination />
            </Protected>
          )
        }
      />
      {notificationsEnabled && (
        <Route
          path="/reinspections/:branch/:id/original"
          element={
            <Protected>
              <ReinspectionOriginalPage />
            </Protected>
          }
        />
      )}
      {notificationsEnabled && (
        <Route
          path="/reinspections/:branch/:id/execution"
          element={
            <Protected>
              <ReinspectionExecutionPage />
            </Protected>
          }
        />
      )}
      {notificationsEnabled && (
        <Route
          path="/inspections/reworks/:id/assign"
          element={
            <Protected>
              <ReinspectionAssignmentPage />
            </Protected>
          }
        />
      )}
      {notificationsEnabled && (
        <Route
          path="/cleaning/reworks/:id"
          element={
            <Protected>
              <AssignedReworkRead />
            </Protected>
          }
        />
      )}
      {notificationsEnabled && (
        <Route
          path="/notifications"
          element={
            <Protected>
              <NotificationCenter />
            </Protected>
          }
        />
      )}
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
            {dashboardEnabled ? (
              <DashboardPage />
            ) : (
              <Navigate replace to="/rooms/status" />
            )}
          </Protected>
        }
      />
      <Route
        path="/inspections/cleaning-tasks/:taskId/assign"
        element={
          <Protected>
            {inspectionExecutionEnabled ? (
              <InitialInspectionAssignmentPage />
            ) : (
              <OwnerDestination />
            )}
          </Protected>
        }
      />
      <Route
        path="/inspections/assignment"
        element={
          <Protected>
            {inspectionExecutionEnabled ? (
              <PendingInspectionsPage />
            ) : (
              <OwnerDestination />
            )}
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
            {(inspectionExecutionEnabled || notificationsEnabled) && <Link to="/inspection-history">Inspection history</Link>}
            {usersReadEnabled && <Link to="/users">Users</Link>}
            {structureReadEnabled && <Link to="/hotel">Structure</Link>}
            {auditReadEnabled && <Link to="/audit">Audit history</Link>}
            {dashboardEnabled && <Link to="/dashboard">Dashboard</Link>}
            {operationalTasksEnabled && (
              <Link to="/operational-tasks">Operational tasks</Link>
            )}
            {notificationsEnabled && (
              <Link to="/notifications">Notifications</Link>
            )}
            <OperationsRoutes />
          </Protected>
        }
      />
    </Routes>
  );
}
