import { displayCode } from "../i18n/vi";
import { useEffect, useRef, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";

type Receipt = { documentId: number; documentNo: string; status: string; rowVersion: string;
  lines: { lineId: number; itemId: number; uomId: number }[] };
type Detail = { canPost: boolean; approvalValid: boolean; document: Receipt; originals: { returnLineId: number; originalIssueLineId: number; originalDocumentNo: string; itemName: string; uomName: string }[]; expectedQuantities: string[]; receivedQuantities: { lineId: number; accepted: string; damaged: string | null; reason: string | null }[] };
type Entry = { lineId: number; acceptedQty: string; rejectedQty: string; damagedQty: string; discrepancyReason: string };

export function ReturnPostPage() {
  const { id: rawId = "" } = useParams();
  const id = /^[1-9]\d*$/.test(rawId) && Number.isSafeInteger(Number(rawId)) ? Number(rawId) : 0;
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const current = useRef(owner); current.current = owner;
  const loadedVersion = useRef<{ owner: typeof owner; version: string } | null>(null);
  const [data, setData] = useState<{ owner: typeof owner; id: number; value: Detail } | null>(null);
  const [lines, setLines] = useState<Entry[]>([]), [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false), [uncertain, setUncertain] = useState(false), [revision, setRevision] = useState(0);
  const writer = useRef<AbortController | null>(null);
  const receipt = data?.owner === owner && data.id === id ? data.value : null;
  useEffect(() => {
    setData(null); if (!owner || !id) { setLines([]); return; }
    const abort = new AbortController();
    void (async () => {
      try {
        const response = await fetch(`/api/return-workspace/${id}`, { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
        const value = await response.json();
        if (response.status === 401) actions.invalidateSession(owner);
        if (!response.ok) throw new Error(value.detail ?? `Hoàn trả chưa khả dụng (${response.status}).`);
        if (abort.signal.aborted || current.current !== owner) return;
        setData({ owner, id, value });
        if (loadedVersion.current?.owner !== owner || loadedVersion.current?.version !== value.document.rowVersion)
        setLines(value.document.lines.map((l: { lineId: number }, i: number) => ({ lineId: l.lineId, acceptedQty: value.expectedQuantities[i], rejectedQty: "0", damagedQty: "0", discrepancyReason: "" })));
        loadedVersion.current = { owner, version: value.document.rowVersion };
        if (value.document.status === "POSTED") { setUncertain(false); setMessage("MSG28: Phiếu hoàn trả đã ghi sổ. Không nhập thêm tồn kho."); }
      } catch (e) { if (!abort.signal.aborted) setMessage(e instanceof Error ? e.message : "Hoàn trả chưa khả dụng."); }
    })();
    // Revalidate current authority; preserve entered quantities only for the same source version.
    const refresh = () => { if (!writer.current) { setData(null); setRevision(v => v + 1); } };
    window.addEventListener("focus", refresh); const timer = window.setInterval(refresh, 30000);
    return () => { abort.abort(); clearInterval(timer); window.removeEventListener("focus", refresh); };
  }, [owner, id, revision, actions]);
  useEffect(() => () => writer.current?.abort(), [owner, id]);
  async function post() {
    if (!owner || !receipt || busy || uncertain || receipt.document.status !== "DRAFT" || !receipt.canPost) return;
    if (!window.confirm("Ghi sổ số lượng đã chấp nhận và xem xét? Không thể sửa tồn kho đã ghi sổ.")) return;
    const abort = new AbortController(); writer.current = abort; setBusy(true); setMessage(""); let sent = false;
    try {
      const bootstrap = await fetch("/api/auth/csrf", { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      const csrf = await bootstrap.json();
      if (!bootstrap.ok || csrf.headerName !== "X-CSRF-TOKEN" || !csrf.csrfToken) throw new Error("Chưa có bảo vệ phiên (CSRF).");
      if (abort.signal.aborted || current.current !== owner) return;
      sent = true;
      const response = await fetch(`/api/inventory/returns/${id}/post`, { method: "POST", credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken }, body: JSON.stringify({ expectedRowVersion: receipt.document.rowVersion, lines }) });
      if (response.status === 401) actions.invalidateSession(owner);
      if (!response.ok) { if (response.status < 500) sent = false; if ([401, 403].includes(response.status)) setData(null); throw new Error((await response.json()).detail ?? `Ghi sổ thất bại (${response.status}).`); }
      if (current.current === owner && !abort.signal.aborted) { setMessage("MSG08: Đã ghi sổ hoàn trả."); setRevision(v => v + 1); }
    } catch (e) { if (sent) setUncertain(true); if (!abort.signal.aborted) setMessage(`${e instanceof Error ? e.message : "Ghi sổ thất bại."}${sent ? "Chưa xác định kết quả. Tải lại phiếu hoàn trả trước mọi thao tác tiếp theo." : ""}`); }
    finally { if (writer.current === abort) { writer.current = null; setBusy(false); } }
  }
  return <section aria-label="Kiểm tra và ghi sổ hoàn trả"><h2>Xác minh và ghi sổ</h2>{message && <p role="status">{message}</p>}
    <button disabled={busy} onClick={() => setRevision(v => v + 1)}>Tải lại hoàn trả</button>
    {receipt && <><p>{receipt.document.documentNo}: {displayCode(receipt.document.status)}</p>
      {receipt.document.status !== "DRAFT" && <section aria-label="Kết quả hoàn trả đã ghi nhận">
        <p>Đây là số hoàn trả đã ghi nhận. Chứng từ hủy hoặc đảo không chứng minh tồn khả dụng.</p>
        <ul>{receipt.receivedQuantities.map(line => {
          const source = receipt.originals.find(o => o.returnLineId === line.lineId);
          return <li key={line.lineId}>{source?.itemName ?? String(line.lineId)}: dùng được {line.accepted}, Hư hỏng {line.damaged ?? "N/A"} {source?.uomName ?? ""}; phiếu xuất gốc {source?.originalDocumentNo ?? "N/A"}.
            {line.reason && <p>{line.reason}</p>}</li>;
        })}</ul>
      </section>}
      {receipt.document.status === "DRAFT" && !receipt.canPost && <p>Cần phê duyệt độc lập trước khi ghi sổ. <Link to={`/inventory/returns/${id}/approve`}>Xem xét phê duyệt (cần quyền riêng)</Link></p>}
      {receipt.document.status === "DRAFT" && <form onSubmit={e => { e.preventDefault(); void post(); }}><fieldset disabled={busy || uncertain || !receipt.canPost}>
        <p>Chỉ số lượng dùng được đã chấp nhận tăng tồn. Số hỏng ghi riêng. Giải thích hư hỏng hoặc thiếu so với khai báo hoàn trả.</p>
        {lines.map((line, index) => <fieldset key={line.lineId}><legend>{receipt.originals.find(o => o.returnLineId === line.lineId)?.itemName ?? `Vật tư ${receipt.document.lines[index].itemId}`}, {receipt.originals.find(o => o.returnLineId === line.lineId)?.uomName ?? `Đơn vị ${receipt.document.lines[index].uomId}`}; đã khai báo {receipt.expectedQuantities[index]}; phiếu xuất gốc {receipt.originals.find(o => o.returnLineId === line.lineId)?.originalDocumentNo ?? "unavailable"}, Dòng {receipt.originals.find(o => o.returnLineId === line.lineId)?.originalIssueLineId ?? "unavailable"}</legend>
          {(["acceptedQty", "damagedQty"] as const).map(field => <label key={field}>{field}<input required inputMode="decimal" pattern="[0-9]+([.][0-9]{1,3})?" value={line[field]} onChange={e => setLines(v => v.map(x => x.lineId === line.lineId ? { ...x, [field]: e.target.value } : x))} /></label>)}
          <label>Lý do chênh lệch <input value={line.discrepancyReason} onChange={e => setLines(v => v.map(x => x.lineId === line.lineId ? { ...x, discrepancyReason: e.target.value } : x))} /></label>
        </fieldset>)}<button>Xác nhận và ghi sổ</button></fieldset></form>}
    </>}
  </section>;
}
