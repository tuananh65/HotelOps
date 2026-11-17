import { useEffect, useRef, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";

type Catalog = { assetId: number; assetCode: string; assetName: string; serverTimeUtc: string; version: string;
  currentRoomId: number | null; currentAreaId: number | null; hasOpenWork: boolean;
  rooms: { id: number; name: string; floor: string }[]; areas: { id: number; name: string; floor: string | null }[] };

export function AssetPlacementPage({ transfer = false }: { transfer?: boolean }) {
  const { id } = useParams(); const navigate = useNavigate(); const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending && !state.session?.mustChangePassword ? state.authorityGeneration : null;
  const api = transfer ? "asset-transfer" : "asset-placement";
  const identity = `${owner}:${id}:${api}`; const live = useRef(identity); live.current = identity;
  const writer = useRef<AbortController | null>(null);
  const [view, setView] = useState<{ identity: string; data: Catalog } | null>(null);
  const [target, setTarget] = useState(""), [effective, setEffective] = useState("");
  const [reason, setReason] = useState(""), [confirmation, setConfirmation] = useState("");
  const [error, setError] = useState(""), [busy, setBusy] = useState(false), [stale, setStale] = useState(false), [revision, setRevision] = useState(0);
  useEffect(() => {
    setView(null); setTarget(""); setEffective(""); setReason(""); setConfirmation(""); setError(""); setBusy(false); setStale(false);
    if (!owner || !id || !/^[1-9]\d*$/.test(id) || !Number.isSafeInteger(Number(id))) return;
    const abort = new AbortController();
    void fetch(`/api/${api}/${id}/catalog`, { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal })
      .then(async r => { if (r.status === 401) actions.invalidateSession(owner); const data = await r.json();
        if (!r.ok) throw Error(data.detail ?? `Placement unavailable (${r.status}).`);
        if (!abort.signal.aborted && live.current === identity) setView({ identity, data }); })
      .catch(e => { if (!abort.signal.aborted && live.current === identity) setError(String(e)); });
    return () => { abort.abort(); writer.current?.abort(); };
  }, [owner, id, api, identity, actions, revision]);
  const data = owner && view?.identity === identity ? view.data : null;
  async function save() {
    if (!data || !owner || writer.current || stale) return;
    const [type, rawId] = target.split(":"); const targetId = Number(rawId);
    if (!(type === "ROOM" ? data.rooms : type === "AREA" ? data.areas : []).some(o => o.id === targetId)) { setError("Select a current room or area."); return; }
    if (transfer && (reason.trim().length < 10 || data.hasOpenWork && !confirmation.trim())) { setError("Enter the transfer reason and required open-work confirmation."); return; }
    const abort = new AbortController(); writer.current = abort; setBusy(true); setError("");
    try {
      const csrf = await fetch("/api/auth/csrf", { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      if (csrf.status === 401) actions.invalidateSession(owner); const token = await csrf.json();
      if (!csrf.ok || token.headerName !== "X-CSRF-TOKEN" || typeof token.csrfToken !== "string" || !token.csrfToken) throw Error("CSRF bootstrap unavailable.");
      if (abort.signal.aborted || live.current !== identity) return;
      const response = await fetch(`/api/${api}/${id}`, { method: "POST", credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": token.csrfToken, "If-Match": data.version },
        body: JSON.stringify({ roomId: type === "ROOM" ? targetId : null, areaId: type === "AREA" ? targetId : null, effectiveAt: !transfer && effective ? `${effective}:00Z` : null,
          reason: transfer ? reason : null, confirmationNote: transfer ? confirmation : null }) });
      if (response.status === 401) actions.invalidateSession(owner); const result = await response.json();
      if (!response.ok) throw Error(result.detail ?? `Placement failed (${response.status}).`);
      if (!abort.signal.aborted && live.current === identity) navigate(`/assets/${id}`, { replace: true, state: { message: "MSG08 - Location assigned." } });
    } catch (e) { if (!abort.signal.aborted && live.current === identity) { setError(String(e)); setStale(true); setView(null); } }
    finally { if (writer.current === abort) { writer.current = null; setBusy(false); } }
  }
  return <section><h1>{transfer ? "Transfer asset within current branch" : "Assign first asset location"}</h1><Link to={`/assets/${id}`}>Asset profile</Link>
    {error && <p role="alert">{error}</p>}{stale && <button disabled={busy} onClick={() => setRevision(v => v + 1)}>Reload current state</button>}
    {data && <fieldset disabled={busy || stale}><legend>{data.assetCode} - {data.assetName}</legend>
      {transfer && <p>Current location: {data.currentRoomId != null ? `Room ${data.rooms.find(r => r.id === data.currentRoomId)?.name ?? data.currentRoomId}` : `Area ${data.areas.find(a => a.id === data.currentAreaId)?.name ?? data.currentAreaId}`}.</p>}
      {transfer && data.hasOpenWork && <p role="alert">Linked incident or maintenance work is open. A confirmation note is required before transfer.</p>}
      <label>Location <select value={target} onChange={e => setTarget(e.target.value)}><option value="">Select</option>
        <optgroup label="Rooms">{data.rooms.map(o => <option key={o.id} value={`ROOM:${o.id}`}>{o.name} / {o.floor}</option>)}</optgroup>
        <optgroup label="Areas">{data.areas.map(o => <option key={o.id} value={`AREA:${o.id}`}>{o.name} / {o.floor ?? "N/A"}</option>)}</optgroup>
      </select></label>
      {transfer ? <><label>Transfer reason <textarea required minLength={10} maxLength={4000} value={reason} onChange={e => setReason(e.target.value)} /></label>
        <label>Confirmation note for linked open work <textarea maxLength={4000} value={confirmation} onChange={e => setConfirmation(e.target.value)} /></label>
        <p><Link to={`/assets/${id}/transfer/cross-branch`}>Request or confirm cross-branch handover</Link></p></>
        : <label>Effective date (UTC, optional) <input type="datetime-local" value={effective} onChange={e => setEffective(e.target.value)} /></label>}
      <p>Leave empty to use the server confirmation time. Future dates are rejected. Server time when loaded: {data.serverTimeUtc}.</p>
      <button onClick={() => void save()}>Save location</button>
    </fieldset>}
  </section>;
}
