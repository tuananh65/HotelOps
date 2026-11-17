import { ShiftHandoverPage } from "../workday/ShiftHandoverPage";
import { HomeNavigation } from "./HomeNavigation";
import { EmployeeMembershipPage } from "../users/EmployeeMembershipPage";
import { SecureShiftPlanWorkspace } from "../workday/SecureShiftPlanWorkspace";
import { ReferenceCatalogPage } from "../operationalTasks/ReferenceCatalogPage";
import { PrioritySlaPage } from "../operationalTasks/PrioritySlaPage";
import { WorkCategoriesPage } from "../operationalTasks/WorkCategoriesPage";
import { WorkdayRoute } from "../workday/WorkdayRoute";
import { TimeCorrectionsPage } from "../workTime/TimeCorrectionsPage";
import { MyTimeLogPage } from "../workTime/MyTimeLogPage";
import { InitialInspectionAssignmentPage } from "../features/inspectionExecution/InitialInspectionAssignmentPage";
import { PendingInspectionsPage } from "../features/inspectionExecution/PendingInspectionsPage";
import { AssignedReworkRead } from "../features/inspectionExecution/AssignedReworkRead";
import { ReinspectionAssignmentPage } from "../features/inspectionExecution/ReinspectionAssignmentPage";
import { ReinspectionExecutionPage } from "../features/inspectionExecution/ReinspectionExecutionPage";
import { ReinspectionOriginalPage } from "../features/inspectionExecution/ReinspectionOriginalPage";
import { NotificationCenter } from "../notifications/NotificationCenter";
import { CleaningCancellationReceipt } from "../notifications/CleaningCancellationReceipt";
import { MaintenanceAssignmentReceipt } from "../notifications/MaintenanceAssignmentReceipt";
import { AssetConditionFollowup } from "../notifications/AssetConditionFollowup";
import { TaskReadPage } from "../operationalTasks/TaskReadPage";
import { UsersRouteBoundary } from "../users/UsersRouteBoundary";
import { UserDirectoryPage } from "../users/UserDirectoryPage";
import { UserAccessReviewPage } from "../users/UserAccessReviewPage";
import { BranchStructureTree } from "../hotel/BranchStructureTree";
import { RoomDirectoryRoute } from "../hotel/RoomDirectoryRoute";
import { StructureManagementRoute } from "../hotel/StructureManagementRoute";
import { AuditPage } from "../audit/AuditPage";
import { DashboardPage } from "../insights/DashboardPage";
import { ReportsPage, ReportAccessLink } from "../insights/ReportsPage";
import { InternalRequestHistoryPage, InternalRequestAccessLink } from "../internalRequests/HistoryPage";
import { HandoverReceiptPage } from "../internalRequests/HandoverReceiptPage";
import { InternalRequestIntakePage, InternalRequestIntakeLink } from "../internalRequests/IntakePage";
import { Dev2Workspace } from "../dev2/Workspace";
import { AssignUserRoleFlowBoundary } from "../users/AssignUserRoleFlowBoundary";
import { RevokeUserRoleFlowBoundary } from "../users/RevokeUserRoleFlowBoundary";
import { ActivateUserFlowBoundary } from "../users/ActivateUserFlowBoundary";
import { DeactivateUserFlowBoundary } from "../users/DeactivateUserFlowBoundary";
import { CreateUserFlowBoundary } from "../users/CreateUserFlowBoundary";
import { UpdateUserFlowBoundary } from "../users/UpdateUserFlowBoundary";
import { CredentialResetRoute } from "../users/CredentialResetRoute";
import { AddUserBranchFlowBoundary } from "../users/AddUserBranchFlowBoundary";
import { RoleCatalogRoute } from "../roles/RoleCatalogRoute";
import { RolePermissionEditor } from "../roles/RolePermissionEditor";
import { CustomRoleForm, CustomRoleEditRoute } from "../roles/CustomRoleForm";
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
  accountManagementEnabled = false,
  dashboardEnabled = false,
  notificationsEnabled = false,
  inspectionExecutionEnabled = false,
  operationalTasksEnabled = false,
  internalRequestsEnabled = false,
  incidentReadEnabled = false,
  materialReadEnabled = false,
  workdayReadEnabled = false,
}: {
  checklistEnabled?: boolean;
  usersReadEnabled?: boolean;
  structureReadEnabled?: boolean;
  auditReadEnabled?: boolean;
  roleAssignmentEnabled?: boolean;
  accountActivationEnabled?: boolean;
  accountManagementEnabled?: boolean;
  dashboardEnabled?: boolean;
  notificationsEnabled?: boolean;
  inspectionExecutionEnabled?: boolean;
  operationalTasksEnabled?: boolean;
  internalRequestsEnabled?: boolean;
  incidentReadEnabled?: boolean;
  materialReadEnabled?: boolean;
  workdayReadEnabled?: boolean;
} = {}) {
  return (
    <Routes>
      <Route path="/home" element={<Protected><main><h1>HotelOps</h1>
        <HomeNavigation />
        {dashboardEnabled && <ReportAccessLink />}
        {internalRequestsEnabled && <><InternalRequestAccessLink /><InternalRequestIntakeLink /></>}
      </main></Protected>} />
      {accountManagementEnabled && <Route path="/users/new" element={<CreateUserFlowBoundary />} />}
      {accountManagementEnabled && <Route path="/users/:id/edit" element={<UpdateUserFlowBoundary />} />}
      {accountManagementEnabled && <Route path="/users/:id/reset-credential" element={<CredentialResetRoute />} />}
      {accountManagementEnabled && <Route path="/users/:id/departments" element={<Protected><EmployeeMembershipPage /></Protected>} />}
      {accountManagementEnabled && <Route path="/users/:id/branches/add" element={<AddUserBranchFlowBoundary />} />}
      {accountManagementEnabled && <Route path="/users/:id/deactivate" element={<DeactivateUserFlowBoundary />} />}
      {roleAssignmentEnabled && <Route path="/roles" element={<RoleCatalogRoute mode="list" creationEnabled />} />}
      {roleAssignmentEnabled && <Route path="/roles/new" element={<CustomRoleForm dispatchEnabled />} />}
      {roleAssignmentEnabled && <Route path="/roles/:roleId" element={<RoleCatalogRoute mode="detail" creationEnabled />} />}
      {roleAssignmentEnabled && <Route path="/roles/:roleId/edit" element={<CustomRoleEditRoute />} />}
      {roleAssignmentEnabled && <Route path="/roles/:roleId/deactivate" element={<CustomRoleEditRoute deactivate />} />}
      {roleAssignmentEnabled && <Route path="/roles/:roleId/permissions" element={<RoleCatalogRoute mode="matrix" creationEnabled />} />}
      {roleAssignmentEnabled && <Route path="/roles/:roleId/permissions/edit" element={<RolePermissionEditor />} />}
      {roleAssignmentEnabled && <Route path="/permissions" element={<RoleCatalogRoute mode="catalog" />} />}
      {incidentReadEnabled && <Route path="/maintenance-assignment-receipts/:id" element={<Protected><MaintenanceAssignmentReceipt /></Protected>} />}
      {workdayReadEnabled && <Route path="/workday/handovers" element={<Protected><ShiftHandoverPage /></Protected>} />}
      {workdayReadEnabled && <Route path="/workday/handovers/:id" element={<Protected><ShiftHandoverPage /></Protected>} />}
      {workdayReadEnabled && <Route path="/workday/shift-plans" element={<Protected><SecureShiftPlanWorkspace saveEnabled /></Protected>} />}
      {workdayReadEnabled && <Route path="/workday/scheduled" element={<Protected><WorkdayRoute scheduled /></Protected>} />}
      {workdayReadEnabled && <Route path="/workday" element={<Protected><WorkdayRoute /></Protected>} />}
      {operationalTasksEnabled && <Route path="/time-corrections" element={<Protected><TimeCorrectionsPage /></Protected>} />}
      {operationalTasksEnabled && <Route path="/my-time-log" element={<Protected><MyTimeLogPage /></Protected>} />}
      {(incidentReadEnabled || materialReadEnabled) && <Route path="/operations/*" element={<Protected><Dev2Workspace
        enabledModules={[...(incidentReadEnabled ? ["incidents", "maintenance"] : []), ...(materialReadEnabled ? ["material-requests", "replenishments"] : [])]}
        enabledActions={["A004A-020", "A004A-021", "A004A-022", "A004A-023", "A004A-026", "A004A-027", "A004A-028", "A004A-029", "A004A-030", "A004A-031", "A004A-032", "A004A-034", "A004A-035", "A004A-036", "A004A-037", "A004A-038", "A004A-040", "A004A-041", "A004A-042", "A004A-043", "A004A-044", "A004A-045", "A004A-046", "A004A-047", "A004A-048", "A004A-049", "A004A-050", "A004A-051", "A004A-052", "A004A-053", "A004A-055", "A004A-056", "A004A-057", "A004A-058", "A004A-059"]} /></Protected>} />}
      {internalRequestsEnabled && <Route path="/internal-requests" element={<Protected><InternalRequestHistoryPage /></Protected>} />}
      {internalRequestsEnabled && <Route path="/internal-requests/:requestId/handovers/:handoverId" element={<Protected><HandoverReceiptPage /></Protected>} />}
      {internalRequestsEnabled && <Route path="/internal-requests/new" element={<Protected><InternalRequestIntakePage /></Protected>} />}
      {dashboardEnabled && <Route path="/reports" element={<Protected><ReportsPage /></Protected>} />}
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
      {roleAssignmentEnabled && <Route path="/users/:id/roles/:assignmentId/revoke" element={<RevokeUserRoleFlowBoundary />} />}
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
          <Route path="/users" element={<UserDirectoryPage accountManagementEnabled={accountManagementEnabled} />} />
          <Route
            path="/users/:id"
            element={
              <UserAccessReviewPage
                roleAssignmentEnabled={roleAssignmentEnabled}
                accountActivationEnabled={accountActivationEnabled}
                accountManagementEnabled={accountManagementEnabled}
              />
            }
          />
        </Route>
      )}
      {structureReadEnabled && (
        <>
          <Route path="/hotel" element={<BranchStructureTree />} />
          <Route path="/room-directory" element={<RoomDirectoryRoute />} />
          <Route path="/room-directory/:id" element={<RoomDirectoryRoute />} />
          <Route path="/structure/floors" element={<StructureManagementRoute kind="floors" />} />
          <Route path="/structure/floors/:id" element={<StructureManagementRoute kind="floors" />} />
          <Route path="/structure/areas" element={<StructureManagementRoute kind="areas" />} />
          <Route path="/structure/areas/:id" element={<StructureManagementRoute kind="areas" />} />
          <Route path="/structure/departments" element={<StructureManagementRoute kind="departments" />} />
          <Route path="/structure/departments/:id" element={<StructureManagementRoute kind="departments" />} />
          <Route path="/structure/reference-data" element={<Protected><ReferenceCatalogPage /></Protected>} />
          <Route path="/structure/priority-sla" element={<Protected><PrioritySlaPage /></Protected>} />
          <Route path="/structure/work-categories" element={<Protected><WorkCategoriesPage /></Protected>} />
          <Route path="/structure/room-types" element={<StructureManagementRoute kind="roomTypes" />} />
          <Route path="/structure/room-types/:id" element={<StructureManagementRoute kind="roomTypes" />} />
          <Route path="/structure/rooms" element={<StructureManagementRoute kind="rooms" />} />
          <Route path="/structure/rooms/:id" element={<StructureManagementRoute kind="rooms" />} />
        </>
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
      {notificationsEnabled && <Route path="/notifications/:id/cleaning-cancellation"
        element={<Protected><CleaningCancellationReceipt /></Protected>} />}
      {notificationsEnabled && <Route path="/notifications/:id/asset-condition"
        element={<Protected><AssetConditionFollowup /></Protected>} />}
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
            <HomeNavigation enabled={[
              "/profile", ...(usersReadEnabled ? ["/users"] : []),
              ...(structureReadEnabled ? ["/hotel", "/room-directory"] : []),
              ...(auditReadEnabled ? ["/audit"] : []), ...(dashboardEnabled ? ["/dashboard"] : []),
              ...(notificationsEnabled ? ["/notifications"] : []),
              ...(roleAssignmentEnabled ? ["/roles"] : []),
              ...(structureReadEnabled ? ["/structure/work-categories", "/structure/reference-data", "/structure/priority-sla"] : []),
              ...(incidentReadEnabled ? ["/operations/incidents"] : []),
              ...(materialReadEnabled ? ["/operations/material-requests", "/operations/replenishments"] : []),
              ...(workdayReadEnabled ? ["/workday", "/workday/shift-plans"] : []),
              ...((inspectionExecutionEnabled || notificationsEnabled) ? ["/inspection-history"] : []),
              ...(checklistEnabled ? ["/inspections/checklists"] : []),
              ...(operationalTasksEnabled ? ["/operational-tasks", "/my-time-log", "/time-corrections"] : [])]} />

{dashboardEnabled && <ReportAccessLink />}
            {internalRequestsEnabled && <InternalRequestAccessLink />}
            {internalRequestsEnabled && <InternalRequestIntakeLink />}

<OperationsRoutes />
          </Protected>
        }
      />
    </Routes>
  );
}
