import { Link, Route, Routes, useLocation } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { AuthRouteHost } from "../auth/AuthRouteHost";
import { HotelWorkspace } from "./HotelWorkspace";
import type { Screen } from "./operations";
function HotelRoute({ screen }: { screen: Screen }) {
  const location = useLocation();
  return (
    <HotelWorkspace key={location.pathname + location.search} screen={screen} />
  );
}
// Additive application routing: all accepted Auth/Users bytes stay unchanged.
function IntegratedDashboard() {
  const { state } = useAuth();
  return (
    <>
      <AuthRouteHost />
      {state.status === "AUTHENTICATED" && state.session && (
        <nav aria-label="Integrated modules">
          <Link to="/hotel">Hotel structure</Link>
        </nav>
      )}
    </>
  );
}
export function HotelRouteHost() {
  return (
    <Routes>
      <Route path="/dashboard" element={<IntegratedDashboard />} />
      <Route path="/hotel" element={<HotelRoute screen="structure" />} />
      <Route path="/branches" element={<HotelRoute screen="structure" />} />
      <Route
        path="/branches/:branchId"
        element={<HotelRoute screen="structure" />}
      />
      <Route path="/floors" element={<HotelRoute screen="floors" />} />
      <Route path="/areas" element={<HotelRoute screen="areas" />} />
      <Route path="/locations" element={<HotelRoute screen="floors" />} />
      <Route path="/room-types" element={<HotelRoute screen="roomTypes" />} />
      <Route
        path="/room-types/:id"
        element={<HotelRoute screen="roomTypes" />}
      />
      <Route path="/rooms" element={<HotelRoute screen="rooms" />} />
      <Route path="/rooms/:id" element={<HotelRoute screen="rooms" />} />
      <Route
        path="/departments"
        element={<HotelRoute screen="departments" />}
      />
      <Route
        path="/departments/:id"
        element={<HotelRoute screen="departments" />}
      />
      <Route path="*" element={<AuthRouteHost />} />
    </Routes>
  );
}
