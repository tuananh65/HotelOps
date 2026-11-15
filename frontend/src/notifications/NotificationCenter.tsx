import { useEffect, useRef, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";
import {
  InboxFailure,
  readInbox,
  markInboxRead,
  openInboxSource,
  type Inbox,
} from "./inboxApi";
export function NotificationCenter() {
  const { state, actions } = useAuth();
  const navigate = useNavigate();
  const owner =
    state.status === "AUTHENTICATED" &&
    state.session &&
    !state.session.mustChangePassword &&
    state.pending === null
      ? state.authorityGeneration
      : null;
  const current = useRef(owner);
  current.current = owner;
  const [query, setQuery] = useState("");
  const [revision, setRevision] = useState(0);
  const [selected, setSelected] = useState<number[]>([]);
  const [draft, setDraft] = useState({
    type: "",
    priority: "",
    search: "",
    unread: "",
    acknowledged: "",
    from: "",
    to: "",
  });
  const [result, setResult] = useState<{
    owner: AuthorityGeneration;
    key: string;
    data?: Inbox;
    error?: string;
  } | null>(null);
  const [write, setWrite] = useState<{
    owner: AuthorityGeneration;
    busy: boolean;
    message?: string;
  } | null>(null);
  const pending = useRef<AbortController | null>(null);
  const key = `${query}:${revision}`;
  useEffect(() => setSelected([]), [owner, key]);
  useEffect(() => {
    setWrite(null);
    return () => {
      pending.current?.abort();
      pending.current = null;
    };
  }, [owner]);
  useEffect(() => {
    if (!owner) return;
    const controller = new AbortController();
    setResult(null);
    void readInbox(query, controller.signal)
      .then((data) => {
        if (!controller.signal.aborted) setResult({ owner, key, data });
      })
      .catch((error) => {
        if (controller.signal.aborted) return;
        setResult({
          owner,
          key,
          error: "Notifications are unavailable. Refresh to try again.",
        });
        if (error instanceof InboxFailure && error.status === 401)
          actions.invalidateSession(owner);
      });
    return () => controller.abort();
  }, [owner, key, query, actions]);
  const visible =
    owner && result?.owner === owner && result.key === key ? result : null;
  const data = visible?.data;
  const appliedFilters =
    [...new URLSearchParams(query).entries()]
      .filter(([name]) => name !== "page")
      .map(([name, value]) => `${name}: ${value}`)
      .join("; ") || "None";
  const operation = write?.owner === owner ? write : null;
  async function openSource(id: number) {
    if (!owner || pending.current) return;
    const controller = new AbortController();
    pending.current = controller;
    setWrite({ owner, busy: true });
    try {
      const path = await openInboxSource(id, controller.signal, () => current.current === owner);
      if (current.current === owner && !controller.signal.aborted)
        navigate(path);
    } catch (error) {
      if (current.current === owner && !controller.signal.aborted) {
        setWrite({
          owner,
          busy: false,
          message: "Source or read confirmation is unavailable. Refresh to check current access.",
        });
        if (error instanceof InboxFailure && error.status === 401)
          actions.invalidateSession(owner);
      }
    } finally {
      if (pending.current === controller) pending.current = null;
    }
  }
  async function mark(id: number | number[] | null, acknowledge = false) {
    if (!owner || pending.current) return;
    const controller = new AbortController();
    pending.current = controller;
    setWrite({ owner, busy: true });
    try {
      await markInboxRead(
        id,
        controller.signal,
        () => current.current === owner,
        acknowledge ? "acknowledge" : "read",
      );
      if (current.current === owner && !controller.signal.aborted) {
        setWrite({
          owner,
          busy: false,
          message: acknowledge
            ? "Acknowledged. Source records are unchanged."
            : "Marked as read. Source records are unchanged.",
        });
        // Read/acknowledgment filters can remove the last row on this page.
        // Restart the filtered list so remaining matches stay reachable.
        setQuery((value) => {
          const parameters = new URLSearchParams(value);
          parameters.delete("page");
          return parameters.toString();
        });
        setRevision((n) => n + 1);
      }
    } catch (error) {
      if (current.current === owner && !controller.signal.aborted) {
        setWrite({
          owner,
          busy: false,
          message:
            "Read status could not be confirmed. Refresh before trying again.",
        });
        if (error instanceof InboxFailure && error.status === 401)
          actions.invalidateSession(owner);
      }
    } finally {
      if (pending.current === controller) pending.current = null;
    }
  }
  return (
    <main aria-label="Notification center">
      <h1>Notification center</h1>
      <nav>
        <Link to="/dashboard">Dashboard</Link>{" "}
        <Link to="/profile">Profile</Link>
      </nav>
      {owner && (
        <>
          <form
            aria-label="Notification filters"
            onSubmit={(e) => {
              e.preventDefault();
              const p = new URLSearchParams();
              Object.entries(draft).forEach(([k, v]) => {
                if (v.trim()) p.set(k, v.trim());
              });
              setQuery(p.toString());
              setRevision((n) => n + 1);
            }}
          >
            {(["type", "priority", "search", "from", "to"] as const).map(
              (name) => (
                <label key={name}>
                  {name}
                  <input
                    type={name === "from" || name === "to" ? "date" : "text"}
                    value={draft[name]}
                    onChange={(e) =>
                      setDraft((d) => ({ ...d, [name]: e.target.value }))
                    }
                  />
                </label>
              ),
            )}
            <label>
              Read status
              <select
                aria-label="Read status"
                value={draft.unread}
                onChange={(e) =>
                  setDraft((d) => ({ ...d, unread: e.target.value }))
                }
              >
                <option value="">All</option>
                <option value="true">Unread</option>
                <option value="false">Read</option>
              </select>
            </label>
            <button>Apply filters</button>
            <button
              type="button"
              onClick={() => {
                setDraft({
                  type: "",
                  priority: "",
                  search: "",
                  unread: "",
                  acknowledged: "",
                  from: "",
                  to: "",
                });
                setQuery("");
                setRevision((n) => n + 1);
              }}
            >
              Clear filters
            </button>
            <label>
              Acknowledgment status
              <select
                value={draft.acknowledged}
                onChange={(e) =>
                  setDraft((d) => ({ ...d, acknowledged: e.target.value }))
                }
              >
                <option value="">All</option>
                <option value="false">Awaiting acknowledgment</option>
                <option value="true">Acknowledged</option>
              </select>
            </label>
          </form>
          <button onClick={() => setRevision((n) => n + 1)}>Refresh</button>
          <p aria-label="Applied notification filters">
            Applied filters: {appliedFilters}
          </p>
          {data?.acknowledgmentAvailable && (
            <button
              disabled={
                !!operation?.busy ||
                !data.items.some(
                  (r) =>
                    r.requiresAcknowledgment &&
                    !r.acknowledgedAt &&
                    selected.includes(r.id),
                )
              }
              onClick={() =>
                void mark(
                  data.items
                    .filter(
                      (r) =>
                        r.requiresAcknowledgment &&
                        !r.acknowledgedAt &&
                        selected.includes(r.id),
                    )
                    .map((r) => r.id),
                  true,
                )
              }
            >
              Acknowledge selected
            </button>
          )}
          <button
            disabled={
              !data ||
              !!operation?.busy ||
              !data.items.some((r) => !r.readAt && selected.includes(r.id))
            }
            onClick={() =>
              void mark(
                data!.items
                  .filter((r) => !r.readAt && selected.includes(r.id))
                  .map((r) => r.id),
              )
            }
          >
            Mark selected as read
          </button>
          <button
            disabled={!data || !!operation?.busy || data.unreadCount === 0}
            onClick={() => void mark(null)}
          >
            Mark all as read
          </button>
          {operation?.message && <p role="status">{operation.message}</p>}
          {visible?.error && <p role="alert">{visible.error}</p>}
          {!visible && <p role="status">Loading notifications…</p>}
          {data && (
            <>
              <p>
                Unread: {data.unreadCount}. Matching: {data.totalCount}.
                Snapshot: {data.snapshot}
              </p>
              {!data.acknowledgmentAvailable && (
                <p>Acknowledging critical alerts is not yet available.</p>
              )}
              {data.items.length === 0 ? (
                <p role="status">
                  No matching notifications (MSG10). Change or clear the applied
                  filters.
                </p>
              ) : (
                <ul>
                  {data.items.map((row) => (
                    <li key={row.id}>
                      <label>
                        <input
                          type="checkbox"
                          checked={selected.includes(row.id)}
                          disabled={
                            (!!row.readAt &&
                              !(
                                row.requiresAcknowledgment &&
                                !row.acknowledgedAt
                              )) ||
                            !!operation?.busy
                          }
                          onChange={(e) =>
                            setSelected((ids) =>
                              e.target.checked
                                ? [...ids, row.id]
                                : ids.filter((id) => id !== row.id),
                            )
                          }
                        />
                        Select notification {row.id}
                      </label>
                      <h2>{row.title}</h2>
                      {[
                        "REWORK_ASSIGNED",
                        "REWORK_SUBMITTED",
                        "REINSPECTION_PASSED",
                        "INSPECTION_FAILED",
                        "REINSPECTION_ASSIGNED",
                        "INSPECTION_ASSIGNED",
                        "TASK_SUBMITTED",
                        "TASK_VERIFIED",
                        "TASK_RETURNED",
                        "TASK_DECLINED",
                        "TASK_ASSIGNED",
                        "TASK_PLANNED",
                        "TASK_CANCELLED",
                        "TASK_SERIES_STOPPED",
                        "TASK_REOPENED",
                        "IR_CREATED",
                        "IR_CLOSED",
                        "IR_HANDOVER",
                        "IR_ESCALATED",
                      ].includes(row.type) && (
                        <button
                          disabled={!!operation?.busy}
                          onClick={() => void openSource(row.id)}
                        >
                          {[
                            "TASK_SUBMITTED",
                            "TASK_VERIFIED",
                            "TASK_RETURNED",
                            "TASK_DECLINED",
                            "TASK_ASSIGNED",
                            "TASK_PLANNED",
                            "TASK_CANCELLED",
                            "TASK_SERIES_STOPPED",
                            "TASK_REOPENED",
                          ].includes(row.type)
                            ? "Open task"
                            : ["IR_CREATED", "IR_CLOSED", "IR_HANDOVER", "IR_ESCALATED"].includes(row.type) ? "Open request"
                            : row.type === "REWORK_SUBMITTED"
                              ? "Assign reinspection"
                              : [
                                    "INSPECTION_FAILED",
                                    "REINSPECTION_ASSIGNED",
                                    "INSPECTION_ASSIGNED",
                                  ].includes(row.type)
                                ? "Open inspection"
                                : "Open rework"}
                        </button>
                      )}
                      <p>{row.message}</p>
                      {row.requiresAcknowledgment && !row.acknowledgedAt && (
                        <strong>Awaiting acknowledgment</strong>
                      )}
                      {row.acknowledgedAt && (
                        <p>
                          Acknowledged: <time>{row.acknowledgedAt}</time>
                        </p>
                      )}
                      <p>
                        {row.type} · {row.priority} ·{" "}
                        <time>{row.createdAt}</time> ·{" "}
                        {row.readAt ? "Read" : "Unread"}
                      </p>
                      <button
                        disabled={!!row.readAt || !!operation?.busy}
                        onClick={() => void mark(row.id)}
                      >
                        Mark notification {row.id} as read
                      </button>
                    </li>
                  ))}
                </ul>
              )}
              <button
                disabled={data.page <= 1}
                onClick={() => {
                  const p = new URLSearchParams(query);
                  p.set("page", String(data.page - 1));
                  setQuery(p.toString());
                }}
              >
                Previous
              </button>
              <button
                disabled={data.page * data.pageSize >= data.totalCount}
                onClick={() => {
                  const p = new URLSearchParams(query);
                  p.set("page", String(data.page + 1));
                  setQuery(p.toString());
                }}
              >
                Next
              </button>
            </>
          )}
        </>
      )}
    </main>
  );
}
