import { displayCode } from "../i18n/vi";
import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { ApiError, getJson } from "../api/dev1/client";

type Receipt = { handoverId: number; number: string; assetId: number; assetCode: string;
  state: string; conditionNote: string | null; submittedAtUtc: string; crossBranch?: boolean };
export function AssetHandoverNotice() {
  const { id = "" } = useParams();
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && state.session && !state.session.mustChangePassword && state.pending === null
    ? state.authorityGeneration : null;
  const [result, setResult] = useState<{ owner: typeof owner; id: string; data?: Receipt; failed?: boolean } | null>(null);
  useEffect(() => {
    if (!owner) return;
    const abort = new AbortController(); setResult(null);
    if (!/^[1-9][0-9]*$/.test(id) || !Number.isSafeInteger(Number(id))) { setResult({ owner, id, failed: true }); return; }
    getJson(`/notifications/${id}/asset-handover`, abort.signal).then(value => {
      const r = value as Receipt;
      if (!r || ![r.handoverId, r.assetId].every(n => Number.isSafeInteger(n) && n > 0)
        || typeof r.number !== "string" || typeof r.assetCode !== "string"
        || !["PENDING", "ACCEPTED", "REJECTED", "CANCELLED"].includes(r.state)
        || (r.conditionNote !== null && typeof r.conditionNote !== "string")
        || typeof r.submittedAtUtc !== "string" || !r.submittedAtUtc.endsWith("Z")
        || !Number.isFinite(Date.parse(r.submittedAtUtc))) throw Error("INVALID_HANDOVER");
      if (!abort.signal.aborted) setResult({ owner, id, data: r });
    }).catch(error => {
      if (abort.signal.aborted) return;
      setResult({ owner, id, failed: true });
      if (error instanceof ApiError && error.status === 401) actions.invalidateSession(owner);
    });
    return () => abort.abort();
  }, [id, owner, actions]);
  const visible = owner && result?.owner === owner && result.id === id ? result : null;
  return <main><h1>Bàn giao tài sản</h1><Link to="/notifications">Thông báo</Link>
    {!visible ? <p>Đang xác minh quyền truy cập…</p> : visible.failed ? <p role="alert">Chưa xem được bàn giao. Cần quyền xem tài sản hiện tại.</p>
      : visible.data && <><h2>{visible.data.number}</h2><dl>
        <dt>Tài sản</dt><dd>{visible.data.assetCode}</dd><dt>Trạng thái</dt><dd>{displayCode(visible.data.state)}</dd>
        <dt>Tình trạng khi bàn giao</dt><dd>{visible.data.conditionNote ?? "Chưa có ghi chú tình trạng"}</dd>
        <dt>Đã nộp</dt><dd>{new Date(visible.data.submittedAtUtc).toLocaleString()}</dd></dl>
        <Link to={`/assets/${visible.data.assetId}/${visible.data.crossBranch ? "transfer/cross-branch" : "custody"}`}>Mở quy trình quản lý tài sản</Link>
        <p>Xem bàn giao không xác nhận đã nhận hoặc đổi người quản lý tài sản.</p></>}
  </main>;
}
