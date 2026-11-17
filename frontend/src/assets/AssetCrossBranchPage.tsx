import { useEffect, useRef, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";

type Receiver = { userId: number; name: string; departmentId: number; departmentName: string };
type Destination = { branchId: number; branchName: string; rooms: { id: number; name: string }[];
  areas: { id: number; name: string }[]; receivers: Receiver[] };
type Pending = { handoverId: number; handoverNo: string; conditionNote: string; reason: string;
  confirmationNote: string | null; destinationBranchId: number; targetRoomId: number | null; targetAreaId: number | null;
  toDepartmentId: number; toUserId: number; canDecide: boolean; canCancel: boolean };
type View = { assetCode: string; assetName: string; branchId: number; version: string; hasOpenWork: boolean;
  currentRoomId: number | null; currentAreaId: number | null;
  canSubmit: boolean; hasPendingHandover: boolean; destinations: Destination[]; pending: Pending[] };

export function AssetCrossBranchPage() {
  const { id } = useParams(); const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending && !state.session?.mustChangePassword ? state.authorityGeneration : null;
  const identity = `${owner}:${id}`; const live = useRef(identity); live.current = identity;
  const write = useRef<AbortController | null>(null);
  const [view, setView] = useState<{ identity: string; data: View } | null>(null);
  const [revision, setRevision] = useState(0), [busy, setBusy] = useState(false), [error, setError] = useState(""), [message, setMessage] = useState("");
  const [branch, setBranch] = useState(""), [target, setTarget] = useState(""), [receiver, setReceiver] = useState("");
  const [reason, setReason] = useState(""), [condition, setCondition] = useState(""), [confirmation, setConfirmation] = useState("");
  const [decisionReason, setDecisionReason] = useState(""), [decisionConfirmation, setDecisionConfirmation] = useState("");
  useEffect(() => {
    setView(null); setError("");
    if (!owner || !id || !/^[1-9]\d*$/.test(id) || !Number.isSafeInteger(Number(id))) return;
    const abort = new AbortController();
    void fetch(`/api/asset-transfer/${id}/cross-branch`, { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal })
      .then(async r => { if (r.status === 401) actions.invalidateSession(owner); const body = await r.json();
        if (!r.ok) throw Error(body.detail ?? `Transfer unavailable (${r.status}).`);
        if (!abort.signal.aborted && live.current === identity) setView({ identity, data: body }); })
      .catch(e => { if (!abort.signal.aborted && live.current === identity) setError(String(e)); });
    const refresh = window.setInterval(() => { if (!write.current) setRevision(v => v + 1); }, 30000);
    return () => { abort.abort(); window.clearInterval(refresh); };
  }, [owner, id, identity, actions, revision]);
  useEffect(() => {
    setBranch(""); setTarget(""); setReceiver(""); setReason(""); setCondition(""); setConfirmation("");
    setDecisionReason(""); setDecisionConfirmation(""); setMessage(""); setBusy(false);
    return () => write.current?.abort();
  }, [identity]);
  const data = owner && view?.identity === identity ? view.data : null;
  const destination = data?.destinations.find(d => String(d.branchId) === branch);

  async function post(path: string, body: object) {
    if (!data || !owner || write.current) return;
    const abort = new AbortController(); write.current = abort; setBusy(true); setError(""); setMessage("");
    try {
      const csrf = await fetch("/api/auth/csrf", { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      if (csrf.status === 401) actions.invalidateSession(owner); const token = await csrf.json();
      if (!csrf.ok || token.headerName !== "X-CSRF-TOKEN" || typeof token.csrfToken !== "string" || !token.csrfToken) throw Error("CSRF bootstrap unavailable.");
      if (abort.signal.aborted || live.current !== identity) return;
      const response = await fetch(`/api/asset-transfer/${id}/cross-branch${path}`, { method: "POST", credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": token.csrfToken, "If-Match": data.version }, body: JSON.stringify(body) });
      if (response.status === 401) actions.invalidateSession(owner); const result = await response.json();
      if (!response.ok) throw Error(result.detail ?? `Transfer failed (${response.status}).`);
      if (!abort.signal.aborted && live.current === identity) {
        setView(null); setMessage(`Handover ${result.handoverId}: ${result.state}.`);
        setReason(""); setCondition(""); setConfirmation(""); setDecisionReason(""); setDecisionConfirmation("");
      }
    } catch (e) {
      if (!abort.signal.aborted && live.current === identity) { setView(null); setError(`${String(e)} Reload current state before another action; do not retry an uncertain submission.`); }
    } finally {
      if (write.current === abort) { write.current = null; setBusy(false); }
    }
  }
  function submit() {
    const [kind, raw] = target.split(":"); const location = Number(raw);
    const selected = destination?.receivers.find(r => `${r.departmentId}:${r.userId}` === receiver);
    if (!destination || !selected || !(kind === "ROOM" ? destination.rooms : kind === "AREA" ? destination.areas : []).some(r => r.id === location)
      || reason.trim().length < 10 || !condition.trim() || data?.hasOpenWork && !confirmation.trim()) {
      setError("Select a destination, current eligible receiver and location; enter reason, condition and required open-work confirmation."); return;
    }
    void post("", { branchId: destination.branchId, roomId: kind === "ROOM" ? location : null, areaId: kind === "AREA" ? location : null,
      departmentId: selected.departmentId, receiverId: selected.userId, reason, conditionNote: condition, confirmationNote: confirmation });
  }
  function decide(row: Pending, action: "ACCEPT" | "REJECT" | "CANCEL") {
    if (action !== "ACCEPT" && decisionReason.trim().length < 10 || action === "ACCEPT" && data?.hasOpenWork && !decisionConfirmation.trim()) {
      setError("Provide the rejection/cancellation reason or confirm that open work remains at its source branch."); return;
    }
    void post(`/${row.handoverId}/decision`, { action, reason: decisionReason, confirmationNote: decisionConfirmation });
  }
  return <section><h1>Cross-branch asset handover</h1><Link to={`/assets/${id}`}>Asset profile</Link>{" · "}<Link to={`/assets/${id}/transfer`}>Transfer within current branch</Link>
    {message && <p role="status">{message}</p>}{error && <p role="alert">{error}</p>}
    <button disabled={busy} onClick={() => setRevision(v => v + 1)}>Reload current state</button>
    {data && <><h2>{data.assetCode} — {data.assetName}</h2>
      <p>Custody stays at branch {data.branchId} until the receiving Head confirms. Existing work, assignees and SLA remain at their original branches.</p>
      <p>Current location: {data.currentRoomId != null ? `room ${data.currentRoomId}` : `area ${data.currentAreaId}`}.</p>
      {data.hasOpenWork && <p role="alert">Linked work is still open. The sender and receiver must explicitly confirm its source-branch responsibility.</p>}
      {data.canSubmit && <fieldset disabled={busy}><legend>Request receiving custody</legend>
        <label>Destination branch <select value={branch} onChange={e => { setBranch(e.target.value); setTarget(""); setReceiver(""); }}>
          <option value="">Select</option>{data.destinations.map(d => <option key={d.branchId} value={d.branchId}>{d.branchName}</option>)}</select></label>
        <label>Destination location <select value={target} onChange={e => setTarget(e.target.value)}><option value="">Select</option>
          <optgroup label="Rooms">{destination?.rooms.map(r => <option key={r.id} value={`ROOM:${r.id}`}>{r.name}</option>)}</optgroup>
          <optgroup label="Areas">{destination?.areas.map(a => <option key={a.id} value={`AREA:${a.id}`}>{a.name}</option>)}</optgroup></select></label>
        <label>Receiving Head / department <select value={receiver} onChange={e => setReceiver(e.target.value)}><option value="">Select</option>
          {destination?.receivers.map(r => <option key={`${r.departmentId}:${r.userId}`} value={`${r.departmentId}:${r.userId}`}>{r.name} / {r.departmentName}</option>)}</select></label>
        <label>Transfer reason <textarea maxLength={4000} minLength={10} value={reason} onChange={e => setReason(e.target.value)} /></label>
        <label>Condition at handover <textarea maxLength={4000} value={condition} onChange={e => setCondition(e.target.value)} /></label>
        <label>Confirm open work remains at its source branch <textarea maxLength={4000} value={confirmation} onChange={e => setConfirmation(e.target.value)} /></label>
        <button onClick={submit}>Submit handover</button>
      </fieldset>}
      {data.hasPendingHandover && data.pending.length === 0 && <p>A handover is pending. Its details require current participant and branch permissions.</p>}
      {data.pending.map(p => <fieldset key={p.handoverId} disabled={busy}><legend>{p.handoverNo}</legend>
        <p>Destination branch {p.destinationBranchId}; department {p.toDepartmentId}; receiver {p.toUserId}; {p.targetRoomId != null ? `room ${p.targetRoomId}` : `area ${p.targetAreaId}`}.</p>
        <p>Reason: {p.reason}</p><p>Condition: {p.conditionNote}</p><p>Sender confirmation: {p.confirmationNote ?? "N/A"}</p>
        <label>Decision reason <textarea maxLength={4000} value={decisionReason} onChange={e => setDecisionReason(e.target.value)} /></label>
        <label>Confirm source-branch responsibility for open work <textarea maxLength={4000} value={decisionConfirmation} onChange={e => setDecisionConfirmation(e.target.value)} /></label>
        {p.canDecide && <><button onClick={() => decide(p, "ACCEPT")}>Confirm receipt and transfer custody</button><button onClick={() => decide(p, "REJECT")}>Reject handover</button></>}
        {p.canCancel && <button onClick={() => decide(p, "CANCEL")}>Cancel request</button>}
      </fieldset>)}
    </>}
  </section>;
}
