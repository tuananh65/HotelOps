import { SharedAuthHost } from "../shared-host/SharedAuthHost";

// Canonical routes use the integrated APIs and their session boundaries.
// Route availability is not a permission grant: each API authorizes the request.
export function AuthRouteHost() {
  return (
    <SharedAuthHost
      usersReadEnabled
      structureReadEnabled
      auditReadEnabled
      roleAssignmentEnabled
      accountActivationEnabled
      dashboardEnabled
      notificationsEnabled
      inspectionExecutionEnabled
    />
  );
}
