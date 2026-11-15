import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { InternalRequestDecisionPanel } from "./DecisionPanel";
import { InternalRequestProgressPanel } from "./ProgressPanel";
import {
  decodeHistory,
  decodeTimeline,
  decodeHistoryOptions,
  type HistoryOptions,
  HistoryFailure,
  readHistory,
  type HistoryPage as Page,
  type Timeline,
} from "./historyApi";

export function InternalRequestAccessLink() {
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
    const controller = new AbortController();
    void readHistory("?pageSize=1", controller.signal)
      .then(decodeHistory)
      .then(() => {
        if (!controller.signal.aborted) setAllowed(owner);
      })
      .catch((e: unknown) => {
        if (
          !controller.signal.aborted &&
          e instanceof HistoryFailure &&
          e.status === 401
        )
          actions.invalidateSession(owner);
      });
    return () => controller.abort();
  }, [owner, actions]);
  return owner && allowed === owner ? (
    <Link to="/internal-requests">Internal requests</Link>
  ) : null;
}

export function InternalRequestHistoryPage() {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" &&
    state.pending === null &&
    !state.session?.mustChangePassword
      ? state.authorityGeneration
      : null;
  const [search, setSearch] = useState("");
  const [overdue, setOverdue] = useState(false);
  const [filters, setFilters] = useState({
    status: "",
    requestTypeId: "",
    priorityId: "",
    receivingDepartmentId: "",
  });
  const [page, setPage] = useState(1);
  const [searchParams, setSearchParams] = useSearchParams();
  const requestIds = searchParams.getAll("requestId");
  const selected = requestIds.length === 1 && /^[1-9][0-9]*$/.test(requestIds[0])
    && Number.isSafeInteger(Number(requestIds[0])) ? Number(requestIds[0]) : null;
  const setSelected = (id: number | null) => setSearchParams(id === null ? {} : { requestId: String(id) });
  const [eventPage, setEventPage] = useState(1);
  const [assignmentPage, setAssignmentPage] = useState(1);
  const [revision, setRevision] = useState(0);
  const query = new URLSearchParams({
    page: String(page),
    pageSize: "25",
    ...(search ? { search } : {}),
    ...(overdue ? { overdue: "true" } : {}),
    ...Object.fromEntries(
      Object.entries(filters).filter(([, value]) => value !== ""),
    ),
  });
  const path =
    selected === null
      ? `?${query}`
      : `/${selected}/timeline?eventPage=${eventPage}&assignmentPage=${assignmentPage}&pageSize=25`;
  const key = `${path}:${revision}`;
  const [result, setResult] = useState<{
    owner: typeof owner;
    key: string;
    list?: Page;
    timeline?: Timeline;
    options?: HistoryOptions;
    error?: string;
  } | null>(null);
  useEffect(() => {
    setResult(null);
    if (!owner) return;
    const controller = new AbortController();
    void Promise.all([
      readHistory(path, controller.signal),
      selected === null
        ? readHistory("/options", controller.signal).then(decodeHistoryOptions)
        : Promise.resolve(undefined),
    ])
      .then(([value, options]) => {
        const data =
          selected === null
            ? { list: decodeHistory(value) }
            : { timeline: decodeTimeline(value, selected) };
        if (!controller.signal.aborted)
          setResult({ owner, key, ...data, options });
      })
      .catch((e: unknown) => {
        if (controller.signal.aborted) return;
        setResult({
          owner,
          key,
          error:
            e instanceof HistoryFailure && e.status === 403
              ? "You do not have access to internal request history."
              : e instanceof HistoryFailure && e.status === 404
                ? "Request unavailable or outside your current access."
                : "History unavailable. Retry to load current data.",
        });
        if (e instanceof HistoryFailure && e.status === 401)
          actions.invalidateSession(owner);
      });
    return () => controller.abort();
  }, [owner, actions, path, key, selected]);
  const visible =
    owner && result?.owner === owner && result.key === key ? result : null;
  if (!owner) return null;
  return (
    <main>
      <h1>Internal request history</h1>
      <p>All dates and deadlines are UTC.</p>
      <Link to="/dashboard">Dashboard</Link>{" "}
      <button onClick={() => setRevision((v) => v + 1)}>Refresh history</button>
      {selected === null ? (
        <>
          <label>
            Search requests
            <input
              maxLength={200}
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
            />
          </label>
          {visible?.options && (
            <>
              <label>
                Status
                <select
                  aria-label="Status"
                  value={filters.status}
                  onChange={(e) => {
                    setFilters((v) => ({ ...v, status: e.target.value }));
                    setPage(1);
                  }}
                >
                  <option value="">All statuses</option>
                  {visible.options.statuses.map((value) => (
                    <option key={value} value={value}>
                      {value}
                    </option>
                  ))}
                </select>
              </label>
              {(
                [
                  ["requestTypeId", "Request type", visible.options.types],
                  ["priorityId", "Priority", visible.options.priorities],
                  [
                    "receivingDepartmentId",
                    "Receiving department",
                    visible.options.departments,
                  ],
                ] as const
              ).map(([field, label, values]) => (
                <label key={field}>
                  {label}
                  <select
                    aria-label={label}
                    value={filters[field]}
                    onChange={(e) => {
                      setFilters((v) => ({ ...v, [field]: e.target.value }));
                      setPage(1);
                    }}
                  >
                    <option value="">All</option>
                    {values.map((v) => (
                      <option key={v.id} value={v.id}>
                        {v.name}
                      </option>
                    ))}
                  </select>
                </label>
              ))}
              <button
                onClick={() => {
                  setFilters({
                    status: "",
                    requestTypeId: "",
                    priorityId: "",
                    receivingDepartmentId: "",
                  });
                  setSearch("");
                  setOverdue(false);
                  setPage(1);
                }}
              >
                Clear filters
              </button>
            </>
          )}
          <label>
            <input
              type="checkbox"
              checked={overdue}
              onChange={(e) => {
                setOverdue(e.target.checked);
                setPage(1);
              }}
            />
            Overdue only
          </label>
        </>
      ) : (
        <button onClick={() => setSelected(null)}>Back to requests</button>
      )}
      {!visible && <p role="status">Loading history…</p>}
      {visible?.error && <p role="alert">{visible.error}</p>}
      {visible?.list && (
        <section aria-label="Request list">
          <p>{visible.list.totalCount} requests</p>
          {visible.list.totalCount === 0 ? (
            <p>No requests match your current access and filters.</p>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Request</th>
                  <th>Subject</th>
                  <th>Status</th>
                  <th>Branch</th>
                  <th>Receiving department</th>
                  <th>Current handler</th>
                  <th>Deadline</th>
                  <th>Overdue</th>
                </tr>
              </thead>
              <tbody>
                {visible.list.items.map((row) => (
                  <tr key={row.id}>
                    <td>
                      <button
                        onClick={() => {
                          setSelected(row.id);
                          setEventPage(1);
                          setAssignmentPage(1);
                        }}
                      >
                        {row.number}
                      </button>
                    </td>
                    <td>{row.subject}</td>
                    <td>{row.status}</td>
                    <td>{row.branchId}</td>
                    <td>{row.receivingDepartmentId ?? "Not assigned"}</td>
                    <td>
                      {row.currentAssigneeId ?? "Unassigned or unresolved"}
                    </td>
                    <td>{row.deadline ?? "No deadline"}</td>
                    <td>{row.overdue ? "Yes" : "No"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          <button disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
            Previous page
          </button>
          <span> Page {page} </span>
          <button
            disabled={page * 25 >= visible.list.totalCount}
            onClick={() => setPage((p) => p + 1)}
          >
            Next page
          </button>
        </section>
      )}
      {visible?.timeline && (
        <section aria-label="Request timeline">
          <h2>Request {visible.timeline.requestId}</h2>
          <InternalRequestDecisionPanel id={visible.timeline.requestId} />
          <InternalRequestProgressPanel id={visible.timeline.requestId} />
          <h3>Events</h3>
          {visible.timeline.events.length === 0 && (
            <p>No events on this page.</p>
          )}
          <ol>
            {visible.timeline.events.map((e) => (
              <li key={e.id}>
                {e.occurredAt} — {e.type}: {e.oldStatus ?? "—"} →{" "}
                {e.newStatus ?? "—"}; actor {e.actorId}
                {e.note && <p>{e.note}</p>}
              </li>
            ))}
          </ol>
          <button
            disabled={eventPage <= 1}
            onClick={() => setEventPage((p) => p - 1)}
          >
            Previous events
          </button>
          <span> Page {eventPage} </span>
          <button
            disabled={eventPage * 25 >= visible.timeline.eventTotalCount}
            onClick={() => setEventPage((p) => p + 1)}
          >
            Next events
          </button>
          <h3>Assignments</h3>
          {visible.timeline.assignments.length === 0 && (
            <p>No assignments on this page.</p>
          )}
          <ol>
            {visible.timeline.assignments.map((a) => (
              <li key={a.id}>
                Handler {a.assigneeId}; assigned by {a.assignedBy};{" "}
                {a.assignedAt} → {a.endedAt ?? "Open"}; {a.status}
                {a.note && <p>{a.note}</p>}
              </li>
            ))}
          </ol>
          <button
            disabled={assignmentPage <= 1}
            onClick={() => setAssignmentPage((p) => p - 1)}
          >
            Previous assignments
          </button>
          <span> Page {assignmentPage} </span>
          <button
            disabled={
              assignmentPage * 25 >= visible.timeline.assignmentTotalCount
            }
            onClick={() => setAssignmentPage((p) => p + 1)}
          >
            Next assignments
          </button>
          <h3>Feedback</h3>
          {visible.timeline.feedback ? (
            <p>
              {visible.timeline.feedback.rating} —{" "}
              {visible.timeline.feedback.comment}; submitted{" "}
              {visible.timeline.feedback.submittedAt}
            </p>
          ) : (
            <p>No feedback recorded.</p>
          )}
        </section>
      )}
    </main>
  );
}
