import { useEffect, useRef, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";

type Target = { departmentId: number; departmentName: string; userId: number; name: string };
type History = { id: number; number: string; state: string; createdAt: string; conditionNote: string | null; snapshot: string | null; decisionSnapshot: string | null; canResubmit: boolean };
type View = { assetId: number; assetCode: string; assetName: string; currentDepartmentId: number | null; custodianUserId: number | null;
  conditionStatus: string; lifecycleStatus: string; version: string; canSubmit: boolean; canDecide: boolean;
  targets: Target[]; pending: { handoverId: number; handoverNo: string; conditionNote: string | null }[];
  history: History[]; historyIncomplete: boolean; page: number; pageSize: number; total: number; photoLimits: { maxFiles: number; maxMegabytes: number; formats: string[] } };
function photo(file: File): Promise<{ name: string; mimeType: string; base64: string }> {
  return new Promise((resolve, reject) => { const reader = new FileReader(); reader.onerror = () => reject(Error("Cannot read photo."));
    reader.onload = () => resolve({ name: file.name, mimeType: file.type, base64: String(reader.result).split(",")[1] }); reader.readAsDataURL(file); });
}
function evidence(row: History): { notes?: string; photo_ids?: number[]; previous_handover_id?: number } {
  try { return row.snapshot ? JSON.parse(row.snapshot) : {}; } catch { return {}; }
}
function decision(row: History): { reason?: string; decided_at?: string; to_user_id?: number } {
  try { return row.decisionSnapshot ? JSON.parse(row.decisionSnapshot) : {}; } catch { return {}; }
}
export function AssetCustodyPage() {
  const { id } = useParams(); const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending && !state.session?.mustChangePassword ? state.authorityGeneration : null;
  const key = `${owner}:${id}`; const live = useRef(key); live.current = key;
  const [view, setView] = useState<{ key: string; value: View } | null>(null), [page, setPage] = useState(1), [reload, setReload] = useState(0);
  const [target, setTarget] = useState(""), [condition, setCondition] = useState(""), [notes, setNotes] = useState(""), [reason, setReason] = useState("");
  const [previous, setPrevious] = useState<number | null>(null), [files, setFiles] = useState<File[]>([]);
  const [message, setMessage] = useState(""), [busy, setBusy] = useState(false), [stale, setStale] = useState(false);
  const inFlight = useRef(false), writer = useRef<AbortController | null>(null);
  const data = owner && view?.key === key ? view.value : null;
  useEffect(() => {
    setView(null); setMessage(""); setStale(false); setPrevious(null); setFiles([]); setTarget(""); setCondition(""); setNotes(""); setReason("");
    writer.current?.abort(); inFlight.current = false; setBusy(false);
    if (!owner || !id || !/^[1-9]\d*$/.test(id) || !Number.isSafeInteger(Number(id))) return;
    const abort = new AbortController();
    void fetch(`/api/asset-custody/${id}?page=${page}`, { signal: abort.signal, credentials: "same-origin", cache: "no-store", redirect: "error" })
      .then(async r => { if (r.status === 401) actions.invalidateSession(owner); const value = await r.json();
        if (!r.ok) throw Error(value.detail ?? `Access unavailable (${r.status}).`);
        const v = value as View;
        if (v.assetId !== Number(id) || !/^"[0-9A-F]{16}"$/.test(v.version) || !Array.isArray(v.targets) || !Array.isArray(v.history) || !Array.isArray(v.pending)) throw Error("Invalid custody response.");
        if (!abort.signal.aborted && live.current === key) setView({ key, value: v });
      }).catch(e => { if (!abort.signal.aborted && live.current === key) setMessage(String(e)); });
    return () => { abort.abort(); writer.current?.abort(); };
  }, [id, owner, key, page, reload, actions]);
  async function mutate(action: "submit" | "acknowledge" | "reject", handover?: number) {
    if (!data || !owner || inFlight.current || stale) return;
    inFlight.current = true; setBusy(true); setMessage(""); const abort = new AbortController(); writer.current = abort; let dispatched = false;
    try {
      let body: unknown = {};
      if (action === "submit") {
        const selected = data.targets.find(t => `${t.departmentId}:${t.userId}` === target);
        if (!selected || !condition.trim() || files.length > data.photoLimits.maxFiles) throw Error("Select an eligible receiver and record the condition; check photo count.");
        for (const file of files) if (file.size > data.photoLimits.maxMegabytes * 1024 * 1024 || !["image/jpeg", "image/png"].includes(file.type)) throw Error("Photo exceeds the allowed size or format.");
        body = { departmentId: selected.departmentId, receiverId: selected.userId, conditionNote: condition.trim(), notes: notes.trim() || null,
          previousHandoverId: previous, photos: await Promise.all(files.map(photo)) };
      } else if (action === "reject") { if (reason.trim().length < 10) throw Error("Rejection reason requires at least 10 characters."); body = { reason: reason.trim() }; }
      if (abort.signal.aborted || live.current !== key) return;
      const csrfResponse = await fetch("/api/auth/csrf", { signal: abort.signal, credentials: "same-origin", cache: "no-store", redirect: "error" });
      if (csrfResponse.status === 401) actions.invalidateSession(owner);
      const csrf = await csrfResponse.json(); if (!csrfResponse.ok || csrf.headerName !== "X-CSRF-TOKEN" || typeof csrf.csrfToken !== "string" || !csrf.csrfToken) throw Error("CSRF unavailable.");
      if (abort.signal.aborted || live.current !== key) return;
      dispatched = true;
      const r = await fetch(`/api/asset-custody/${id}${action === "submit" ? "" : `/${handover}/${action}`}`, {
        method: "POST", credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": data.version }, body: JSON.stringify(body) });
      const result = await r.json(); if (r.status === 401) actions.invalidateSession(owner);
      if (abort.signal.aborted || live.current !== key) return;
      setStale(true);
      if (!r.ok) { setView(null); throw Error(result.detail ?? `Request failed (${r.status}); refresh before retrying.`); }
      setView(null); setFiles([]); setMessage(`${result.message ?? "MSG08"}: ${result.state}. Refresh to review current custody.`);
    } catch (e) { if (!abort.signal.aborted && live.current === key) { setMessage(`${String(e)}${dispatched ? " No automatic retry; refresh to confirm the committed state." : ""}`); if (dispatched) { setStale(true); setView(null); } } }
    finally { if (writer.current === abort) { writer.current = null; inFlight.current = false; setBusy(false); } }
  }
  if (!owner) return <p>Current unrestricted session required.</p>;
  return <section><h1>Asset custody handover</h1><Link to={`/assets/${id}`}>Asset profile</Link>
    {message && <p role="status">{message}</p>}<button disabled={busy} onClick={() => setReload(x => x + 1)}>Refresh custody</button>
    {data && <><h2>{data.assetCode} — {data.assetName}</h2><p>Department: {data.currentDepartmentId ?? "N/A"}; custodian: {data.custodianUserId ?? "N/A"}; condition: {data.conditionStatus}; lifecycle: {data.lifecycleStatus}</p>
      {data.canSubmit && <fieldset disabled={busy || stale}><legend>{previous ? `Resubmit rejected version #${previous}` : "New handover"}</legend>
        <label>Receiving department / Head <select value={target} onChange={e => setTarget(e.target.value)}><option value="">Select</option>{data.targets.map(t => <option key={`${t.departmentId}:${t.userId}`} value={`${t.departmentId}:${t.userId}`}>{t.departmentName} — {t.name}</option>)}</select></label>
        <label>Condition at handover <textarea maxLength={4000} value={condition} onChange={e => setCondition(e.target.value)} /></label>
        <label>Notes <textarea maxLength={4000} value={notes} onChange={e => setNotes(e.target.value)} /></label>
        <label>Photos ({data.photoLimits.maxFiles} maximum; {data.photoLimits.maxMegabytes} MiB each) <input key={`${reload}:${previous}`} type="file" multiple accept="image/jpeg,image/png" onChange={e => setFiles(Array.from(e.target.files ?? []))} /></label>
        <p>Submission keeps current custody. Custody changes only after the receiving Head acknowledges.</p>
        <button disabled={!target || !condition.trim()} onClick={() => void mutate("submit")}>Submit handover</button>
        {previous && <button onClick={() => { setPrevious(null); setFiles([]); }}>Cancel resubmit</button>}
      </fieldset>}
      {data.pending.map(p => <section key={p.handoverId}><h3>{p.handoverNo} — Pending acknowledgment</h3><p>{p.conditionNote}</p>
        {data.canDecide && <fieldset disabled={busy || stale}><legend>Receiving Head decision</legend>
          <button onClick={() => void mutate("acknowledge", p.handoverId)}>Acknowledge custody</button>
          <label>Rejection reason <textarea maxLength={4000} value={reason} onChange={e => setReason(e.target.value)} /></label>
          <button disabled={reason.trim().length < 10} onClick={() => void mutate("reject", p.handoverId)}>Reject handover</button></fieldset>}
      </section>)}
      <h3>Handover history</h3>{data.historyIncomplete && <p>N/A: some historical receipts lack verified evidence or require additional branch access.</p>}{data.history.map(h => { const proof = evidence(h), decided = decision(h); return <article key={h.id}><h4>{h.number} — {h.state}</h4>
        <p>Submitted: {h.createdAt} UTC</p>{decided.decided_at && <p>Decision: {decided.decided_at} UTC; Head #{decided.to_user_id}. {decided.reason}</p>}
        <p>{h.conditionNote}</p><p>{proof.notes}</p>{proof.previous_handover_id && <p>Previous version #{proof.previous_handover_id}</p>}
        {proof.photo_ids?.filter(n => Number.isSafeInteger(n) && n > 0).map(n => <p key={n}><a href={`/api/asset-profiles/${id}/files/${n}`} download>Download handover photo #{n}</a></p>)}
        {data.canSubmit && h.canResubmit && <button disabled={busy || stale} onClick={() => { setPrevious(h.id); setCondition(h.conditionNote ?? ""); setNotes(proof.notes ?? ""); setFiles([]); }}>Resubmit as new version</button>}
      </article>; })}
      <p>Page {data.page} <button disabled={busy || page === 1} onClick={() => { setView(null); setPage(x => x - 1); }}>Previous</button> <button disabled={busy || page * data.pageSize >= data.total} onClick={() => { setView(null); setPage(x => x + 1); }}>Next</button></p>
    </>}
  </section>;
}
