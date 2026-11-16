import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";

type Option = { id: number; name: string };
type Options = { branches: Option[]; types: (Option & { materialNeed: boolean })[]; departments: Option[]; requesterDepartments: Option[]; priorities: Option[]; rooms: Option[]; areas: Option[] };
class Failure extends Error { constructor(readonly status: number, readonly code: string) { super(code); } }
async function json(path: string, signal: AbortSignal, init?: RequestInit): Promise<unknown> {
  const response = await fetch(path, { ...init, signal, credentials: "same-origin", cache: "no-store", redirect: "error" });
  const body = response.headers.get("content-type")?.includes("json") ? await response.json() : null;
  if (!response.ok) throw new Failure(response.status, typeof body?.code === "string" ? body.code : "REQUEST_FAILED");
  return body;
}
function options(raw: unknown): Options {
  const value = raw as Options;
  if (!value || ![value.branches, value.types, value.departments, value.requesterDepartments, value.priorities, value.rooms, value.areas].every(list =>
    Array.isArray(list) && list.every(v => Number.isSafeInteger(v.id) && v.id > 0 && typeof v.name === "string"))
    || value.types.some(v => typeof v.materialNeed !== "boolean")) throw new Error("Invalid intake options");
  return value;
}
export function InternalRequestIntakeLink() {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending && !state.session?.mustChangePassword ? state.authorityGeneration : null;
  const [allowed, setAllowed] = useState<typeof owner>(null);
  useEffect(() => {
    setAllowed(null); const abort = new AbortController();
    if (owner) void json("/api/internal-requests/intake", abort.signal).then(options).then(() => {
      if (!abort.signal.aborted) setAllowed(owner);
    }).catch(e => { if (!abort.signal.aborted && e instanceof Failure && e.status === 401) actions.invalidateSession(owner); });
    return () => abort.abort();
  }, [owner, actions]);
  return owner && allowed === owner ? <Link to="/internal-requests/new">Create internal request</Link> : null;
}
export function InternalRequestIntakePage() {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending && !state.session?.mustChangePassword ? state.authorityGeneration : null;
  const [branch, setBranch] = useState(""); const [revision, setRevision] = useState(0);
  const [loaded, setLoaded] = useState<{ owner: typeof owner; branch: string; value: Options } | null>(null);
  const [type, setType] = useState(""); const [department, setDepartment] = useState(""); const [priority, setPriority] = useState("");
  const [requesterDepartment, setRequesterDepartment] = useState("");
  const [subject, setSubject] = useState(""); const [description, setDescription] = useState(""); const [deadline, setDeadline] = useState("");
  const [location, setLocation] = useState(""); const [files, setFiles] = useState<File[]>([]);
  const [message, setMessage] = useState(""); const [created, setCreated] = useState<number | null>(null); const [busy, setBusy] = useState(false);
  const sending = useRef(false); const controller = useRef<AbortController | null>(null);
  // Preserve the submission key after uncertain outcomes; the server's unique request number prevents duplicate creation.
  const submission = useRef(crypto.randomUUID());
  useEffect(() => {
    setLoaded(null); setType(""); setDepartment(""); setRequesterDepartment(""); setPriority(""); setLocation(""); setFiles([]); setMessage(""); setBusy(false); sending.current = false;
    const abort = new AbortController(); controller.current = abort;
    if (owner) void json(`/api/internal-requests/intake${branch ? `?branchId=${branch}` : ""}`, abort.signal).then(options).then(value => {
      if (!abort.signal.aborted) {
        setLoaded({ owner, branch, value });
        setRequesterDepartment(value.requesterDepartments.length === 1 ? String(value.requesterDepartments[0].id) : "");
      }
    }).catch(e => { if (!abort.signal.aborted) { setMessage("Creation unavailable. Reload to check current access."); if (e instanceof Failure && e.status === 401) actions.invalidateSession(owner); } });
    return () => abort.abort();
  }, [owner, branch, revision, actions]);
  const view = owner && loaded?.owner === owner && loaded.branch === branch ? loaded.value : null;
  const material = view?.types.find(v => v.id === Number(type))?.materialNeed;
  async function submit() {
    const abort = controller.current;
    if (!owner || !view || !branch || material || sending.current || !abort || abort.signal.aborted) return;
    if (!type || !department || !priority || !subject.trim() || !description.trim() || !deadline || !Number.isFinite(Date.parse(deadline))) { setMessage("MSG03: Complete all required fields."); return; }
    if (files.length > 5 || files.some(f => f.size < 1 || f.size > 10 * 1024 * 1024)) { setMessage("MSG03: Upload up to five JPG, PNG or PDF files, at most 10 MiB each."); return; }
    sending.current = true; setBusy(true); setMessage("");
    try {
      const uploads = await Promise.all(files.map(async file => {
        const bytes = new Uint8Array(await file.arrayBuffer()); let binary = "";
        for (let offset = 0; offset < bytes.length; offset += 8192) binary += String.fromCharCode(...bytes.subarray(offset, offset + 8192));
        return { name: file.name, mimeType: file.type, base64: btoa(binary) };
      }));
      const csrf = await json("/api/auth/csrf", abort.signal) as { csrfToken?: unknown };
      if (typeof csrf?.csrfToken !== "string" || !csrf.csrfToken || abort.signal.aborted) throw new Error("Security check failed");
      const result = await json("/api/internal-requests/intake", abort.signal, { method: "POST", headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken },
        body: JSON.stringify({ submissionId: submission.current, branchId: Number(branch), typeId: Number(type), departmentId: Number(department), subject: subject.trim(), description: description.trim(),
          priorityId: Number(priority), desiredDeadline: new Date(`${deadline}:00+07:00`).toISOString(), roomId: location.startsWith("room:") ? Number(location.slice(5)) : null,
          areaId: location.startsWith("area:") ? Number(location.slice(5)) : null, requesterDepartmentId: requesterDepartment ? Number(requesterDepartment) : null, files: uploads }) }) as { requestId: number; status: string; message: string };
      if (!result || !Number.isSafeInteger(result.requestId) || result.requestId <= 0 || result.status !== "PENDING_ACCEPTANCE" || result.message !== "MSG09") throw new Error("Creation not confirmed");
      if (!abort.signal.aborted) { setCreated(result.requestId); setSubject(""); setDescription(""); setFiles([]); setLoaded(null); setMessage("MSG09: Request created. Pending Acceptance."); }
    } catch (e) {
      if (!abort.signal.aborted) {
        setLoaded(null); setFiles([]);
        setMessage(e instanceof Failure && e.code === "MATERIAL_REQUEST_REQUIRED" ? "MSG03: Create a material request instead (UC091)."
          : e instanceof Failure && e.code === "IR_SUBMISSION_ALREADY_USED" ? "Submission already used. Check your sent requests before creating another."
          : e instanceof Failure && e.code === "IR_REQUESTER_MEMBERSHIP_CHANGED" ? "Your department membership changed. Reload to select a currently available department."
          : e instanceof Failure && e.status === 409 ? "Request conditions changed. Reload current options before submitting again."
          : e instanceof Failure && e.status === 400 ? "MSG03: Check required fields, future deadline, current branch references and attachment format. Reload to correct the request."
          : "Creation not confirmed. Check your sent requests, then reload current access. No automatic retry was sent.");
        if (e instanceof Failure && e.status === 401) actions.invalidateSession(owner);
      }
    } finally { if (!abort.signal.aborted) { sending.current = false; setBusy(false); } }
  }
  if (!owner) return null;
  const select = (label: string, value: string, items: Option[], change: (value: string) => void) => <label>{label}<select required aria-label={label} value={value} disabled={busy} onChange={e => change(e.target.value)}><option value="">Select</option>{items.map(v => <option key={v.id} value={v.id}>{v.name}</option>)}</select></label>;
  return <main><h1>Create internal request</h1><Link to="/internal-requests">Internal requests</Link>
    {message && <p role="status">{message}</p>}
    {created ? <Link to={`/internal-requests?requestId=${created}`}>Open created request</Link> : <>
      <button disabled={busy} onClick={() => setRevision(v => v + 1)}>Reload creation</button>
      {view && <form aria-label="Create internal request" onSubmit={e => { e.preventDefault(); void submit(); }}>
        {select("Branch", branch, view.branches, setBranch)}
        {branch && <>
          {view.requesterDepartments.length > 0 && <label>Your department<select aria-label="Your department" value={requesterDepartment} disabled={busy} onChange={e => setRequesterDepartment(e.target.value)}>{view.requesterDepartments.length > 1 && <option value="">Not specified</option>}{view.requesterDepartments.map(v => <option key={v.id} value={v.id}>{v.name}</option>)}</select></label>}
          {select("Request type", type, view.types, setType)}{select("Receiving department", department, view.departments, setDepartment)}
          {select("Priority", priority, view.priorities, setPriority)}
          <label>Title<input required maxLength={200} value={subject} disabled={busy} onChange={e => setSubject(e.target.value)} /></label>
          <label>Description<textarea required maxLength={4000} value={description} disabled={busy} onChange={e => setDescription(e.target.value)} /></label>
          <label>Desired deadline (UTC+7)<input type="datetime-local" required value={deadline} disabled={busy} onChange={e => setDeadline(e.target.value)} /></label>
          <label>Location<select value={location} disabled={busy} onChange={e => setLocation(e.target.value)}><option value="">No location</option>{view.rooms.map(v => <option key={`room:${v.id}`} value={`room:${v.id}`}>Room {v.name}</option>)}{view.areas.map(v => <option key={`area:${v.id}`} value={`area:${v.id}`}>Area {v.name}</option>)}</select></label>
          <label>Attachments<input key={`${branch}:${revision}`} type="file" multiple accept="image/jpeg,image/png,application/pdf" disabled={busy} onChange={e => setFiles(Array.from(e.target.files ?? []))} /></label>
          {material && <p role="alert">MSG03: Create a material request instead (UC091).</p>}
          <button disabled={busy || !!material} type="submit">Submit request</button>
        </>}
      </form>}
    </>}
  </main>;
}
