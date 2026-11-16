import { useLocation } from "react-router-dom";
import { HotelWorkspace } from "./HotelWorkspace";

export function StructureManagementRoute({ kind }: { kind: "floors" | "areas" | "departments" | "roomTypes" | "rooms" }) {
  const location = useLocation();
  return <HotelWorkspace key={location.pathname + location.search} screen={kind} directoryBasePath="/structure/rooms" canonicalStructure />;
}
