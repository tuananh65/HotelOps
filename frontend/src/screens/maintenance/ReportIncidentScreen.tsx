import { useEffect, useRef, useState } from "react";
import * as api from "../../incidents/api";
import { parseIncidentContext } from "../../incidents/navigation";
import type { ApiFailure } from "../../auth/contracts";

function message(error: ApiFailure): string {
  if (error.kind === "http") {
    if (error.status === 401) return "Session expired. Sign in again before reporting.";
    if (error.status === 403) return "Incident creation is no longer authorized for this branch.";
    return error.problem?.detail ?? error.problem?.title ?? "The request was rejected. Reload and check the selected source.";
  }
  return "The request could not be confirmed. Check your session and connection.";
}

export default function ReportIncidentScreen({ search }: { search: string }) {
  let context: ReturnType<typeof parseIncidentContext> = {};
  let contextError = "";
  try { context = parseIncidentContext(search); } catch (error) { contextError = (error as Error).message; }
  const [branch, setBranch] = useState(context.branchId);
  const [room, setRoom] = useState(context.roomId ? String(context.roomId) : "");
  const [data, setData] = useState<api.Options>();
  const [error, setError] = useState("");
  const [blocked, setBlocked] = useState(false);
  const [busy, setBusy] = useState(false);
  const [created, setCreated] = useState<api.CreatedIncident>();
  const inFlight = useRef(false);
  useEffect(() => {
    if (contextError) return;
    const controller = new AbortController();
    setData(undefined); setError("");
    api.options(branch, context.roomId, controller.signal).then(result => {
      if (controller.signal.aborted) return;
      if (result.ok) setData(result.data);
      else setError(message(result.error));
    }).catch(() => { if (!controller.signal.aborted) setError("Unable to load incident context."); });
    return () => controller.abort();
  }, [branch, context.roomId, contextError]);
  if (contextError) return <p role="alert">{contextError} Return to the source room and open a corrected link.</p>;
  if (error && !data) return <p role="alert">{error} Return to the source or reload; no branch was substituted.</p>;
  if (!data) return <p role="status">Loading authorized incident sources...</p>;
  if (created) return <p role="status">Incident {created.incidentNo} created in branch {created.branchId}. Reference: {created.incidentId}.</p>;
  return <section aria-label="Report incident">
    <h1>Report incident</h1>
    <label>Branch <select aria-label="Branch" value={branch ?? ""} disabled={!!context.branchId || busy || blocked}
      onChange={e => { setBranch(e.target.value ? Number(e.target.value) : undefined); setRoom(""); }}>
      <option value="">Select branch</option>{data.branches.map(v => <option key={v.id} value={v.id}>{v.name}</option>)}
    </select></label>
    <form onSubmit={async e => {
      e.preventDefault();
      if (inFlight.current || blocked || !branch || !data.canCreate) return;
      const form = new FormData(e.currentTarget);
      const selected = (name: string) => { const v = String(form.get(name) ?? ""); return v ? Number(v) : null; };
      const areaId = selected("area"), assetId = selected("asset");
      if (!room && !areaId && !assetId) { setError("Select a room, area or asset."); return; }
      const incidentTitle = String(form.get("title") ?? "").trim();
      const description = String(form.get("description") ?? "").trim();
      const date = new Date(String(form.get("discoveredAt")));
      if (!incidentTitle || !description || !Number.isFinite(date.getTime())) { setError("Enter a title, description and valid discovery time."); return; }
      inFlight.current = true; setBusy(true); setError("");
      try {
        const result = await api.create({ branchId: branch, roomId: room ? Number(room) : null, areaId, assetId,
          severityId: selected("severity"), priorityId: selected("priority"), incidentTitle, description, discoveredAt: date.toISOString() });
        if (result.ok) {
          if (result.data.branchId !== branch || result.data.roomId !== (room ? Number(room) : null)) {
            setError("Created response context differs. Outcome requires verification before another submission."); setBlocked(true);
          } else setCreated(result.data);
        } else {
          setError(message(result.error));
          // No automatic retry of an ambiguous write, expired session or rejected context.
          if (result.error.kind !== "http" || [401, 403].includes(result.error.status) || result.error.status >= 500) setBlocked(true);
        }
      } catch { setError("Creation outcome is unknown. Verify before submitting again."); setBlocked(true); }
      finally { inFlight.current = false; setBusy(false); }
    }}>
      <fieldset disabled={busy || blocked || !branch || !data.canCreate}>
        <label>Room <select aria-label="Room" value={room} disabled={!!context.roomId} onChange={e => setRoom(e.target.value)}>
          <option value="">Select room</option>{data.rooms.map(v => <option key={v.id} value={v.id}>{v.name}</option>)}
        </select></label>
        {([['area', data.areas], ['asset', data.assets], ['severity', data.severities], ['priority', data.priorities]] as const).map(([name, items]) =>
          <label key={name}>{name}<select name={name} aria-label={name}><option value="">Optional</option>
            {items.map(v => <option key={v.id} value={v.id}>{v.name}</option>)}</select></label>)}
        <label>Title <input name="title" required maxLength={200} /></label>
        <label>Description <textarea name="description" required /></label>
        <label>Discovered at <input name="discoveredAt" type="datetime-local" required /></label>
        <button disabled={!data.canCreate || !branch || busy || blocked}>Report incident</button>
      </fieldset>
    </form>
    {error && <p role="alert">{error}</p>}
  </section>;
}
