import { useLocation } from "react-router-dom";
import { HotelWorkspace } from "./HotelWorkspace";

export function RoomDirectoryRoute() {
  const location = useLocation();
  return <HotelWorkspace key={location.pathname + location.search} screen="rooms" readOnly directoryBasePath="/room-directory" />;
}
