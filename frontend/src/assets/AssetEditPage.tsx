import { useEffect, useRef, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";

type Input = { assetCode: string; assetName: string; assetTypeId: number; serialNumber: string | null; brand: string | null; model: string | null; purchaseDate: string | null; warrantyExpiryDate: string | null; description: string | null };
type Editor = { asset: Input & { branchName: string; roomNumber: string | null; areaName: string | null; departmentName: string | null; custodianName: string | null; conditionStatus: string; lifecycleStatus: string }; version: string; categories: { id: number; name: string }[] };

export function AssetEditPage() {
  const { id } = useParams(); const navigate = useNavigate();
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const current = useRef(owner); current.current = owner;
  const writer = useRef<AbortController | null>(null);
  const [data, setData] = useState<Editor | null>(null), [input, setInput] = useState<Input | null>(null);
  const [loadedOwner, setLoadedOwner] = useState(owner), [revision, setRevision] = useState(0);
  const [error, setError] = useState(""), [busy, setBusy] = useState(false), [stale, setStale] = useState(false);
  useEffect(() => {
    setData(null); setInput(null); setError(""); setStale(false); writer.current?.abort(); setBusy(false);
    if (!owner || !id || !/^[1-9]\d*$/.test(id) || !Number.isSafeInteger(Number(id))) return;
    const abort = new AbortController();
    void (async () => {
      const response = await fetch(`/api/asset-editor/${id}`, { signal: abort.signal, credentials: "same-origin", cache: "no-store", redirect: "error" });
      if (response.status === 401) actions.invalidateSession(owner);
      if (!response.ok) { const body = await response.json().catch(() => null); throw new Error(body?.detail ?? `Không mở được form (${response.status}).`); }
      const result = await response.json() as Editor;
      if (!abort.signal.aborted && current.current === owner) { setData(result); setInput(result.asset); setLoadedOwner(owner); }
    })().catch(e => { if (!abort.signal.aborted) setError(e instanceof Error ? e.message : "Không mở được form."); });
    return () => { abort.abort(); writer.current?.abort(); };
  }, [id, owner, actions, revision]);
  async function save() {
    if (!owner || owner !== loadedOwner || !data || !input || busy || stale) return;
    const abort = new AbortController(); writer.current = abort; setBusy(true); setError("");
    try {
      const bootstrap = await fetch("/api/auth/csrf", { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      if (bootstrap.status === 401) actions.invalidateSession(owner);
      const token = await bootstrap.json();
      if (!bootstrap.ok || token.headerName !== "X-CSRF-TOKEN" || typeof token.csrfToken !== "string" || !token.csrfToken) throw new Error("CSRF bootstrap unavailable.");
      if (current.current !== owner || abort.signal.aborted) return;
      const response = await fetch(`/api/asset-editor/${id}`, { method: "PUT", credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": token.csrfToken, "If-Match": data.version },
        body: JSON.stringify({ assetCode: input.assetCode, assetName: input.assetName, assetTypeId: Number(input.assetTypeId), serialNumber: input.serialNumber,
          brand: input.brand, model: input.model, purchaseDate: input.purchaseDate || null, warrantyExpiryDate: input.warrantyExpiryDate || null, description: input.description }) });
      if (response.status === 401) actions.invalidateSession(owner);
      if (!response.ok) {
        const body = await response.json().catch(() => null);
        if ([401, 403, 412, 428, 503].includes(response.status)) setStale(true);
        throw new Error(body?.detail ?? `Lưu thất bại (${response.status}).`);
      }
      if (!abort.signal.aborted && current.current === owner) navigate(`/assets/${id}`, { replace: true, state: { message: "MSG08 — Đã lưu thông tin tài sản." } });
    } catch (e) {
      if (!abort.signal.aborted && current.current === owner) { setError(e instanceof Error ? e.message : "Kết quả chưa xác định; tải lại trước khi thử lại."); setStale(true); }
    } finally { if (writer.current === abort) { writer.current = null; setBusy(false); } }
  }
  if (!owner || loadedOwner !== owner) return <p>Đang kiểm tra quyền…</p>;
  const field = (key: keyof Input, value: string | number) => setInput(v => v ? { ...v, [key]: value } : null);
  return <section><h2>Cập nhật thông tin tài sản</h2><Link to={`/assets/${id}`}>Hồ sơ tài sản</Link>
    {error && <p role="alert">{error}</p>}
    {stale && <button disabled={busy} onClick={() => { if (window.confirm("Tải dữ liệu hiện tại và bỏ bản chỉnh sửa chưa lưu?")) setRevision(v => v + 1); }}>Tải lại</button>}
    {data && input && <form onSubmit={e => { e.preventDefault(); void save(); }}>
      <p>Chi nhánh: {data.asset.branchName}. Vị trí: {data.asset.roomNumber ?? data.asset.areaName ?? "Unassigned"}. Bộ phận/người giữ: {data.asset.departmentName ?? "N/A"} / {data.asset.custodianName ?? "N/A"}. Tình trạng: {data.asset.conditionStatus}; lifecycle: {data.asset.lifecycleStatus}.</p>
      <fieldset disabled={busy || stale}><legend>Thông tin nhận dạng</legend>
        <label>Mã <input required maxLength={50} value={input.assetCode} onChange={e => field("assetCode", e.target.value)} /></label>{" "}
        <label>Tên <input required maxLength={200} value={input.assetName} onChange={e => field("assetName", e.target.value)} /></label>{" "}
        <label>Loại <select required value={input.assetTypeId} onChange={e => field("assetTypeId", Number(e.target.value))}><option value="">Chọn loại Active</option>{data.categories.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}</select></label>
        {([['serialNumber', 'Serial'], ['brand', 'Hãng'], ['model', 'Model']] as const).map(([key, label]) => <label key={key}>{label} <input maxLength={100} value={input[key] ?? ""} onChange={e => field(key, e.target.value)} /></label>)}
        <label>Ngày mua <input type="date" value={input.purchaseDate ?? ""} onChange={e => field("purchaseDate", e.target.value)} /></label>{" "}
        <label>Hết bảo hành <input type="date" min={input.purchaseDate ?? undefined} value={input.warrantyExpiryDate ?? ""} onChange={e => field("warrantyExpiryDate", e.target.value)} /></label>
        <label>Mô tả <textarea value={input.description ?? ""} onChange={e => field("description", e.target.value)} /></label>
        <button type="submit">{busy ? "Đang lưu…" : "Lưu"}</button>
      </fieldset>
    </form>}
  </section>;
}
