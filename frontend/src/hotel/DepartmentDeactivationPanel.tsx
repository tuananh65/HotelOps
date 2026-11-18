import { useEffect, useMemo, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { strongTag } from "./contracts";
import { createDepartmentDeactivationApi } from "./departmentDeactivationApi";

type Review = { id: number; branchId: number; isActive: boolean; dependencies: { kind: string; count: number }[]; etag: string };
const labels: Record<string,string> = {
  ACTIVE_USERS: "Nhân viên đang hoạt động", WAREHOUSES: "Kho",
  OPEN_OR_UNPROVEN_TASKS: "Công việc chưa xác nhận đóng", OPEN_INTERNAL_REQUESTS: "Mở yêu cầu nội bộ",
  ASSETS_IN_CUSTODY: "Tài sản đang quản lý", PENDING_ASSET_HANDOVERS: "Bàn giao tài sản đang chờ",
  OPEN_MAINTENANCE: "Mở công việc bảo trì", WORKING_WORKDAYS: "Ngày làm việc đang diễn ra",
};

export function DepartmentDeactivationPanel({ branchId, departmentId, onSaved }: { branchId: number; departmentId: number; onSaved: () => void }) {
  const { state, actions } = useAuth();
  const identity = useMemo(() => ({}), [state.session, state.status, state.pending, branchId, departmentId]);
  const latest = useRef(identity); latest.current = identity;
  const [review, setReview] = useState<Review | null>(null);
  const [reason, setReason] = useState("");
  const [confirmed, setConfirmed] = useState(false);
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const [reload, setReload] = useState(0);
  const [api] = useState(() => createDepartmentDeactivationApi(globalThis.fetch.bind(globalThis), true));
  const controller = useRef<AbortController | null>(null);
  const pending = useRef(false);
  useEffect(() => {
    const abort = new AbortController(); controller.current = abort;
    setReview(null); setConfirmed(false); setReason(""); setMessage("");
    if (state.status !== "AUTHENTICATED" || state.pending !== null) return () => abort.abort();
    setBusy(true);
    void (async () => {
      try {
        const response = await fetch(`/api/branches/${branchId}/departments/${departmentId}/deactivation-review`, {
          credentials: "include", cache: "no-store", redirect: "error", signal: abort.signal,
          headers: { Accept: "application/json" },
        });
        if (abort.signal.aborted || latest.current !== identity) return;
        if (!response.ok) {
          setMessage(`Kiểm tra ràng buộc chưa khả dụng (${response.status}).`);
          if (response.status === 401 || response.status === 403) void actions.revalidate();
          return;
        }
        const data = await response.json() as Review;
        const etag = response.headers.get("ETag");
        if (!data || data.id !== departmentId || data.branchId !== branchId || typeof data.isActive !== "boolean"
          || !Array.isArray(data.dependencies) || !data.dependencies.every(d => d && typeof d.kind === "string" && Number.isSafeInteger(d.count) && d.count > 0) || !strongTag(etag))
          throw new Error("Xem xét không hợp lệ");
        if (!abort.signal.aborted && latest.current === identity) setReview({ ...data, etag });
      } catch { if (!abort.signal.aborted && latest.current === identity) setMessage("Chưa thể kiểm tra ràng buộc. Tải lại trước khi tiếp tục."); }
      finally { if (!abort.signal.aborted && latest.current === identity) setBusy(false); }
    })();
    return () => abort.abort();
  }, [identity, reload]);

  async function deactivate() {
    if (!review || !review.isActive || review.dependencies.length || !confirmed || pending.current || busy) return;
    pending.current = true; setBusy(true);
    const current = () => latest.current === identity && controller.current?.signal.aborted === false;
    try {
      const result = await api.change(branchId, departmentId, review.etag, reason, current, controller.current?.signal);
      if (!current()) return;
      setConfirmed(false);
      if (result.outcome === "CONFIRMED") {
        setReview({ ...review, isActive: false, etag: result.etag });
        setMessage("MSG08: Đã vô hiệu hóa bộ phận. Giữ nguyên lịch sử.");
        onSaved();
      } else {
        setReview(null);
        setMessage(result.outcome === "OUTCOME_UNKNOWN"
          ? "Chưa xác định kết quả. Tải lại hồ sơ máy chủ trước khi thay đổi tiếp."
          : "Thay đổi bị từ chối hoặc chưa gửi. Tải lại ràng buộc; phiên bản, quyền hoặc ràng buộc có thể đã đổi.");
        if (result.status === 401 || result.status === 403) void actions.revalidate();
      }
    } finally { pending.current = false; if (current()) setBusy(false); }
  }

  if (state.status !== "AUTHENTICATED" || state.pending !== null) return null;
  return <section aria-label="Vô hiệu hóa bộ phận">
    <h2>Vô hiệu hóa bộ phận</h2>
    <button type="button" disabled={busy} onClick={() => setReload(n => n + 1)}>Tải lại kiểm tra ràng buộc</button>
    {busy && <p role="status">Đang tải hoặc lưu…</p>}
    {message && <p role="status">{message}</p>}
    {review && !review.isActive && <p>Bộ phận đã ngừng hoạt động, không được chọn cho hồ sơ mới.</p>}
    {review && review.dependencies.length > 0 && <><p>MSG34: Xử lý các ràng buộc này trước khi vô hiệu hóa.</p>
      <ul>{review.dependencies.map(d => <li key={d.kind}>{labels[d.kind] ?? d.kind}: {d.count}</li>)}</ul></>}
    {review?.isActive && review.dependencies.length === 0 && <>
      <p>MSG33: Vô hiệu hóa bộ phận này? Lịch sử vẫn khả dụng.</p>
      <label>Lý do (10–1000 ký tự)<textarea minLength={10} maxLength={1000} value={reason} disabled={busy}
        onChange={e => { setReason(e.target.value); setConfirmed(false); }} /></label>
      <label><input type="checkbox" checked={confirmed} disabled={busy} onChange={e => setConfirmed(e.target.checked)} />Xác nhận vô hiệu hóa</label>
      <button type="button" disabled={busy || !confirmed || reason.trim().length < 10} onClick={() => void deactivate()}>Vô hiệu hóa</button>
    </>}
  </section>;
}
