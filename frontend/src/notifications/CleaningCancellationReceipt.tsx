import { referenceLabel } from "../i18n/referenceLabel";
import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { ApiError, getJson } from "../api/dev1/client";

type Receipt = { taskId: number; branchId: number; assignmentId: number; cancelledAtUtc: string; state: "CANCELLED" };

export function CleaningCancellationReceipt() {
  const { id = "" } = useParams();
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && state.session && !state.session.mustChangePassword
    && state.pending === null ? state.authorityGeneration : null;
  const [result, setResult] = useState<{ owner: typeof owner; id: string; data?: Receipt; failed?: boolean } | null>(null);
  useEffect(() => {
    if (!owner) return;
    const abort = new AbortController();
    setResult(null);
    if (!/^[1-9][0-9]*$/.test(id) || !Number.isSafeInteger(Number(id))) {
      setResult({ owner, id, failed: true }); return;
    }
    getJson(`/notifications/${id}/cleaning-cancellation`, abort.signal).then(value => {
      const r = value as Receipt;
      if (!r || r.state !== "CANCELLED" || ![r.taskId, r.branchId, r.assignmentId].every(n=>Number.isSafeInteger(n) && n>0)
        || typeof r.cancelledAtUtc !== "string" || !r.cancelledAtUtc.endsWith("Z") || !Number.isFinite(Date.parse(r.cancelledAtUtc)))
        throw new Error("INVALID_RECEIPT");
      if (!abort.signal.aborted) setResult({ owner, id, data: r });
    }).catch(error => {
      if (abort.signal.aborted) return;
      setResult({ owner, id, failed: true });
      if (error instanceof ApiError && error.status === 401) actions.invalidateSession(owner);
    });
    return () => abort.abort();
  }, [id, owner, actions]);
  const current = result?.owner === owner && result?.id === id ? result : null;
  return <main><h1>Biên nhận hủy công việc dọn phòng</h1><Link to="/notifications">Thông báo</Link>
    {!owner ? <p>Đang xác minh phiên đăng nhập.</p> : !current ? <p>Đang tải…</p>
      : current.failed ? <p role="alert">Không thể truy cập biên nhận. Quyền truy cập có thể đã thay đổi.</p>
      : current.data && <dl><dt>Công việc</dt><dd>{referenceLabel(current.data, "taskId")}</dd><dt>Chi nhánh</dt><dd>{referenceLabel(current.data, "branchId")}</dd>
        <dt>Phân công</dt><dd>{referenceLabel(current.data, "assignmentId")}</dd><dt>Trạng thái</dt><dd>Đã hủy</dd>
        <dt>Thời điểm hủy</dt><dd>{new Date(current.data.cancelledAtUtc).toLocaleString()}</dd></dl>}
  </main>;
}
