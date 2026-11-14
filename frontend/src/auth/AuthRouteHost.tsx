import { SharedAuthHost } from "../shared-host/SharedAuthHost";

// Canonical source now includes the scoped read APIs and their session boundaries.
// Route availability is not a permission grant: each API authorizes the request.
export function AuthRouteHost() {
  return <SharedAuthHost usersReadEnabled structureReadEnabled />;
}
