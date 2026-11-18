import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { ApiError, getJson } from "../api/dev1/client";

type Followup = { assetId: number; branchId: number; assetName: string; conditionAtClosure: string; currentCondition: string; closedAtUtc: string };
export function AssetConditionFollowup() {
  const { id = "" } = useParams();
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && state.session && !state.session.mustChangePassword && state.pending === null
    ? state.authorityGeneration : null;
  const [result, setResult] = useState<{ owner: typeof owner; id: string; data?: Followup; failed?: boolean } | null>(null);
  useEffect(() => {
    if (!owner) return;
    const abort = new AbortController(); setResult(null);
    if (!/^[1-9][0-9]*$/.test(id) || !Number.isSafeInteger(Number(id))) { setResult({ owner, id, failed: true }); return; }
    getJson(`/notifications/${id}/asset-condition`, abort.signal).then(value => {
      const r = value as Followup;
      if (!r || ![r.assetId, r.branchId].every(n => Number.isSafeInteger(n) && n > 0)
        || typeof r.assetName !== "string" || typeof r.currentCondition !== "string"
        || !["DAMAGED", "UNDER_REPAIR"].includes(r.conditionAtClosure)
        || typeof r.closedAtUtc !== "string" || !r.closedAtUtc.endsWith("Z") || !Number.isFinite(Date.parse(r.closedAtUtc))) throw Error("INVALID_FOLLOWUP");
      if (!abort.signal.aborted) setResult({ owner, id, data: r });
    }).catch(error => {
      if (abort.signal.aborted) return;
      setResult({ owner, id, failed: true });
      if (error instanceof ApiError && error.status === 401) actions.invalidateSession(owner);
    });
    return () => abort.abort();
  }, [id, owner, actions]);
  const visible = owner && result?.owner === owner && result.id === id ? result : null;
  return <main><h1>Xem lại tình trạng tài sản</h1><Link to="/notifications">Thông báo</Link>
    {!visible ? <p>Đang xác minh quyền truy cập…</p> : visible.failed ? <p role="alert">Chưa thể theo dõi tiếp tài sản. Quyền của bạn có thể đã thay đổi.</p>
      : visible.data && <><h2>{visible.data.assetName}</h2><dl>
        <dt>Tài sản</dt><dd>{visible.data.assetId}</dd><dt>Chi nhánh</dt><dd>{visible.data.branchId}</dd>
        <dt>Tình trạng khi đóng sự cố</dt><dd>{visible.data.conditionAtClosure}</dd>
        <dt>Tình trạng hiện tại</dt><dd>{visible.data.currentCondition}</dd>
        <dt>Đã đóng sự cố</dt><dd>{new Date(visible.data.closedAtUtc).toLocaleString()}</dd></dl>
        <p>Xem tài sản qua quy trình tình trạng tài sản được cấp quyền. Đóng sự cố không tự đổi tình trạng tài sản.</p></>}
  </main>;
}
