import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";
import { InboxFailure, readInbox, markInboxRead, type Inbox } from "./inboxApi";
export function NotificationCenter() {
  const { state, actions } = useAuth();
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
  const [draft, setDraft] = useState({
    type: "",
    priority: "",
    search: "",
    unread: "",
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
  const operation = write?.owner === owner ? write : null;
  async function mark(id: number | null) {
    if (!owner || pending.current) return;
    const controller = new AbortController();
    pending.current = controller;
    setWrite({ owner, busy: true });
    try {
      await markInboxRead(
        id,
        controller.signal,
        () => current.current === owner,
      );
      if (current.current === owner && !controller.signal.aborted) {
        setWrite({
          owner,
          busy: false,
          message: "Marked as read. Source records are unchanged.",
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
          </form>
          <button onClick={() => setRevision((n) => n + 1)}>Refresh</button>
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
              <p>
                Opening source records and acknowledging critical alerts are not
                yet available.
              </p>
              {data.items.length === 0 ? (
                <p>No matching notifications.</p>
              ) : (
                <ul>
                  {data.items.map((row) => (
                    <li key={row.id}>
                      <h2>{row.title}</h2>
                      <p>{row.message}</p>
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
