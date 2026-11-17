import { DashboardSourceRecords } from "./DashboardSourceRecords";
import { HomeNavigation } from "../shared-host/HomeNavigation";
import { DashboardFilters } from "./DashboardFilters";
import { dashboardWorkloadLink } from "./dashboardWorkloadLink";
import { ReportAccessLink } from "./ReportsPage";
import { InternalRequestAccessLink } from "../internalRequests/HistoryPage";
import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";
import {
  DashboardFailure,
  readDashboard,
  type Dashboard,
} from "./dashboardApi";

const sources: Record<string, string> = {
  "rooms.effective_readiness": "Effective room readiness",
  "rooms.operational_status": "Recorded room status",
  "cleaning_tasks.status": "Cleaning tasks",
  "operational_tasks.status/priority_id": "Operational tasks / priority",
  "operational_tasks.workflow_status/priority_id": "Task workflow / priority",
  "incidents.status/severity_id": "Open incidents / severity",
  "maintenance_works.status": "Maintenance work",
  "low_stock.warehouse_id": "Low stock by warehouse",
  "preventive_occurrences.workflow_status": "Preventive occurrences",
  "stock_movements.category": "Stock movements by item category / base unit",
};
const quantities: Record<string, string> = {
  netStockMovement: "Net stock movement",
  stockReceived: "Stock received", stockIssued: "Stock issued", transferLosses: "Transfer losses",
  outstandingApproved: "Outstanding approved materials",
  openReplenishment: "Open replenishment",
};
function RoomLinks({ filters, status }: { filters: Dashboard["filters"]; status?: string }) {
  return <>{filters.branchIds.map(branch => {
    const params = new URLSearchParams({ branchId: String(branch), isActive: "true" });
    if (filters.departmentId !== null) params.set("departmentId", String(filters.departmentId));
    if (status) params.set("readiness", status);
    return <span key={branch}> <Link to={"/rooms/status?" + params}>Rooms in branch {branch}</Link></span>;
  })}</>;
}
export function DashboardPage({ roomsOnly = false }: { roomsOnly?: boolean }) {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" &&
    state.session &&
    state.pending === null &&
    !state.session.mustChangePassword
      ? state.authorityGeneration
      : null;
  const [query, setQuery] = useState("");
  const [source, setSource] = useState<string | null>(null);
  const [metric, setMetric] = useState<string | undefined>();
  const metricSources: Record<string, string> = { openIncidents: "incidents", responseHours: "incidents", resolutionHours: "incidents", slaCompliance: "incidents", reopenedIncidents: "incidents", incidentBlockedRooms: "incidents", preventiveDue: "occurrences", preventiveOverdue: "occurrences", lowStockItems: "stock", transferLosses: "transfers" };
  const [retry, setRetry] = useState(0);
  const key = `${roomsOnly}:${query}:${retry}`;
  const [result, setResult] = useState<{
    owner: AuthorityGeneration;
    key: string;
    data?: Dashboard;
    error?: string;
  } | null>(null);
  useEffect(() => {
    if (!owner) return;
    const controller = new AbortController();
    setResult(null);
    void readDashboard(query, controller.signal, roomsOnly)
      .then((data) => {
        if (!controller.signal.aborted) setResult({ owner, key, data });
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) return;
        const status = error instanceof DashboardFailure ? error.status : 0;
        setResult({
          owner,
          key,
          error:
            status === 403
              ? "You do not have access to this dashboard scope."
              : status === 400
                ? "Check the selected filters."
                : "Dashboard data is unavailable. No zero values have been substituted.",
        });
        if (status === 401) actions.invalidateSession(owner);
      });
    return () => controller.abort();
  }, [owner, key, query, actions, roomsOnly]);
  const visible =
    owner && result?.owner === owner && result.key === key ? result : null;
  const data = visible?.data;
  return (
    <main aria-label="Operations dashboard">
      <h1>{roomsOnly ? "Room readiness dashboard" : "Operations dashboard"}</h1>
      <nav>
        <Link to={roomsOnly ? "/dashboard" : "/dashboard/rooms"}>{roomsOnly ? "Operations dashboard" : "Room readiness dashboard"}</Link>{" "}
        <ReportAccessLink />{" "}
        <InternalRequestAccessLink />{" "}
        <HomeNavigation />
      </nav>
      {!owner ? (
        <p role="status">
          Dashboard data is hidden while your session is checked.
        </p>
      ) : (
        <>
          <DashboardFilters
            owner={owner}
            roomsOnly={roomsOnly}
            invalidate={actions.invalidateSession}
            apply={(query) => {
              setQuery(query);
              setRetry((n) => n + 1);
            }}
          />
          <button onClick={() => setRetry((n) => n + 1)}>Refresh</button>
          {visible?.error && <p role="alert">{visible.error}</p>}
          {!visible && <p role="status">Loading dashboard…</p>}
          {data && (
            <>
              <p>
                Snapshot (UTC):{" "}
                <time dateTime={data.snapshot}>{data.snapshot}</time>
              </p>
              <p>
                Branches: {data.filters.branchIds.join(", ") || "None"};
                department: {data.filters.departmentId ?? "All permitted"}.
              </p>
              <p>
                Period (UTC): {data.filters.from} to {data.filters.to}. Applies
                to inspection cycles, stock movements, task effort and tasks closed in period. Room readiness and open or overdue
                tasks show current status at the snapshot time.
              </p>
              {data.missingSources.length > 0 && (
                <>
                <p role="status">
                  MSG29: Incomplete dashboard: some indicators are unavailable.
                  Recorded room status is not a verified effective-readiness
                  decision.
                </p>
                <ul aria-label="Unavailable dashboard sources">
                  {data.missingSources.map((source, index) => <li key={`${index}:${source}`}>{source}</li>)}
                </ul>
                </>
              )}
              {!roomsOnly && <nav aria-label="Dashboard drill-down">
                {["incidents", "occurrences", "stock", "requests", "replenishment", "transfers"].map(kind =>
                  <button key={kind} onClick={() => { setMetric(undefined); setSource(kind); }}>View {kind} records</button>)}
              </nav>}
              {!roomsOnly && source && <DashboardSourceRecords key={source + key + metric} source={source} metric={metric} filters={data.filters} close={() => setSource(null)} />}
              <section aria-label="Dashboard indicators">
                <dl>
                  {data.metrics.map((m) => (
                    <div key={m.key}>
                      <dt>{m.label}</dt>
                      <dd>
                        <span>{m.value === null
                          ? "N/A"
                          : `${m.value.toLocaleString(undefined, { maximumFractionDigits: 2 })} ${m.unit}`}</span>
                        {!roomsOnly && m.value !== null && metricSources[m.key] && <button onClick={() => { setSource(metricSources[m.key]); setMetric(m.key); }}>View source records</button>}
                        {m.value !== null && ["activeRooms", "readyRooms", "readyRatio"].includes(m.key) && <RoomLinks filters={data.filters} status={m.key === "activeRooms" ? undefined : "READY"} />}
                        {m.value !== null && dashboardWorkloadLink(data.filters, m.key) && (
                          <> <Link to={dashboardWorkloadLink(data.filters, m.key)!}>Review workload</Link></>
                        )}
                      </dd>
                    </div>
                  ))}
                </dl>
              </section>
              <section aria-label="Status distributions">
                <h2>Status distributions</h2>
                {data.distributions.length === 0 ? (
                  <p>No matching status records.</p>
                ) : (
                  <table>
                    <thead>
                      <tr>
                        <th>Category</th>
                        <th>Recorded status / ID</th>
                        <th>Priority / severity ID</th>
                        <th>Share of active rooms</th><th>Count</th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.distributions.map((d, i) => (
                        <tr key={i}>
                          <td>{sources[d.source] ?? "Other status records"}</td>
                          <td>{d.status}</td>
                          <td>{d.severity ?? "—"}</td>
                          <td>{d.source === "rooms.effective_readiness" && (data.metrics.find(m => m.key === "activeRooms")?.value ?? 0) > 0 ? ((100 * d.count) / data.metrics.find(m => m.key === "activeRooms")!.value!).toFixed(2) + "%" : "N/A"}</td>
                          <td>{d.count}{d.source === "rooms.effective_readiness" && <RoomLinks filters={data.filters} status={d.status} />}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </section>
              {!roomsOnly && <section aria-label="Material quantities">
                <h2>Material quantities</h2>
                {!!data.categoryQuantities?.length && <table aria-label="Received and issued by item category"><thead><tr>
                  <th>Item category</th><th>Warehouse</th><th>Base unit</th><th>Received</th><th>Issued</th>
                </tr></thead><tbody>{data.categoryQuantities.map((q, i) => <tr key={i}>
                  <td>{q.categoryId}</td><td>{q.warehouseId}</td><td>{q.uomId}</td><td>{q.received}</td><td>{q.issued}</td>
                </tr>)}</tbody></table>}
                <p>Quantities stay separate by item, unit and warehouse.</p>
                {data.quantities.length === 0 ? (
                  <p>No matching material quantities.</p>
                ) : (
                  <table>
                    <thead>
                      <tr>
                        <th>Category</th>
                        <th>Item category</th><th>Item ID</th>
                        <th>Unit ID</th>
                        <th>Warehouse ID</th>
                        <th>Quantity</th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.quantities.map((q, i) => (
                        <tr key={i}>
                          <td>{quantities[q.kind] ?? "Other quantity"} <button onClick={() => { setSource(q.kind === "outstandingApproved" ? "requests" : q.kind === "openReplenishment" ? "replenishment" : q.kind === "transferLosses" ? "transfers" : "stock"); setMetric(q.kind); }}>View source</button></td>
                          <td>{q.categoryId ?? "N/A"}</td><td>{q.itemId}</td>
                          <td>{q.uomId}</td>
                          <td>{q.warehouseId ?? "—"}</td>
                          <td>{q.quantity}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </section>}
            </>
          )}
        </>
      )}
    </main>
  );
}
