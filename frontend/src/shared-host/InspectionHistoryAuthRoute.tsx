import { useEffect, useRef, useState } from "react";
import { Link, useLocation, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { HistoryRoute } from "../features/inspectionHistory/HistoryRoute";
import {
  createHistoryApi,
  HistoryError,
} from "../features/inspectionHistory/api";
import { positiveId } from "../features/inspectionHistory/model";

/** Mounted inside Protected; permission admission comes from the scoped API. */
export function InspectionHistoryAuthRoute() {
  const { state } = useAuth();
  const { id } = useParams();
  const location = useLocation();
  const query = new URLSearchParams(location.search);
  let branch: number, inspection: number;
  try {
    if (
      query.getAll("branchId").length !== 1 ||
      [...query.keys()].some((k) => k !== "branchId")
    )
      throw Error();
    branch = positiveId(query.get("branchId") ?? "");
    inspection = positiveId(id ?? "");
  } catch {
    return <p role="alert">Invalid inspection history destination.</p>;
  }
  if (
    state.status !== "AUTHENTICATED" ||
    state.pending !== null ||
    !state.session ||
    state.session.mustChangePassword
  )
    return <p role="status">Checking session...</p>;
  return (
    <AdmittedHistory
      key={`${branch}:${inspection}`}
      branch={branch}
      inspection={inspection}
    />
  );
}

function AdmittedHistory({
  branch,
  inspection,
}: {
  branch: number;
  inspection: number;
}) {
  const { state, actions } = useAuth();
  const generation = state.authorityGeneration;
  const lifetime = useRef<AbortController | null>(null);
  const [admission, setAdmission] = useState<"pending" | "granted" | "denied">(
    "pending",
  );
  useEffect(() => {
    const abort = new AbortController();
    lifetime.current = abort;
    setAdmission("pending");
    void createHistoryApi(branch, abort.signal)
      .detail(inspection)
      .then(() => {
        if (!abort.signal.aborted) setAdmission("granted");
      })
      .catch((error) => {
        if (abort.signal.aborted) return;
        setAdmission("denied");
        if (error instanceof HistoryError && error.status === 401)
          actions.invalidateSession(generation);
      });
    return () => abort.abort();
  }, [branch, inspection, generation, actions]);
  return (
    <main>
      <h1>Inspection history</h1>
      <Link to="/notifications">Notifications</Link>
      {admission === "pending" && (
        <p role="status">Checking inspection history access...</p>
      )}
      {admission === "denied" && (
        <p role="alert">
          Inspection history is unavailable or access has changed.
        </p>
      )}
      {admission === "granted" && state.session && (
        <HistoryRoute
          initialId={String(inspection)}
          context={{
            authorization: "granted",
            sessionKey: "admitted-history",
            principalId: state.session.user.id,
            branchId: branch,
            inspectionView: true,
          }}
          onAccessDenied={(status) => {
            if (lifetime.current?.signal.aborted) return;
            setAdmission("denied");
            if (status === 401) actions.invalidateSession(generation);
          }}
        />
      )}
    </main>
  );
}
