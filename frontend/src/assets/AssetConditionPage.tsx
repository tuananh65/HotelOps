import { displayCode } from "../i18n/vi";
import { useEffect, useRef, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";

type View = { assetCode: string; assetName: string; conditionStatus: string; lifecycleStatus: string; version: string;
  incidentAccess: string; incidents: { incidentId: number; incidentNo: string; status: string }[];
  repairs: { auditId: number; incidentId: number; incidentNo: string; acceptedAt: string }[] };
export function AssetConditionPage() {
  const { id } = useParams(); const navigate = useNavigate(); const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending && !state.session?.mustChangePassword ? state.authorityGeneration : null;
  const key = `${owner}:${id}`, live = useRef(key); live.current = key;
  const writer = useRef<AbortController | null>(null);
  const [view, setView] = useState<{ key: string; data: View } | null>(null), [revision, setRevision] = useState(0);
  const [action, setAction] = useState("DAMAGED"), [note, setNote] = useState(""), [reason, setReason] = useState("");
  const [condition, setCondition] = useState(""), [repair, setRepair] = useState("");
  const [busy, setBusy] = useState(false), [error, setError] = useState("");
  useEffect(() => {
    setView(null); setError(""); setNote(""); setReason(""); setCondition(""); setRepair("");
    if (!owner || !id || !/^[1-9]\d*$/.test(id) || !Number.isSafeInteger(Number(id))) return;
    const abort = new AbortController();
    void fetch(`/api/asset-condition/${id}`, { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal })
      .then(async r => { if (r.status === 401) actions.invalidateSession(owner); const data = await r.json();
        if (!r.ok) throw Error(data.detail ?? `Tình trạng chưa khả dụng (${r.status}).`);
        if (!abort.signal.aborted && live.current === key) setView({ key, data }); })
      .catch(e => { if (!abort.signal.aborted && live.current === key) setError(String(e)); });
    return () => { abort.abort(); writer.current?.abort(); };
  }, [owner, id, key, revision, actions]);
  const data = owner && view?.key === key ? view.data : null;
  async function save() {
    if (!data || !owner || writer.current) return;
    if (!note.trim() || action === "REPAIRED" && (!condition || !repair)) { setError("Nhập ghi chú và chọn bằng chứng sửa chữa / tình trạng sau kiểm tra nếu áp dụng."); return; }
    const abort = new AbortController(); writer.current = abort; setBusy(true); setError("");
    try {
      const csrf = await fetch("/api/auth/csrf", { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      if (csrf.status === 401) actions.invalidateSession(owner); const token = await csrf.json();
      if (!csrf.ok || token.headerName !== "X-CSRF-TOKEN" || typeof token.csrfToken !== "string" || !token.csrfToken) throw Error("Chưa khởi tạo được bảo vệ phiên (CSRF).");
      if (abort.signal.aborted || live.current !== key) return;
      const response = await fetch(`/api/asset-condition/${id}`, { method: "POST", credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": token.csrfToken, "If-Match": data.version },
        body: JSON.stringify({ action, note, openIncidentReason: reason || null, condition: action === "REPAIRED" ? condition : null, closureAuditId: action === "REPAIRED" ? Number(repair) : null }) });
      if (response.status === 401) actions.invalidateSession(owner); const result = await response.json();
      if (!response.ok) throw Error(result.detail ?? `Cập nhật tình trạng thất bại (${response.status}).`);
      if (!abort.signal.aborted && live.current === key) navigate(`/assets/${id}`, { replace: true, state: { message: "MSG08 - Đã lưu tình trạng tài sản." } });
    } catch (e) { if (!abort.signal.aborted && live.current === key) { setView(null); setError(String(e)); } }
    finally { if (writer.current === abort) { writer.current = null; setBusy(false); } }
  }
  return <section><h1>Cập nhật tình trạng tài sản</h1><Link to={`/assets/${id}`}>Hồ sơ tài sản</Link>
    {error && <p role="alert">{error}</p>}<button disabled={busy} onClick={() => setRevision(v => v + 1)}>Tải lại trạng thái hiện tại</button>
    {data && <><h2>{data.assetCode} - {data.assetName}</h2><p>Tình trạng: {displayCode(data.conditionStatus)}; Vòng đời: {displayCode(data.lifecycleStatus)}</p>
      <h3>Sự cố liên quan đang mở được phép xem</h3>{data.incidentAccess === "ACCESS_DENIED" && <p>ACCESS_DENIED: Cần quyền xem sự cố riêng.</p>}
      {data.incidents.map(i => <p key={i.incidentId}><Link to={`/operations/incidents/${i.incidentId}?action=A004A-023`}>{i.incidentNo}</Link> - {displayCode(i.status)}</p>)}
      <fieldset disabled={busy}><legend>Ghi nhận thay đổi tình trạng</legend>
        <label>Thao tác <select value={action} onChange={e => setAction(e.target.value)}>{["DAMAGED", "UNDER_REPAIR", "REPAIRED", "LOST"].map(a => <option key={a} value={a}>{displayCode(a)}</option>)}</select></label>
        {action === "DAMAGED" && <p>Nếu chưa có sự cố được báo, <Link to="/operations/incidents?action=A004A-021">báo sự cố</Link>. Reporting requires its own permission.</p>}
        {action === "REPAIRED" && <><label>Kết quả sửa chữa đã chấp nhận <select value={repair} onChange={e => setRepair(e.target.value)}><option value="">Chọn</option>{data.repairs.map(r => <option key={r.auditId} value={r.auditId}>{displayCode(r.incidentNo)} - {displayCode(r.acceptedAt)}</option>)}</select></label>
          <label>Tình trạng sau kiểm tra <select value={condition} onChange={e => setCondition(e.target.value)}><option value="">Chọn</option>{["GOOD", "FAIR", "POOR"].map(c => <option key={c} value={c}>{displayCode(c)}</option>)}</select></label>
          <label>Lý do khi còn sự cố khác chưa đóng <textarea maxLength={4000} value={reason} onChange={e => setReason(e.target.value)} /></label>
          {data.repairs.length === 0 && <p>Không có bằng chứng sửa chữa đã chấp nhận được phép xem.</p>}</>}
        <label>Ghi chú <textarea required maxLength={4000} value={note} onChange={e => setNote(e.target.value)} /></label>
        {action === "LOST" && <p>Đánh dấu mất sẽ loại tài sản khỏi danh sách khả dụng. Trách nhiệm quản lý và lịch sử giữ nguyên.</p>}
        <button onClick={() => void save()}>Lưu tình trạng</button>
      </fieldset></>}
  </section>;
}
