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
  return <main><h1>Review asset condition</h1><Link to="/notifications">Notifications</Link>
    {!visible ? <p>Verifying access...</p> : visible.failed ? <p role="alert">Asset follow-up is unavailable. Your access may have changed.</p>
      : visible.data && <><h2>{visible.data.assetName}</h2><dl>
        <dt>Asset</dt><dd>{visible.data.assetId}</dd><dt>Branch</dt><dd>{visible.data.branchId}</dd>
        <dt>Condition when Incident closed</dt><dd>{visible.data.conditionAtClosure}</dd>
        <dt>Current condition</dt><dd>{visible.data.currentCondition}</dd>
        <dt>Incident closed</dt><dd>{new Date(visible.data.closedAtUtc).toLocaleString()}</dd></dl>
        <p>Review this asset through the authorized asset condition workflow. Closing an Incident does not update its condition automatically.</p></>}
  </main>;
}
