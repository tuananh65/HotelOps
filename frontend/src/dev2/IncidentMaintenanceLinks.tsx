import { Link } from "react-router-dom";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";

export function IncidentMaintenanceLinks({ data }: { data: Values }) {
  if (!Array.isArray(data.maintenanceWorks) || data.maintenanceWorks.length === 0) return null;
  const rows = data.maintenanceWorks;
  if (!rows.every(r => isObject(r) && typeof r.workId === "number" && Number.isSafeInteger(r.workId) && r.workId > 0
    && typeof r.workNo === "string" && typeof r.status === "string"))
    return <p role="alert">Related maintenance work could not be read. Reload the incident.</p>;
  return <section aria-label="Incident maintenance history"><h3>Maintenance progress and processing history</h3>
    <p>Open a work record to see its progress, work logs and processing details within your current access scope. Previous processing cycles remain separate.</p>
    <ul>{(rows as Values[]).map(row => <li key={String(row.workId)}>
      <Link to={"/operations/maintenance/" + row.workId + "?action=A004A-036"}>{String(row.workNo)}</Link>
      {" — " + String(row.status)}
    </li>)}</ul>
  </section>;
}
