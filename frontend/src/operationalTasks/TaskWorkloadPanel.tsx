import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";
import { taskWorkflowStates } from "./api";
import { TaskProgressError } from "./progress";
import {
  readWorkload,
  type WorkloadDetail,
  type WorkloadFilters,
  type WorkloadPage,
} from "./workload";

export function TaskWorkloadPanel({
  branch,
  owner,
  initialSearch = "",
}: {
  branch: number;
  owner: AuthorityGeneration;
  initialSearch?: string;
}) {
  const { actions } = useAuth();
  const [form, setForm] = useState({
    from: "",
    to: "",
    departmentId: "",
    employeeId: "",
    priorityId: "",
    status: "",
    deadlineFrom: "",
    deadlineTo: "",
    search: initialSearch,
    overdueOnly: false,
  });
  const [query, setQuery] = useState<{
    filters: WorkloadFilters;
    page: number;
    employee: number | null;
    revision: number;
  } | null>(null);
  const [result, setResult] = useState<WorkloadPage | WorkloadDetail | null>(
    null,
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [denied, setDenied] = useState(false);
  useEffect(() => {
    if (!query) return;
    const controller = new AbortController();
    setBusy(true);
    setResult(null);
    setError("");
    void readWorkload(
      branch,
      query.filters,
      query.page,
      query.employee,
      controller.signal,
    )
      .then((data) => {
        if (!controller.signal.aborted) setResult(data);
      })
      .catch((error) => {
        if (controller.signal.aborted) return;
        if (error instanceof TaskProgressError && error.status === 401)
          actions.invalidateSession(owner);
        if (error instanceof TaskProgressError && error.status === 403)
          setDenied(true);
        setError(
          error instanceof TaskProgressError && error.status === 400
            ? "Check the reference IDs and dates. Use an ordered period of at most 366 days."
            : "Workload is unavailable. Reload after checking your filters and access.",
        );
      })
      .finally(() => {
        if (!controller.signal.aborted) setBusy(false);
      });
    return () => controller.abort();
  }, [branch, owner, actions, query]);
  function load() {
    const filters: WorkloadFilters = { overdueOnly: form.overdueOnly };
    for (const key of [
      "from",
      "to",
      "deadlineFrom",
      "deadlineTo",
      "status",
      "search",
    ] as const)
      if (form[key]) filters[key] = form[key];
    for (const key of ["departmentId", "employeeId", "priorityId"] as const)
      if (form[key]) {
        const n = Number(form[key]);
        if (!/^[1-9]\d*$/.test(form[key]) || !Number.isSafeInteger(n)) {
          setError("Reference IDs must be positive whole numbers.");
          return;
        }
        filters[key] = n;
      }
    setQuery((q) => ({
      filters,
      page: 1,
      employee: null,
      revision: (q?.revision ?? 0) + 1,
    }));
  }
  if (denied)
    return (
      <p role="status">
        Workload requires an authorized Department Head or Operations Manager.
      </p>
    );
  if (!query)
    return <button onClick={load}>Review workload and time logs</button>;
  const page = result && "items" in result ? result : null;
  const detail = result && "userId" in result ? result : null;
  const total =
    page?.totalCount ??
    (detail
      ? Math.max(detail.taskCount, detail.intervalCount, detail.shiftCount ?? 0)
      : 0);
  const field = (key: keyof Omit<typeof form, "overdueOnly">, value: string) =>
    setForm((f) => ({ ...f, [key]: value }));
  return (
    <section aria-label="Task workload">
      <h3>Workload and time logs</h3>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          load();
        }}
      >
        <label>
          Workload from
          <input
            type="date"
            value={form.from}
            onChange={(e) => field("from", e.target.value)}
          />
        </label>
        <label>
          Workload to
          <input
            type="date"
            value={form.to}
            onChange={(e) => field("to", e.target.value)}
          />
        </label>
        <label>
          Workload department ID
          <input
            inputMode="numeric"
            pattern="[1-9][0-9]*"
            value={form.departmentId}
            onChange={(e) => field("departmentId", e.target.value)}
          />
        </label>
        <label>
          Workload employee ID
          <input
            inputMode="numeric"
            pattern="[1-9][0-9]*"
            value={form.employeeId}
            onChange={(e) => field("employeeId", e.target.value)}
          />
        </label>
        <label>
          Workload priority ID
          <input
            inputMode="numeric"
            pattern="[1-9][0-9]*"
            value={form.priorityId}
            onChange={(e) => field("priorityId", e.target.value)}
          />
        </label>
        <label>
          Workload status
          <select
            aria-label="Workload status"
            value={form.status}
            onChange={(e) => field("status", e.target.value)}
          >
            <option value="">All workflows</option>
            {taskWorkflowStates.map((s) => (
              <option key={s} value={s}>
                {s.replaceAll("_", " ")}
              </option>
            ))}
          </select>
        </label>
        <label>
          Deadline from
          <input
            type="date"
            value={form.deadlineFrom}
            onChange={(e) => field("deadlineFrom", e.target.value)}
          />
        </label>
        <label>
          Deadline to
          <input
            type="date"
            value={form.deadlineTo}
            onChange={(e) => field("deadlineTo", e.target.value)}
          />
        </label>
        <label>
          Workload search
          <input
            maxLength={150}
            value={form.search}
            onChange={(e) => field("search", e.target.value)}
          />
        </label>
        <label>
          <input
            type="checkbox"
            checked={form.overdueOnly}
            onChange={(e) =>
              setForm((f) => ({ ...f, overdueOnly: e.target.checked }))
            }
          />
          Overdue only
        </label>
        <button disabled={busy}>Apply workload filters</button>
      </form>
      {busy && <p role="status">Loading workload…</p>}
      {error && <p role="alert">{error}</p>}
      <button
        disabled={busy}
        onClick={() => setQuery((q) => q && { ...q, revision: q.revision + 1 })}
      >
        Reload workload
      </button>
      {result && (
        <p>
          Hotel dates {result.from} through {result.to} (Asia/Ho_Chi_Minh).
          Snapshot {result.asOf}. Durations are seconds within this period.
        </p>
      )}
      {page && (
        <>
          {page.messageCode === "MSG10" && (
            <p role="status">MSG10: No workload matches these filters.</p>
          )}
          <table aria-label="Employee workload">
            <thead>
              <tr>
                <th>Employee</th>
                <th>Open tasks</th>
                <th>Overdue</th>
                <th>Closed in period</th>
                <th>Closed effort (s)</th>
                <th>Provisional open (s)</th>
                <th>Details</th>
              </tr>
            </thead>
            <tbody>
              {page.items.map((u) => (
                <tr key={u.userId}>
                  <td>
                    {u.name} ({u.employeeCode})
                  </td>
                  <td>{u.openTasks}</td>
                  <td>{u.overdueTasks}</td>
                  <td>{u.closedTasks}</td>
                  <td>{u.closedEffortSeconds}</td>
                  <td>{u.provisionalSeconds}</td>
                  <td>
                    <button
                      onClick={() =>
                        setQuery(
                          (q) => q && { ...q, employee: u.userId, page: 1 },
                        )
                      }
                    >
                      Time logs for {u.name}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <p>
            Closed counts distinct tasks with an accepted completion by that
            employee in the period. Reopening preserves previous completions and
            recorded effort.
          </p>
        </>
      )}
      {detail && (
        <section aria-label="Employee time log">
          <h4>{detail.name}</h4>
          <button
            onClick={() =>
              setQuery((q) => q && { ...q, employee: null, page: 1 })
            }
          >
            Back to employee workload
          </button>
          <table aria-label="Employee tasks">
            <thead>
              <tr>
                <th>Task</th>
                <th>Department</th>
                <th>Priority</th>
                <th>Deadline</th>
                <th>State</th>
              </tr>
            </thead>
            <tbody>
              {detail.tasks.map((t) => (
                <tr key={t.taskId}>
                  <td>
                    <Link
                      to={`/operational-tasks?branchId=${branch}&taskId=${t.taskId}`}
                    >
                      {t.code}: {t.title}
                    </Link>
                  </td>
                  <td>{t.department}</td>
                  <td>{t.priority ?? "—"}</td>
                  <td>
                    {t.deadline ?? "—"}
                    {t.overdue && " · Overdue"}
                  </td>
                  <td>{t.state}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <table aria-label="Task work intervals">
            <thead>
              <tr>
                <th>Interval</th>
                <th>Task</th>
                <th>Start UTC</th>
                <th>End UTC</th>
                <th>Closed (s)</th>
                <th>Provisional (s)</th>
              </tr>
            </thead>
            <tbody>
              {detail.intervals.map((i) => (
                <tr key={i.intervalId}>
                  <td>{i.intervalId}</td>
                  <td>{i.taskId}</td>
                  <td>{i.start}</td>
                  <td>{i.end ?? "Open"}</td>
                  <td>{i.closedSeconds}</td>
                  <td>{i.provisionalSeconds}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <h4>Workday / shift time — separate from task effort</h4>
          {detail.shifts === null ? (
            <p>Shift time is unavailable without current SHIFT_VIEW access.</p>
          ) : (
            <table aria-label="Separate workday time">
              <thead>
                <tr>
                  <th>Hotel date</th>
                  <th>Start UTC</th>
                  <th>End UTC</th>
                  <th>Closed (s)</th>
                  <th>Provisional (s)</th>
                </tr>
              </thead>
              <tbody>
                {detail.shifts.map((d) => (
                  <tr key={d.workdayId}>
                    <td>{d.workDate}</td>
                    <td>{d.start}</td>
                    <td>{d.end ?? "Working"}</td>
                    <td>{d.closedSeconds}</td>
                    <td>{d.provisionalSeconds}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          <p>
            Task, interval and workday tables are paginated independently using
            the page below; task filters do not turn shift duration into effort.
          </p>
        </section>
      )}
      <p>
        Page {query.page};{" "}
        {page
          ? `${total} employees`
          : detail
            ? `${detail.taskCount} tasks, ${detail.intervalCount} intervals, ${detail.shiftCount ?? "unavailable"} workdays`
            : ""}
      </p>
      <button
        disabled={busy || query.page <= 1}
        onClick={() => setQuery((q) => q && { ...q, page: q.page - 1 })}
      >
        Previous workload page
      </button>
      <button
        disabled={busy || !result || query.page * 20 >= total}
        onClick={() => setQuery((q) => q && { ...q, page: q.page + 1 })}
      >
        Next workload page
      </button>
    </section>
  );
}
