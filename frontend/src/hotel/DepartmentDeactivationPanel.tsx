import { useEffect, useMemo, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { strongTag } from "./contracts";
import { createDepartmentDeactivationApi } from "./departmentDeactivationApi";

type Review = { id: number; branchId: number; isActive: boolean; dependencies: { kind: string; count: number }[]; etag: string };
const labels: Record<string,string> = {
  ACTIVE_USERS: "Active employees", WAREHOUSES: "Warehouses",
  OPEN_OR_UNPROVEN_TASKS: "Tasks whose closure is not confirmed", OPEN_INTERNAL_REQUESTS: "Open internal requests",
  ASSETS_IN_CUSTODY: "Assets in custody", PENDING_ASSET_HANDOVERS: "Pending asset handovers",
  OPEN_MAINTENANCE: "Open maintenance work", WORKING_WORKDAYS: "Working workdays",
};

export function DepartmentDeactivationPanel({ branchId, departmentId, onSaved }: { branchId: number; departmentId: number; onSaved: () => void }) {
  const { state, actions } = useAuth();
  const identity = useMemo(() => ({}), [state.session, state.status, state.pending, branchId, departmentId]);
  const latest = useRef(identity); latest.current = identity;
  const [review, setReview] = useState<Review | null>(null);
  const [reason, setReason] = useState("");
  const [confirmed, setConfirmed] = useState(false);
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const [reload, setReload] = useState(0);
  const [api] = useState(() => createDepartmentDeactivationApi(globalThis.fetch.bind(globalThis), true));
  const controller = useRef<AbortController | null>(null);
  const pending = useRef(false);
  useEffect(() => {
    const abort = new AbortController(); controller.current = abort;
    setReview(null); setConfirmed(false); setReason(""); setMessage("");
    if (state.status !== "AUTHENTICATED" || state.pending !== null) return () => abort.abort();
    setBusy(true);
    void (async () => {
      try {
        const response = await fetch(`/api/branches/${branchId}/departments/${departmentId}/deactivation-review`, {
          credentials: "include", cache: "no-store", redirect: "error", signal: abort.signal,
          headers: { Accept: "application/json" },
        });
        if (abort.signal.aborted || latest.current !== identity) return;
        if (!response.ok) {
          setMessage(`Dependency review unavailable (${response.status}).`);
          if (response.status === 401 || response.status === 403) void actions.revalidate();
          return;
        }
        const data = await response.json() as Review;
        const etag = response.headers.get("ETag");
        if (!data || data.id !== departmentId || data.branchId !== branchId || typeof data.isActive !== "boolean"
          || !Array.isArray(data.dependencies) || !data.dependencies.every(d => d && typeof d.kind === "string" && Number.isSafeInteger(d.count) && d.count > 0) || !strongTag(etag))
          throw new Error("Invalid review");
        if (!abort.signal.aborted && latest.current === identity) setReview({ ...data, etag });
      } catch { if (!abort.signal.aborted && latest.current === identity) setMessage("Dependency review unavailable. Reload before continuing."); }
      finally { if (!abort.signal.aborted && latest.current === identity) setBusy(false); }
    })();
    return () => abort.abort();
  }, [identity, reload]);

  async function deactivate() {
    if (!review || !review.isActive || review.dependencies.length || !confirmed || pending.current || busy) return;
    pending.current = true; setBusy(true);
    const current = () => latest.current === identity && controller.current?.signal.aborted === false;
    try {
      const result = await api.change(branchId, departmentId, review.etag, reason, current, controller.current?.signal);
      if (!current()) return;
      setConfirmed(false);
      if (result.outcome === "CONFIRMED") {
        setReview({ ...review, isActive: false, etag: result.etag });
        setMessage("MSG08: Department deactivated. Historical records are preserved.");
        onSaved();
      } else {
        setReview(null);
        setMessage(result.outcome === "OUTCOME_UNKNOWN"
          ? "Outcome unknown. Reload the server record before attempting another change."
          : "Change rejected or not sent. Reload dependency review; the version, permissions or dependencies may have changed.");
        if (result.status === 401 || result.status === 403) void actions.revalidate();
      }
    } finally { pending.current = false; if (current()) setBusy(false); }
  }

  if (state.status !== "AUTHENTICATED" || state.pending !== null) return null;
  return <section aria-label="Deactivate department">
    <h2>Deactivate department</h2>
    <button type="button" disabled={busy} onClick={() => setReload(n => n + 1)}>Refresh dependency review</button>
    {busy && <p role="status">Loading or saving…</p>}
    {message && <p role="status">{message}</p>}
    {review && !review.isActive && <p>This department is inactive and cannot be selected for new records.</p>}
    {review && review.dependencies.length > 0 && <><p>MSG34: Resolve these dependencies before deactivation.</p>
      <ul>{review.dependencies.map(d => <li key={d.kind}>{labels[d.kind] ?? d.kind}: {d.count}</li>)}</ul></>}
    {review?.isActive && review.dependencies.length === 0 && <>
      <p>MSG33: Deactivate this department? Existing history remains available.</p>
      <label>Reason (10–1000 characters)<textarea minLength={10} maxLength={1000} value={reason} disabled={busy}
        onChange={e => { setReason(e.target.value); setConfirmed(false); }} /></label>
      <label><input type="checkbox" checked={confirmed} disabled={busy} onChange={e => setConfirmed(e.target.checked)} />Confirm deactivation</label>
      <button type="button" disabled={busy || !confirmed || reason.trim().length < 10} onClick={() => void deactivate()}>Deactivate</button>
    </>}
  </section>;
}
