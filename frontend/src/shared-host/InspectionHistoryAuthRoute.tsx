import { useEffect, useRef, useState } from "react";
import { useLocation, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { HistoryBranchSelector } from "../features/inspectionHistory/HistoryBranchSelector";
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
  if (!id && !location.search)
    return (
      <main className="ops-history">
        <header>
          <h1>Lịch sử kiểm tra phòng</h1>
          <p>
            Xem kết quả kiểm tra, lỗi cần khắc phục và bằng chứng theo từng
            phòng.
          </p>
        </header>
        <HistoryBranchSelector autoSelect />
      </main>
    );
  let branch: number, inspection: number | undefined;
  try {
    if (
      query.getAll("branchId").length !== 1 ||
      [...query.keys()].some((k) => k !== "branchId")
    )
      throw Error();
    branch = positiveId(query.get("branchId") ?? "");
    inspection = id === undefined ? undefined : positiveId(id);
  } catch {
    return <p role="alert">Đích lịch sử kiểm tra không hợp lệ.</p>;
  }
  if (
    state.status !== "AUTHENTICATED" ||
    state.pending !== null ||
    !state.session ||
    state.session.mustChangePassword
  )
    return <p role="status">Đang kiểm tra phiên đăng nhập...</p>;
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
  inspection?: number;
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
    const api = createHistoryApi(branch, abort.signal);
    void (
      inspection === undefined
        ? api.list({ page: 1, pageSize: 20 })
        : api.detail(inspection)
    )
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
    <main className="ops-history">
      <header>
        <h1>Lịch sử kiểm tra phòng</h1>
        <p>
          Xem kết quả kiểm tra, lỗi cần khắc phục và bằng chứng theo từng phòng.
        </p>
      </header>
      {admission === "granted" && (
        <HistoryBranchSelector selected={String(branch)} />
      )}
      {admission === "pending" && (
        <p role="status">Đang kiểm tra quyền xem lịch sử kiểm tra...</p>
      )}
      {admission === "denied" && (
        <p role="alert">Lịch sử kiểm tra chưa khả dụng hoặc quyền đã đổi.</p>
      )}
      {admission === "granted" && state.session && (
        <HistoryRoute
          initialId={inspection === undefined ? undefined : String(inspection)}
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
