import { referenceLabel } from "../i18n/referenceLabel";
import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";

type Receipt = { auditId: number; workId: number; previousAssigneeId: number; assigneeId: number; occurredAt: string };
export function MaintenanceAssignmentReceipt() {
  const { id = "" } = useParams(), { state } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const [result, setResult] = useState<{ owner: typeof owner; id: string; value?: Receipt; failed?: boolean } | null>(null);
  useEffect(() => {
    if (!owner) return;
    const abort = new AbortController();
    if (!/^[1-9][0-9]*$/.test(id) || !Number.isSafeInteger(Number(id))) { setResult({ owner, id, failed: true }); return; }
    fetch(`/api/maintenance-assignment-receipts/${id}`, { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal })
      .then(async response => {
        if (!response.ok) throw Error("Phiếu nhập chưa khả dụng");
        const r: unknown = await response.json();
        if (!isObject(r) || r.auditId !== Number(id) || r.previousAssigneeId !== Number(state.session?.user.id)
          || ![r.workId, r.assigneeId].every(n => typeof n === "number" && Number.isSafeInteger(n) && n > 0)
          || typeof r.occurredAt !== "string" || !r.occurredAt.endsWith("Z") || !Number.isFinite(Date.parse(r.occurredAt))) throw Error("Phiếu nhập không hợp lệ");
        if (!abort.signal.aborted) setResult({ owner, id, value: r as Receipt });
      }).catch(() => { if (!abort.signal.aborted) setResult({ owner, id, failed: true }); });
    return () => abort.abort();
  }, [id, owner, state.session]);
  const current = result?.id === id && result.owner === owner ? result : null;
  return <main><h1>Xác nhận phân công lại bảo trì</h1><Link to="/notifications">Thông báo</Link>
    {!owner || !current ? <p>Đang kiểm tra quyền truy cập hiện tại…</p> : current.failed ? <p role="alert">Biên nhận chưa khả dụng. Quyền của bạn có thể đã đổi.</p>
      : current.value && <dl><dt>Công việc</dt><dd>{referenceLabel(current.value, "workId")}</dd><dt>Kỹ thuật viên được phân công lại</dt><dd>{referenceLabel(current.value, "assigneeId")}</dd>
        <dt>Phân công lại lúc</dt><dd>{new Intl.DateTimeFormat("vi-VN", { timeZone: "Asia/Ho_Chi_Minh", dateStyle: "short", timeStyle: "medium" }).format(new Date(current.value.occurredAt))}</dd></dl>}
  </main>;
}
