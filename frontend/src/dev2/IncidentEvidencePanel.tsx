import { useEffect, useRef, useState } from "react";
import { createHttpClient, isObject } from "../auth/http";

type Item = { id: number; name: string; note: string | null; authorId: number; uploadedAt: string };
type Page = { incidentId: number; page: number; total: number; items: Item[]; version: string; canAdd: boolean;
  noteTotal: number; notes: { id: number; note: string | null; authorId: number; at: string }[] };
const request = createHttpClient();
export function IncidentEvidencePanel({ incidentId }: { incidentId: number }) {
  const [page, setPage] = useState(1);
  const [revision, setRevision] = useState(0);
  const [loaded, setLoaded] = useState<{ key: string; data?: Page; error?: string }>();
  const [note, setNote] = useState("");
  const [files, setFiles] = useState<File[]>([]);
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const sending = useRef(false);
  const writer = useRef<AbortController | null>(null);
  useEffect(() => () => writer.current?.abort(), []);
  const key = `${incidentId}:${page}:${revision}`;
  useEffect(() => {
    const abort = new AbortController();
    void request<Page>({ method: "GET", url: `/api/incidents/${incidentId}/evidence?page=${page}`, expectedStatus: 200, signal: abort.signal,
      decode: raw => {
        if (!isObject(raw) || raw.incidentId !== incidentId || raw.page !== page || !Number.isSafeInteger(raw.total)
          || typeof raw.version !== "string" || !/^[A-Za-z0-9+/]{43}=$/.test(raw.version) || typeof raw.canAdd !== "boolean"
          || !Number.isSafeInteger(raw.noteTotal) || Number(raw.noteTotal) < 0 || !Array.isArray(raw.notes)
          || raw.notes.length !== Math.min(25, Math.max(0, Number(raw.noteTotal) - (page - 1) * 25))
          || raw.notes.some(n => !isObject(n) || !Number.isSafeInteger(n.id) || Number(n.id) <= 0
            || !(n.note === null || typeof n.note === "string") || !Number.isSafeInteger(n.authorId) || Number(n.authorId) <= 0
            || typeof n.at !== "string" || !Number.isFinite(Date.parse(n.at)))
          || Number(raw.total) < 0 || !Array.isArray(raw.items) || raw.items.length !== Math.min(25, Math.max(0, Number(raw.total) - (page - 1) * 25))) return undefined;
        const ids = new Set<number>();
        for (const item of raw.items) {
          if (!isObject(item) || typeof item.id !== "number" || !Number.isSafeInteger(item.id) || item.id <= 0 || ids.has(item.id)
            || typeof item.name !== "string" || !(item.note === null || typeof item.note === "string")
            || typeof item.authorId !== "number" || !Number.isSafeInteger(item.authorId) || item.authorId <= 0
            || typeof item.uploadedAt !== "string" || !Number.isFinite(Date.parse(item.uploadedAt))) return undefined;
          ids.add(item.id);
        }
        return raw as unknown as Page;
      },
    }).then(result => { if (!abort.signal.aborted) setLoaded({ key, ...(result.ok ? { data: result.data }
      : { error: "Incident evidence is unavailable. Reload to check current access." }) }); });
    return () => abort.abort();
  }, [incidentId, page, key]);
  const current = loaded?.key === key ? loaded : undefined;
  async function save() {
    if (!current?.data?.canAdd || sending.current) return;
    if ((!note.trim() && !files.length) || note.trim().length > 4000 || files.length > 5
      || files.some(f => f.size < 1 || f.size > 10 * 1024 * 1024 || !["image/jpeg", "image/png", "application/pdf"].includes(f.type))) {
      setMessage("Enter a note or up to five JPG, PNG or PDF files, at most 10 MiB each."); return;
    }
    const abort = new AbortController(); writer.current = abort;
    sending.current = true; setBusy(true); setMessage("");
    try {
      const uploads = await Promise.all(files.map(async file => {
        const bytes = new Uint8Array(await file.arrayBuffer());
        let binary = "";
        for (let offset = 0; offset < bytes.length; offset += 8192) binary += String.fromCharCode(...bytes.subarray(offset, offset + 8192));
        return { name: file.name, mimeType: file.type, base64: btoa(binary) };
      }));
      const csrf = await request({ method: "GET", url: "/api/auth/csrf", expectedStatus: 200, signal: abort.signal,
        decode: value => isObject(value) && typeof value.csrfToken === "string" && value.csrfToken ? value.csrfToken : undefined });
      if (!csrf.ok || abort.signal.aborted) throw Error("Session check failed. Reload before saving.");
      const response = await fetch(`/api/incidents/${incidentId}/evidence`, { method: "POST", credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.data, "If-Match": `"${current.data.version}"` },
        body: JSON.stringify({ note, files: uploads }) });
      if (!response.ok) throw Error(response.status === 412 ? "Incident changed. Reload evidence before retrying."
        : response.status === 409 ? "MSG24: Incident is closed or cancelled. Evidence is read-only."
        : response.status === 400 ? "MSG23: Check the note and file format, size and content before retrying."
        : "Save not confirmed. Reload current access and evidence before retrying.");
      const result: unknown = await response.json();
      if (!isObject(result) || result.incidentId !== incidentId || !Number.isSafeInteger(result.eventId) || Number(result.eventId) <= 0
        || result.message !== "MSG08" || typeof result.version !== "string" || !/^[A-Za-z0-9+/]{43}=$/.test(result.version)) throw Error("Save response could not be confirmed. Reload evidence.");
      if (!abort.signal.aborted) { setNote(""); setFiles([]); setMessage("MSG08: Evidence saved."); setRevision(r => r + 1); }
    } catch (error) { if (!abort.signal.aborted) { setLoaded(undefined); setMessage(error instanceof Error ? error.message : "Save not confirmed. Reload evidence."); } }
    finally { if (!abort.signal.aborted) { sending.current = false; setBusy(false); } }
  }
  return <section aria-label="Incident evidence"><h3>Incident evidence</h3>
    <button type="button" disabled={busy} onClick={() => setRevision(r => r + 1)}>Reload evidence</button>
    {message && <p role="status">{message}</p>}
    {!current && <p role="status">Loading evidence…</p>}
    {current?.error && <p role="alert">{current.error}</p>}
    {current?.data && <>
      {!current.data.total && <p>No attachments recorded.</p>}
      <ul>{current.data.items.map(item => <li key={item.id}>
        <a href={`/api/incidents/${incidentId}/evidence/${item.id}`} download>{item.name}</a>
        <p>Uploaded by user {item.authorId} at {item.uploadedAt}</p>{item.note && <p>{item.note}</p>}
      </li>)}</ul>
      <ul>{current.data.notes.map(n => <li key={n.id}>{n.note ?? "Files added"} — user {n.authorId}, {n.at}</li>)}</ul>
      <button type="button" disabled={busy || page <= 1} onClick={() => setPage(p => p - 1)}>Previous evidence</button>
      <span> Page {page} </span><button type="button" disabled={busy || page * 25 >= Math.max(current.data.total, current.data.noteTotal)} onClick={() => setPage(p => p + 1)}>Next evidence</button>
      {current.data.canAdd && <fieldset disabled={busy}><legend>Add evidence</legend>
        <label>Note<textarea maxLength={4000} value={note} onChange={e => setNote(e.target.value)} /></label>
        <label>Files<input key={revision} type="file" multiple accept=".jpg,.jpeg,.png,.pdf" onChange={e => setFiles(Array.from(e.target.files ?? []))} /></label>
        <button type="button" onClick={() => void save()}>{busy ? "Saving…" : "Save evidence"}</button>
      </fieldset>}
    </>}
  </section>;
}
