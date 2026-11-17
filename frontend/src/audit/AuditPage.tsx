import { useEffect, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { auditChanges, auditValue, recordHistoryQuery } from "./auditHistory";
import type { AuditRow } from "./auditApi";
import type { AuthorityGeneration } from "../auth/authStore";
import {
  AuditFailure,
  readAudit,
  type AuditPage as Page,
  type AuditDetail,
} from "./auditApi";

export function AuditPage() {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" &&
    state.pending === null &&
    !state.session?.mustChangePassword
      ? state.authorityGeneration
      : null;
  const [draft, setDraft] = useState({
    branchId: "",
    actorId: "",
    entityType: "",
    entityId: "",
    action: "",
    search: "",
    from: "",
    to: "",
  });
  const [query, setQuery] = useState("page=1&pageSize=25");
  const [id, setId] = useState<number | null>(null);
  const [retry, setRetry] = useState(0);
  const [result, setResult] = useState<{
    owner: AuthorityGeneration;
    key: string;
    data?: Page | AuditDetail;
    error?: string;
  } | null>(null);
  const key = `${query}:${id}:${retry}`;
  useEffect(() => {
    if (!owner) return;
    const controller = new AbortController();
    setResult(null);
    void readAudit(query, id, controller.signal)
      .then((data) => {
        if (!controller.signal.aborted) setResult({ owner, key, data });
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) return;
        const status = error instanceof AuditFailure ? error.status : 0;
        setResult({
          owner,
          key,
          error:
            status === 403
              ? "You do not have access to these audit entries."
              : status === 404
                ? "This audit entry is unavailable."
                : status === 400
                  ? "Check the selected filters."
                  : "Audit data is unavailable.",
        });
        if (status === 401) {
          actions.invalidateSession(owner);
        }
      });
    return () => controller.abort();
  }, [owner, key, query, id, actions]);
  const visible =
    owner && result?.owner === owner && result.key === key ? result : null;
  const data = visible?.data;
  const list = data && "items" in data ? data : null;
  const detail = data && "entry" in data ? data : null;
  const changes = detail ? auditChanges(detail.before, detail.after) : [];
  function history(entry: AuditRow) {
    const next = recordHistoryQuery(entry, query);
    const params = new URLSearchParams(next);
    setDraft({
      branchId: params.get("branchId") ?? "",
      actorId: "",
      entityType: entry.entityType,
      entityId: entry.entityId,
      action: "",
      search: "",
      from: "",
      to: "",
    });
    setId(null);
    setQuery(next);
    setRetry((n) => n + 1);
  }
  function apply(event: FormEvent) {
    event.preventDefault();
    const params = new URLSearchParams({ page: "1", pageSize: "25" });
    for (const [name, value] of Object.entries(draft))
      if (value.trim()) params.set(name, value.trim());
    setId(null);
    setQuery(params.toString());
    setRetry((n) => n + 1);
  }
  function page(next: number) {
    const params = new URLSearchParams(query);
    params.set("page", String(next));
    setQuery(params.toString());
  }
  return (
    <main aria-label="Audit history">
      <h1>Audit history</h1>
      <Link to="/dashboard">Workspace</Link>
      {!owner ? (
        <p role="status">Audit data is hidden while your session is checked.</p>
      ) : (
        <>
          <form onSubmit={apply} aria-label="Audit filters">
            {Object.entries(draft).map(([name, value]) => (
              <label key={name}>
                {
                  (
                    {
                      branchId: "Branch ID",
                      actorId: "Actor ID",
                      entityType: "Entity type",
                      entityId: "Record ID",
                      action: "Action",
                      search: "Search",
                      from: "From (UTC)",
                      to: "To (UTC)",
                    } as Record<string, string>
                  )[name]
                }
                <input
                  type={name === "from" || name === "to" ? "date" : "text"}
                  value={value}
                  maxLength={100}
                  onChange={(e) =>
                    setDraft((d) => ({ ...d, [name]: e.target.value }))
                  }
                />
              </label>
            ))}
            <button type="submit">Apply filters</button>
          </form>
          {visible?.error && <p role="alert">{visible.error}</p>}
          <p aria-label="Applied audit filters">
            Applied filters:{" "}
            {[...new URLSearchParams(query)]
              .filter(([k]) => k !== "page" && k !== "pageSize")
              .map(([k, v]) => `${k}=${v}`)
              .join("; ") || "All permitted audit records"}
          </p>
          {!visible && <p role="status">Loading audit history…</p>}
          <button
            onClick={() => {
              setRetry((n) => n + 1);
            }}
          >
            Refresh
          </button>
          {list && (
            <>
              <p>{list.totalCount} entries</p>
              {list.items.length === 0 ? (
                <p role="status">
                  MSG10: No matching audit entries. Change the filters to
                  continue.
                </p>
              ) : (
                <table>
                  <thead>
                    <tr>
                      <th>Time (UTC)</th>
                      <th>Actor</th>
                      <th>Action</th>
                      <th>Record</th>
                      <th>Summary / recorded changes</th>
                      <th>Details</th>
                    </tr>
                  </thead>
                  <tbody>
                    {list.items.map((row) => (
                      <tr key={row.id}>
                        <td>{row.occurredAt}</td>
                        <td>{row.actor ?? "System"}</td>
                        <td>{row.action}</td>
                        <td>
                          {row.entityType} / {row.entityId}
                        </td>
                        <td>
                          {row.action}:{" "}
                          {row.changedFields?.length
                            ? row.changedFields.join(", ")
                            : "Field names not recorded or withheld"}
                        </td>
                        <td>
                          <button onClick={() => setId(row.id)}>
                            View entry {row.id}
                          </button>
                          <button onClick={() => history(row)}>
                            View record history {row.id}
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
              <button
                disabled={list.page <= 1}
                onClick={() => page(list.page - 1)}
              >
                Previous
              </button>
              <span>Page {list.page}</span>
              <button
                disabled={list.page * list.pageSize >= list.totalCount}
                onClick={() => page(list.page + 1)}
              >
                Next
              </button>
            </>
          )}
          {id !== null && (
            <button onClick={() => setId(null)}>Back to list</button>
          )}
          {detail && (
            <section aria-label="Audit entry">
              <h2>
                {detail.entry.action}: {detail.entry.entityType} /{" "}
                {detail.entry.entityId}
              </h2>
              <p>
                {detail.entry.occurredAt} — {detail.entry.actor ?? "System"}
              </p>
              <p>Sensitive and unsupported fields are withheld.</p>
              <p>{detail.provenance}</p>
              {detail.retention && <section aria-label="Evidence retention">
                <h3>Evidence retention</h3>
                <dl>
                  <dt>Archive status</dt><dd>{detail.retention.archiveStatus}</dd>
                  <dt>Archived at (UTC)</dt><dd>{detail.retention.archivedAt ?? "Not archived"}</dd>
                  <dt>Minimum retention until (UTC)</dt><dd>{detail.retention.retentionUntil ?? "Not applicable"}</dd>
                  <dt>Archived by</dt><dd>{detail.retention.archivedBy ?? "Not applicable"}</dd>
                  <dt>Legal hold</dt><dd>{detail.retention.legalHold
                    ? detail.retention.legalHoldUntil ? `Until ${detail.retention.legalHoldUntil} (UTC)` : "Indefinite"
                    : "No additional hold recorded"}</dd>
                  <dt>Purge eligibility</dt><dd>{detail.retention.purgeEligibility}: {detail.retention.reason}</dd>
                </dl>
                <p>Reaching the retention date does not authorize deletion. Audit logs and receipts remain preserved.</p>
              </section>}
              <button onClick={() => history(detail.entry)}>
                View this record's history
              </button>
              <h3>Changed fields</h3>
              {changes.length === 0 ? (
                <p>
                  No comparable recorded changes. Missing or withheld values are
                  not reconstructed.
                </p>
              ) : (
                <table aria-label="Field changes">
                  <thead>
                    <tr>
                      <th>Field</th>
                      <th>Before</th>
                      <th>After</th>
                    </tr>
                  </thead>
                  <tbody>
                    {changes.map((change) => (
                      <tr key={change.field}>
                        <td>{change.field}</td>
                        <td>
                          {auditValue(change.before, change.beforeRecorded)}
                        </td>
                        <td>
                          {auditValue(change.after, change.afterRecorded)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
              <h3>Before</h3>
              <pre>{JSON.stringify(detail.before, null, 2)}</pre>
              <h3>After</h3>
              <pre>{JSON.stringify(detail.after, null, 2)}</pre>
            </section>
          )}
        </>
      )}
    </main>
  );
}
