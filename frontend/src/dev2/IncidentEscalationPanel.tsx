import { displayCode } from "../i18n/vi";
import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";

type Item = { escalationId: number; state: string; reason: string; requestedSupport: string; openedAt: string; closedAt: string | null };
type Context = { eTag: string | null; canCreate: boolean; severity: string | null; responseDeadlineAt: string | null; resolutionDeadlineAt: string | null;
  serverNow: string; timing: { classification: string; percentUsed: number | null; unavailableReason: string | null }; items: Item[] };
const date = (value: string | null) => value ? new Date(value).toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" }) + " UTC+7" : "Chưa có thông tin";

export function IncidentEscalationPanel({ id, onCommitted }: { id: number; onCommitted?: (notice: string) => void }) {
  const { state } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const current = useRef(owner); current.current = owner;
  const active = useRef<AbortController | null>(null), sending = useRef(false);
  const [snapshot, setSnapshot] = useState<{ owner: typeof owner; id: number; value: Context } | null>(null);
  const [message, setMessage] = useState<{ owner: typeof owner; text: string } | null>(null);
  const [reason, setReason] = useState(""), [support, setSupport] = useState("");
  const [busy, setBusy] = useState(false), [attempted, setAttempted] = useState(false), [review, setReview] = useState(false);
  const [reload, setReload] = useState(0);
  useEffect(() => {
    const abort = new AbortController(); active.current = abort;
    setSnapshot(null); setMessage(null); setAttempted(false); setReview(false); setReason(""); setSupport("");
    if (owner) {
      setBusy(true);
      void fetch(`/api/incidents/${id}/escalations`, { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal })
        .then(async response => {
          if (!response.ok) throw Error("Chưa có chi tiết chuyển cấp. Kiểm tra quyền và khả dụng dữ liệu.");
          const data: unknown = await response.json();
          if (!isObject(data) || !isObject(data.timing) || !Array.isArray(data.items) || typeof data.canCreate !== "boolean") throw Error("Phản hồi chuyển cấp không hợp lệ.");
          if (!abort.signal.aborted && current.current === owner) setSnapshot({ owner, id, value: data as unknown as Context });
        }).catch(error => { if (!abort.signal.aborted && current.current === owner) setMessage({ owner, text: String(error.message) }); })
        .finally(() => { if (!abort.signal.aborted) setBusy(false); });
    }
    return () => { abort.abort(); active.current?.abort(); };
  }, [owner, id, reload]);
  const data = snapshot?.owner === owner && snapshot.id === id ? snapshot.value : null;
  const valid = reason.trim().length >= 10 && reason.trim().length <= 4000 && support.trim().length > 0 && support.trim().length <= 4000;
  async function submit() {
    if (!owner || !data?.canCreate || !data.eTag || !/^"[0-9A-F]{16}"$/.test(data.eTag) || !review || !valid || attempted || sending.current) return;
    sending.current = true; setBusy(true); setAttempted(true);
    const abort = new AbortController(); active.current = abort;
    try {
      const bootstrap = await fetch("/api/auth/csrf", { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      if (!bootstrap.ok) throw Error("Không xác minh được phiên đăng nhập. Tải lại trước khi thử lại.");
      const csrf: unknown = await bootstrap.json();
      if (!isObject(csrf) || csrf.headerName !== "X-CSRF-TOKEN" || typeof csrf.csrfToken !== "string" || !/^[\x21-\x7e]+$/.test(csrf.csrfToken)
        || current.current !== owner || abort.signal.aborted) throw Error("Phiên đăng nhập đã thay đổi. Vui lòng tải lại.");
      const response = await fetch(`/api/incidents/${id}/escalations`, { method: "POST", credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": data.eTag }, body: JSON.stringify({ reason: reason.trim(), requestedSupport: support.trim() }) });
      if (!response.ok) throw Error(response.status === 412 ? "Sự cố đã thay đổi. Tải lại trước khi thử lại." : "Chưa xác nhận chuyển cấp. Tải lại để kiểm tra điều kiện, quyền và chuyển cấp đang mở.");
      const saved: unknown = await response.json();
      if (!isObject(saved) || saved.incidentId !== id || saved.status !== "OPEN" || typeof saved.eventId !== "number" || !Number.isSafeInteger(saved.eventId) || saved.eventId <= 0) throw Error("Chưa xác nhận phản hồi. Tải lại trước khi thử lại.");
      if (!abort.signal.aborted && current.current === owner) {
        const notice = "Đã lưu chuyển cấp. Thông báo cho quản lý vận hành đang chờ gửi đến người đủ điều kiện và cần xác nhận đã đọc.";
        setSnapshot(null); setMessage({ owner, text: notice }); onCommitted?.(notice);
      }
    } catch (error) { if (!abort.signal.aborted && current.current === owner) setMessage({ owner, text: error instanceof Error ? error.message : "Chưa xác nhận chuyển cấp xử lý. Vui lòng tải lại." }); }
    finally { sending.current = false; if (!abort.signal.aborted) { setBusy(false); setReview(false); } }
  }
  if (!owner) return null;
  return <section aria-label="Chuyển cấp xử lý sự cố"><h3>Chuyển cấp xử lý sự cố</h3>
    <button disabled={busy} onClick={() => setReload(n => n + 1)}>Tải lại chuyển cấp xử lý</button>
    {message?.owner === owner && <p role="status">{message.text}</p>}
    {data && <><p>Mức độ: {data.severity ?? "N/A"}. Response deadline: {date(data.responseDeadlineAt)}. Resolution deadline: {date(data.resolutionDeadlineAt)}.</p>
      <p>{data.timing.classification} — thời gian đã dùng: {data.timing.percentUsed == null ? "Chưa có thông tin" : `${data.timing.percentUsed.toFixed(1)}%`}; ngưỡng nguy cơ: 80%. Bản chụp dữ liệu máy chủ: {date(data.serverNow)}.</p>
      {data.timing.unavailableReason && <p>{data.timing.unavailableReason}</p>}
      {data.items.map(item => <article key={item.escalationId}><h4>Chuyển cấp xử lý #{item.escalationId} — {displayCode(item.state)}</h4>
        <p>Mở lúc: {date(item.openedAt)}{item.closedAt && `; Đã đóng: ${date(item.closedAt)}`}</p><p>Lý do: {item.reason}</p><p>Hỗ trợ được yêu cầu: {item.requestedSupport}</p></article>)}
      {data.canCreate && <><fieldset disabled={busy || attempted || review}>
        <label>Lý do (10–4000 ký tự)<textarea maxLength={4000} value={reason} onChange={e => setReason(e.target.value)} /></label>
        <label>Hỗ trợ được yêu cầu<textarea maxLength={4000} value={support} onChange={e => setSupport(e.target.value)} /></label>
      </fieldset>{review ? <><p>Tạo chuyển cấp và yêu cầu quản lý vận hành xác nhận?</p><button disabled={busy} onClick={() => void submit()}>Xác nhận chuyển cấp xử lý</button><button disabled={busy} onClick={() => setReview(false)}>Quay lại</button></>
        : <button disabled={!valid || busy || attempted} onClick={() => setReview(true)}>Xem lại chuyển cấp xử lý</button>}</>}
    </>}
  </section>;
}
