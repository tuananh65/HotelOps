import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import {
  createReportApi,
  ReportFailure,
  reportTypes,
  type ReportOptions,
  type ReportSnapshot,
  type ReportType,
} from "./reportApi";

// API admission checks actor and REPORT_VIEW on the same eligible role path.
// A route or a permission string in the browser is never an access grant.
export function ReportAccessLink() {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" &&
    state.pending === null &&
    !state.session?.mustChangePassword
      ? state.authorityGeneration
      : null;
  const [allowed, setAllowed] = useState<typeof owner>(null);
  useEffect(() => {
    setAllowed(null);
    if (!owner) return;
    const lifetime = new AbortController();
    void createReportApi(lifetime.signal)
      .options()
      .then(() => {
        if (!lifetime.signal.aborted) setAllowed(owner);
      })
      .catch((e: unknown) => {
        if (
          !lifetime.signal.aborted &&
          e instanceof ReportFailure &&
          e.status === 401
        )
          actions.invalidateSession(owner);
      });
    return () => lifetime.abort();
  }, [owner, actions]);
  return owner && allowed === owner ? <Link to="/reports">Reports</Link> : null;
}

export function ReportsPage() {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" &&
    state.session &&
    state.pending === null &&
    !state.session.mustChangePassword
      ? state.authorityGeneration
      : null;
  const [admission, setAdmission] = useState<{
    owner: typeof owner;
    options?: ReportOptions;
    error?: string;
  } | null>(null);
  const [kind, setKind] = useState<ReportType>("tasks");
  const [branch, setBranch] = useState("");
  const [department, setDepartment] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [snapshot, setSnapshot] = useState<ReportSnapshot | null>(null);
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const [page, setPage] = useState(0);
  const operation = useRef<AbortController | null>(null);
  useEffect(() => {
    setAdmission(null);
    setSnapshot(null);
    setMessage("");
    setBusy(false);
    operation.current?.abort();
    operation.current = null;
    if (!owner) return;
    const lifetime = new AbortController();
    void createReportApi(lifetime.signal)
      .options()
      .then((options) => {
        if (lifetime.signal.aborted) return;
        const end = new Date();
        const start = new Date(end);
        start.setUTCDate(start.getUTCDate() - options.defaultPeriodDays + 1);
        setFrom(start.toISOString().slice(0, 10));
        setTo(end.toISOString().slice(0, 10));
        setBranch("");
        setDepartment("");
        setAdmission({ owner, options });
      })
      .catch((e: unknown) => {
        if (lifetime.signal.aborted) return;
        setAdmission({
          owner,
          error:
            e instanceof ReportFailure && e.status === 403
              ? "You do not have access to operational reports."
              : "Report access is unavailable.",
        });
        if (e instanceof ReportFailure && e.status === 401)
          actions.invalidateSession(owner);
      });
    return () => {
      lifetime.abort();
      operation.current?.abort();
    };
  }, [owner, actions]);
  const visible = owner && admission?.owner === owner ? admission : null;
  const options = visible?.options;
  function changed() {
    operation.current?.abort();
    operation.current = null;
    setBusy(false);
    setSnapshot(null);
    setMessage("");
    setPage(0);
  }
  async function run(exporting: boolean) {
    if (!owner || !options || operation.current) return;
    const controller = new AbortController();
    operation.current = controller;
    const api = createReportApi(controller.signal);
    setBusy(true);
    setMessage("");
    if (!exporting) setSnapshot(null);
    try {
      if (exporting && snapshot) {
        const file = await api.export(snapshot);
        if (controller.signal.aborted) return;
        const url = URL.createObjectURL(file.blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = file.name;
        document.body.append(link);
        link.click();
        link.remove();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
        setMessage("MSG25: Export created; download requested.");
      } else {
        const data = await api.preview(kind, {
          from,
          to,
          ...(branch ? { branchId: Number(branch) } : {}),
          ...(department ? { departmentId: Number(department) } : {}),
        });
        if (!controller.signal.aborted) {
          setSnapshot(data);
          setPage(0);
        }
      }
    } catch (e: unknown) {
      if (controller.signal.aborted) return;
      const status = e instanceof ReportFailure ? e.status : 0;
      if (status === 401 || status === 403) {
        setSnapshot(null);
        setAdmission({ owner, error: "Report access is no longer available." });
        if (status === 401) actions.invalidateSession(owner);
      }
      setMessage(
        status === 422
          ? "MSG26: Narrow the report filters; the export limit was exceeded."
          : exporting
            ? "MSG27: Export failed. Nothing was downloaded. You may retry or generate a new preview."
            : "Preview unavailable. Check filters and current access; no data was substituted.",
      );
    } finally {
      if (operation.current === controller) {
        operation.current = null;
        setBusy(false);
      }
    }
  }
  return (
    <main aria-label="Operational reports">
      <h1>Operational reports</h1>
      <Link to="/dashboard">Dashboard</Link>
      {visible?.error && <p role="alert">{visible.error}</p>}
      {!visible && <p role="status">Checking report access…</p>}
      {options && (
        <>
          <p>
            Periods use UTC. Export limit: {options.exportRowLimit} rows. CSV is
            currently available.
          </p>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              void run(false);
            }}
          >
            <label>
              Report type
              <select
                aria-label="Report type"
                value={kind}
                onChange={(e) => {
                  changed();
                  setKind(e.target.value as ReportType);
                  setDepartment("");
                }}
              >
                {reportTypes.map((k) => (
                  <option key={k} value={k}>
                    {k}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Branch
              <select
                aria-label="Branch"
                value={branch}
                onChange={(e) => {
                  changed();
                  setBranch(e.target.value);
                  setDepartment("");
                }}
              >
                <option value="">All permitted</option>
                {options.branches.map((b) => (
                  <option key={b.id} value={b.id}>
                    {b.code} — {b.name}
                  </option>
                ))}
              </select>
            </label>
            {kind === "tasks" && (
              <label>
                Department
                <select
                  aria-label="Department"
                  value={department}
                  onChange={(e) => {
                    changed();
                    setDepartment(e.target.value);
                  }}
                >
                  <option value="">All permitted</option>
                  {options.departments
                    .filter((d) => !branch || d.branchId === Number(branch))
                    .map((d) => (
                      <option key={d.id} value={d.id}>
                        {d.name}
                      </option>
                    ))}
                </select>
              </label>
            )}
            <label>
              From (UTC)
              <input
                required
                type="date"
                value={from}
                max={to}
                onChange={(e) => {
                  changed();
                  setFrom(e.target.value);
                }}
              />
            </label>
            <label>
              To (UTC)
              <input
                required
                type="date"
                value={to}
                min={from}
                onChange={(e) => {
                  changed();
                  setTo(e.target.value);
                }}
              />
            </label>
            <button disabled={busy}>Preview</button>
          </form>
          {message && <p role="status">{message}</p>}
          {snapshot && (
            <section aria-label="Report preview">
              <p>
                Snapshot (UTC): {snapshot.snapshot}. Branches:{" "}
                {snapshot.filters.branchIds.join(", ") || "None"}; department:{" "}
                {snapshot.filters.departmentId ?? "All permitted"}; period:{" "}
                {snapshot.filters.from} — {snapshot.filters.to}.
              </p>
              {Object.entries(snapshot.appliedFilters ?? {}).map(
                ([key, value]) => (
                  <p key={key}>
                    {key}: {value ?? "All"}
                  </p>
                ),
              )}
              {snapshot.missingSources.length > 0 && (
                <aside aria-label="Report limitations">
                  <strong>Partial report — unavailable indicators:</strong>
                  <ul>
                    {snapshot.missingSources.map((s, i) => (
                      <li key={i}>{s}</li>
                    ))}
                  </ul>
                </aside>
              )}
              {snapshot.totalCount === 0 && (
                <p role="status">
                  MSG10: No records match these filters. CSV headers can still
                  be exported.
                </p>
              )}
              <p>
                {snapshot.totalCount} rows. Page {page + 1}.
              </p>
              {snapshot.sections.map((section) => (
                <section key={section.key}>
                  <h2>{section.key}</h2>
                  <table>
                    <thead>
                      <tr>
                        {section.columns.map((c) => (
                          <th key={c}>{c}</th>
                        ))}
                      </tr>
                    </thead>
                    <tbody>
                      {section.rows
                        .slice(page * 25, (page + 1) * 25)
                        .map((row, i) => (
                          <tr key={i}>
                            {section.columns.map((c) => (
                              <td key={c}>
                                {row[c] === null ? "—" : String(row[c])}
                              </td>
                            ))}
                          </tr>
                        ))}
                    </tbody>
                  </table>
                </section>
              ))}
              <button
                disabled={page === 0}
                onClick={() => setPage((n) => n - 1)}
              >
                Previous page
              </button>
              <button
                disabled={snapshot.sections.every(
                  (s) => s.rows.length <= (page + 1) * 25,
                )}
                onClick={() => setPage((n) => n + 1)}
              >
                Next page
              </button>
              <button
                disabled={busy || !snapshot.exportFormats.includes("csv")}
                onClick={() => void run(true)}
              >
                Export CSV
              </button>
            </section>
          )}
        </>
      )}
    </main>
  );
}
