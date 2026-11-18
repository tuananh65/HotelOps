import { displayCode } from "../i18n/vi";
import { useEffect, useRef, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";

type Preview = { assetId: number; assetCode: string; assetName: string; lifecycleStatus: string; version: string;
  blockers: { kind: string; count: number }[]; canRetire: boolean };
const labels: Record<string, string> = { OPEN_INCIDENT: "Sự cố chưa đóng", OPEN_MAINTENANCE_WORK: "Công việc bảo trì chưa đóng",
  ACTIVE_PREVENTIVE_SCHEDULE: "Lịch bảo trì còn Active", PENDING_HANDOVER: "Bàn giao đang chờ",
  OPEN_OR_UNPROVEN_OCCURRENCE: "Occurrence còn mở hoặc thiếu bằng chứng kết thúc", OCCURRENCE_PROVENANCE_UNAVAILABLE: "Chưa có nguồn bằng chứng occurrence",
  OPEN_WORK_INTERVAL: "Khoảng làm việc còn mở", RETIREMENT_WRITER_GUARDS_NOT_INSTALLED: "Chưa cài đặt migration bảo vệ retirement" };

export function AssetRetirementPage() {
  const { id } = useParams(); const navigate = useNavigate(); const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const current = useRef(owner); current.current = owner;
  const writer = useRef<AbortController | null>(null);
  const [preview, setPreview] = useState<Preview | null>(null), [loadedOwner, setLoadedOwner] = useState(owner);
  const [reason, setReason] = useState(""), [confirmed, setConfirmed] = useState(false), [busy, setBusy] = useState(false);
  const [error, setError] = useState(""), [revision, setRevision] = useState(0);
  useEffect(() => {
    setPreview(null); setError(""); setConfirmed(false); writer.current?.abort(); setBusy(false);
    if (!owner || !id || !/^[1-9]\d*$/.test(id) || !Number.isSafeInteger(Number(id))) return;
    const abort = new AbortController();
    void (async () => {
      const response = await fetch(`/api/asset-retirement/${id}`, { signal: abort.signal, credentials: "same-origin", cache: "no-store", redirect: "error" });
      if (response.status === 401) actions.invalidateSession(owner);
      if (!response.ok) { const problem = await response.json().catch(() => null); throw new Error(problem?.detail ?? `Không kiểm tra được điều kiện (${response.status}).`); }
      const body = await response.json() as Preview;
      if (!abort.signal.aborted && current.current === owner) { setPreview(body); setLoadedOwner(owner); }
    })().catch(e => { if (!abort.signal.aborted) setError(e instanceof Error ? e.message : "Không kiểm tra được điều kiện."); });
    return () => { abort.abort(); writer.current?.abort(); };
  }, [id, owner, actions, revision]);
  async function retire() {
    if (!owner || loadedOwner !== owner || !preview?.canRetire || !confirmed || reason.trim().length < 10 || busy) return;
    const abort = new AbortController(); writer.current = abort; setBusy(true); setError("");
    try {
      const csrf = await fetch("/api/auth/csrf", { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      if (csrf.status === 401) actions.invalidateSession(owner);
      const token = await csrf.json();
      if (!csrf.ok || token.headerName !== "X-CSRF-TOKEN" || typeof token.csrfToken !== "string" || !token.csrfToken) throw new Error("Chưa khởi tạo được bảo vệ phiên (CSRF).");
      if (current.current !== owner || abort.signal.aborted) return;
      const response = await fetch(`/api/asset-retirement/${id}`, { method: "POST", credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": token.csrfToken, "If-Match": preview.version }, body: JSON.stringify({ reason: reason.trim(), confirmed }) });
      if (response.status === 401) actions.invalidateSession(owner);
      if (!response.ok) { const problem = await response.json().catch(() => null); throw new Error(problem?.detail ?? `Retirement thất bại (${response.status}).`); }
      if (!abort.signal.aborted && current.current === owner) navigate(`/assets/${id}`, { replace: true, state: { message: "MSG08 — Đã chuyển tài sản sang Retired; lịch sử được giữ nguyên." } });
    } catch (e) { if (!abort.signal.aborted && current.current === owner) { setPreview(null); setConfirmed(false); setError(e instanceof Error ? e.message : "Kết quả chưa xác định; tải lại trước khi thử lại."); } }
    finally { if (writer.current === abort) { writer.current = null; setBusy(false); } }
  }
  if (!owner || owner !== loadedOwner) return <p>Đang kiểm tra quyền…</p>;
  return <section><h2>Retire tài sản</h2><Link to={`/assets/${id}`}>Hồ sơ tài sản</Link>
    {error && <p role="alert">{error}</p>}<p><button disabled={busy} onClick={() => setRevision(v => v + 1)}>Kiểm tra lại điều kiện</button></p>
    {preview && <><h3>{preview.assetCode} — {preview.assetName}</h3><p>Vòng đời: {displayCode(preview.lifecycleStatus)}</p>
      {preview.blockers.length > 0 && <><p>MSG34 — Chưa thể retire:</p><ul>{preview.blockers.map(b => <li key={b.kind}>{labels[b.kind] ?? b.kind}: {b.count}</li>)}</ul></>}
      {preview.canRetire && <form onSubmit={e => { e.preventDefault(); void retire(); }}><fieldset disabled={busy}>
        <legend>MSG33 — Xác nhận ngừng sử dụng tài sản</legend><p>Lịch sử và chứng từ sẽ được giữ nguyên. Tài sản không được tính vào tập đang hoạt động.</p>
        <label>Lý do <textarea required minLength={10} maxLength={4000} value={reason} onChange={e => setReason(e.target.value)} /></label>
        <label><input type="checkbox" checked={confirmed} onChange={e => setConfirmed(e.target.checked)} />Tôi xác nhận retire tài sản này.</label>
        <button disabled={!confirmed || reason.trim().length < 10} type="submit">{busy ? "Đang lưu…" : "Xác nhận Retire"}</button>
      </fieldset></form>}
    </>}
  </section>;
}
