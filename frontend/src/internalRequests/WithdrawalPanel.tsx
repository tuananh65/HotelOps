import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";

interface WithdrawalView { requestId: number; status: string; version: string; canWithdraw: boolean }
class WithdrawalFailure extends Error { constructor(readonly status: number) { super("Withdrawal failed"); } }
const version = (v: unknown): v is string => typeof v === "string" && /^[A-Za-z0-9+/]{11}=$/.test(v);
async function json(path: string, signal: AbortSignal, init?: RequestInit): Promise<unknown> {
  const response = await fetch(path, { ...init, credentials: "same-origin", cache: "no-store", redirect: "error", signal });
  if (!response.ok) throw new WithdrawalFailure(response.status);
  if (!response.headers.get("content-type")?.includes("application/json")) throw new Error("Invalid response");
  return response.json();
}

export function InternalRequestWithdrawalPanel({ id, onWithdrawn }: { id: number; onWithdrawn: () => void }) {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && state.pending === null && !state.session?.mustChangePassword ? state.authorityGeneration : null;
  const [revision, setRevision] = useState(0);
  const [result, setResult] = useState<{ owner: typeof owner; id: number; view: WithdrawalView } | null>(null);
  const [reason, setReason] = useState("");
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const sending = useRef(false);
  const controller = useRef<AbortController | null>(null);
  useEffect(() => {
    setResult(null); setReason(""); setMessage(""); setBusy(false); sending.current = false;
    const abort = new AbortController(); controller.current = abort;
    if (owner) void json(`/api/internal-requests/${id}/withdrawal`, abort.signal).then(raw => {
      const view = raw as WithdrawalView | null;
      if (!view || view.requestId !== id || typeof view.status !== "string" || !version(view.version) || typeof view.canWithdraw !== "boolean"
        || view.canWithdraw && view.status !== "PENDING_ACCEPTANCE") throw new Error("Invalid withdrawal response");
      if (!abort.signal.aborted) setResult({ owner, id, view });
    }).catch(e => {
      if (abort.signal.aborted) return;
      setMessage("Withdrawal availability could not be loaded. Reload to check current access and state.");
      if (e instanceof WithdrawalFailure && e.status === 401) actions.invalidateSession(owner);
    });
    return () => abort.abort();
  }, [id, owner, revision, actions]);
  const view = owner && result?.owner === owner && result.id === id ? result.view : null;
  async function withdraw() {
    const abort = controller.current;
    if (!owner || !view?.canWithdraw || sending.current || !abort || abort.signal.aborted) return;
    if (!reason.trim()) { setMessage("Enter a withdrawal reason."); return; }
    sending.current = true; setBusy(true); setMessage("");
    try {
      const csrf = await json("/api/auth/csrf", abort.signal) as { csrfToken?: unknown };
      if (typeof csrf.csrfToken !== "string" || !csrf.csrfToken || abort.signal.aborted) throw new Error("Security check failed");
      const raw = await json(`/api/internal-requests/${id}/withdrawal`, abort.signal, {
        method: "POST", headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": `"${view.version}"` },
        body: JSON.stringify({ reason: reason.trim() }),
      }) as { requestId?: unknown; status?: unknown; version?: unknown; message?: unknown };
      if (raw.requestId !== id || raw.status !== "CANCELLED" || !version(raw.version) || raw.message !== "MSG08") throw new Error("Withdrawal not confirmed");
      if (!abort.signal.aborted) { setResult(null); setReason(""); setMessage("MSG08: Request withdrawn."); onWithdrawn(); }
    } catch (e) {
      if (!abort.signal.aborted) {
        setResult(null);
        setMessage(e instanceof WithdrawalFailure && e.status === 412 ? "Request changed. Reload before withdrawing."
          : e instanceof WithdrawalFailure && e.status === 403 ? "MSG02: Withdrawal is not permitted. Reload current access."
          : "Withdrawal not confirmed. Reload before retrying.");
        if (e instanceof WithdrawalFailure && e.status === 401) actions.invalidateSession(owner);
      }
    } finally { if (!abort.signal.aborted) { sending.current = false; setBusy(false); } }
  }
  if (!owner || view && !view.canWithdraw) return null;
  return <section aria-label="Withdraw request" aria-busy={busy}>
    {message && <p role="status">{message}</p>}
    <button disabled={busy} onClick={() => setRevision(v => v + 1)}>Reload withdrawal</button>
    {view?.canWithdraw && <>
      <p>You can withdraw your request before it is accepted.</p>
      <label>Withdrawal reason<input value={reason} maxLength={4000} disabled={busy} onChange={e => setReason(e.target.value)} /></label>
      <button disabled={busy} onClick={() => void withdraw()}>Withdraw request</button>
    </>}
  </section>;
}
