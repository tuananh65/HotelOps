import { DashboardFilters } from "./DashboardFilters";
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
  "rooms.operational_status": "Recorded room status",
  "cleaning_tasks.status": "Cleaning tasks",
  "operational_tasks.status/priority_id": "Operational tasks / priority",
  "incidents.status/severity_id": "Open incidents / severity",
  "maintenance_works.status": "Maintenance work",
  "low_stock.warehouse_id": "Low stock by warehouse",
};
const quantities: Record<string, string> = {
  netStockMovement: "Net stock movement",
  outstandingApproved: "Outstanding approved materials",
  openReplenishment: "Open replenishment",
};
export function DashboardPage() {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" &&
    state.session &&
    state.pending === null &&
    !state.session.mustChangePassword
      ? state.authorityGeneration
      : null;
  const [query, setQuery] = useState("");
  const [retry, setRetry] = useState(0);
  const key = `${query}:${retry}`;
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
    void readDashboard(query, controller.signal)
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
  }, [owner, key, query, actions]);
  const visible =
    owner && result?.owner === owner && result.key === key ? result : null;
  const data = visible?.data;
  return (
    <main aria-label="Operations dashboard">
      <h1>Operations dashboard</h1>
      <nav>
        <Link to="/users">Users</Link> <Link to="/hotel">Structure</Link>{" "}
        <Link to="/rooms/status">Room status board</Link>{" "}
        <Link to="/audit">Audit history</Link>{" "}
        <Link to="/profile">Profile</Link>
      </nav>
      {!owner ? (
        <p role="status">
          Dashboard data is hidden while your session is checked.
        </p>
      ) : (
        <>
          <DashboardFilters
            owner={owner}
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
                Stock movement period (UTC): {data.filters.from} to{" "}
                {data.filters.to}. Other figures show current status.
              </p>
              {data.missingSources.length > 0 && (
                <p role="status">
                  Incomplete dashboard: some indicators are unavailable.
                  Recorded room status is not a verified effective-readiness
                  decision.
                </p>
              )}
              <section aria-label="Dashboard indicators">
                <dl>
                  {data.metrics.map((m) => (
                    <div key={m.key}>
                      <dt>{m.label}</dt>
                      <dd>
                        {m.value === null
                          ? "N/A"
                          : `${m.value.toLocaleString(undefined, { maximumFractionDigits: 2 })} ${m.unit}`}
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
                        <th>Count</th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.distributions.map((d, i) => (
                        <tr key={i}>
                          <td>{sources[d.source] ?? "Other status records"}</td>
                          <td>{d.status}</td>
                          <td>{d.severity ?? "—"}</td>
                          <td>{d.count}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </section>
              <section aria-label="Material quantities">
                <h2>Material quantities</h2>
                <p>Quantities stay separate by item, unit and warehouse.</p>
                {data.quantities.length === 0 ? (
                  <p>No matching material quantities.</p>
                ) : (
                  <table>
                    <thead>
                      <tr>
                        <th>Category</th>
                        <th>Item ID</th>
                        <th>Unit ID</th>
                        <th>Warehouse ID</th>
                        <th>Quantity</th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.quantities.map((q, i) => (
                        <tr key={i}>
                          <td>{quantities[q.kind] ?? "Other quantity"}</td>
                          <td>{q.itemId}</td>
                          <td>{q.uomId}</td>
                          <td>{q.warehouseId ?? "—"}</td>
                          <td>{q.quantity}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </section>
            </>
          )}
        </>
      )}
    </main>
  );
}
