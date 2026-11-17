import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { ApiError, getJson } from "../api/dev1/client";

type Receipt = { handoverId: number; number: string; assetId: number; assetCode: string;
  state: string; conditionNote: string | null; submittedAtUtc: string };
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
        || !["PENDING", "ACCEPTED", "REJECTED"].includes(r.state)
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
  return <main><h1>Asset handover</h1><Link to="/notifications">Notifications</Link>
    {!visible ? <p>Verifying access...</p> : visible.failed ? <p role="alert">Handover unavailable. Current asset read permission is required.</p>
      : visible.data && <><h2>{visible.data.number}</h2><dl>
        <dt>Asset</dt><dd>{visible.data.assetCode}</dd><dt>Status</dt><dd>{visible.data.state}</dd>
        <dt>Condition at handover</dt><dd>{visible.data.conditionNote ?? "No condition note"}</dd>
        <dt>Submitted</dt><dd>{new Date(visible.data.submittedAtUtc).toLocaleString()}</dd></dl>
        <Link to={`/assets/${visible.data.assetId}/custody`}>Open custody workflow</Link>
        <p>Reading this handover does not acknowledge it or change asset custody.</p></>}
  </main>;
}
