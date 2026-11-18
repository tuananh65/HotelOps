import { displayCode } from "../i18n/vi";
import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";

type Queue = { items: { documentId: number; documentNo: string; status: string; createdAt: string; approvedAt: string | null }[]; nextAfter: number | null };
export function ReturnQueuePage() {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const current = useRef(owner); current.current = owner;
  const [mode, setMode] = useState("mine"), [after, setAfter] = useState(0), [revision, setRevision] = useState(0);
  const [snapshot, setSnapshot] = useState<{ owner: typeof owner; mode: string; after: number; value: Queue } | null>(null);
  const [message, setMessage] = useState("");
  const data = snapshot?.owner === owner && snapshot.mode === mode && snapshot.after === after ? snapshot.value : null;
  useEffect(() => {
    setSnapshot(null); setMessage(""); if (!owner) return;
    const abort = new AbortController();
    void (async () => {
      try {
        const response = await fetch(`/api/return-requests?mode=${mode}&after=${after}`, { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
        const value = await response.json();
        if (response.status === 401) actions.invalidateSession(owner);
        if (!response.ok) throw new Error(value.detail ?? `Hàng đợi chưa khả dụng (${response.status}).`);
        if (!abort.signal.aborted && current.current === owner) setSnapshot({ owner, mode, after, value });
      } catch (e) { if (!abort.signal.aborted) setMessage(e instanceof Error ? e.message : "Hàng đợi chưa khả dụng."); }
    })();
    const refresh = () => { setSnapshot(null); setRevision(v => v + 1); };
    const timer = window.setInterval(refresh, 30000); window.addEventListener("focus", refresh);
    return () => { abort.abort(); window.clearInterval(timer); window.removeEventListener("focus", refresh); };
  }, [owner, mode, after, revision, actions]);
  return <section aria-label="Hoàn trả vật tư"><h2>Hoàn trả vật tư</h2>
    <p>Tạo hoàn trả từ phiếu xuất đã ghi sổ trong lịch sử yêu cầu vật tư. Mỗi hàng đợi kiểm tra quyền riêng.</p>
    <label>Hàng đợi<select value={mode} onChange={e => { setMode(e.target.value); setAfter(0); }}>
      <option value="mine">Yêu cầu của tôi</option><option value="approve">Chờ phê duyệt độc lập</option><option value="post">Chờ kho tiếp nhận</option>
    </select></label><button onClick={() => { setAfter(0); setRevision(v => v + 1); }}>Tải lại trang đầu</button>
    {message && <p role="status">{message}</p>}
    {data && <><ul>{data.items.map(d => <li key={d.documentId}>
      <Link to={mode === "mine" ? `/inventory/return-requests/${d.documentId}` : `/inventory/returns/${d.documentId}${mode === "approve" ? "/approve" : ""}`}>{d.documentNo}</Link>
      {" "}{displayCode(d.status)}; Đã tạo {d.createdAt}; phê duyệt {d.approvedAt ?? "pending"}.
    </li>)}</ul>{data.items.length === 0 && <p>Không có phiếu hoàn trả được phép xem trên trang này.</p>}
      {data.nextAfter != null && <button onClick={() => setAfter(data.nextAfter!)}>Trang sau</button>}</>}
  </section>;
}
