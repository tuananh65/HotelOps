import { useAuth } from "../auth/AuthProvider";
import { useLocation } from "react-router-dom";
import ReportIncidentScreen from "../screens/maintenance/ReportIncidentScreen";

// Mount beneath the host's existing AuthProvider and BrowserRouter.
export default function ReportIncidentRoute() {
  const { state } = useAuth();
  const location = useLocation();
  if (state.status !== "AUTHENTICATED" || !state.session || state.session.mustChangePassword)
    return <p role="alert">An active unrestricted server session is required. Sign in or recover your session.</p>;
  return <ReportIncidentScreen key={`${state.session.user.id}:${location.key}:${location.search}`}
    search={location.search} />;
}
