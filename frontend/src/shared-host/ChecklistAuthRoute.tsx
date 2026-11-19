import { userMessage } from "../i18n/userMessage";
import { useEffect, useRef, useState } from "react";
import { Navigate, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { ChecklistRoute } from "../features/inspectionChecklists/ChecklistRoute";
import {
  ChecklistApiError,
  createChecklistApi,
} from "../features/inspectionChecklists/api";
import { defaultCollectionQuery } from "../features/inspectionChecklists/collection";
import type { AuthorityGeneration } from "../auth/authStore";
import { Recovery } from "./AuthForms";

/** Captures operation authority; FE05 receives no session token or permission grant. */
export function bindChecklistSessionInvalidation(
  invalidateSession: (expected: AuthorityGeneration) => void,
  expectedGeneration: AuthorityGeneration,
  lifetime: AbortSignal,
): () => void {
  return () => {
    if (!lifetime.aborted) invalidateSession(expectedGeneration);
  };
}
/** Local route selector only. The collection API independently checks CHECKLIST_MANAGE. */
export function checklistBranch(search: string): number | null {
  const params = new URLSearchParams(search);
  if ([...params.keys()].some((key) => key !== "branchId")) return null;
  const ids = params.getAll("branchId");
  if (ids.length !== 1 || !/^[1-9][0-9]*$/.test(ids[0])) return null;
  const id = Number(ids[0]);
  return Number.isSafeInteger(id) ? id : null;
}

/** Canonical host opts in; branch API admission remains mandatory. */
export function ChecklistAuthRoute({ enabled = false }: { enabled?: boolean }) {
  const { state } = useAuth();
  const { search } = useLocation();
  const owner = useRef(state.authorityGeneration);
  const revision = useRef(0);
  if (owner.current !== state.authorityGeneration) {
    owner.current = state.authorityGeneration;
    ++revision.current;
  }
  if (!enabled)
    return <p role="alert">Chức năng danh sách kiểm tra chưa được kích hoạt.</p>;
  if (state.status === "INITIALIZING" || state.status === "ERROR_UNAVAILABLE")
    return <Recovery />;
  if (state.status === "RESTRICTED")
    return <Navigate replace to="/change-password" />;
  if (state.status !== "AUTHENTICATED" || !state.session)
    return <Navigate replace to="/login" />;
  if (!search) return <ChecklistBranchSelection />;
  const branch = checklistBranch(search);
  if (branch === null)
    return <p role="alert">Ngữ cảnh chi nhánh của danh sách kiểm tra không hợp lệ.</p>;
  const key = JSON.stringify([revision.current, state.session.user.id, branch]);
  return (
    <AdmittedChecklist
      key={key}
      generation={state.authorityGeneration}
      branch={branch}
      sessionKey={key}
      principalId={state.session.user.id}
    />
  );
}

function ChecklistBranchSelection() {
  const navigate = useNavigate();
  const [branch, setBranch] = useState("");
  const [error, setError] = useState("");
  return <form aria-label="Chọn chi nhánh của danh sách kiểm tra" onSubmit={event => {
    event.preventDefault();
    const id = checklistBranch(`?branchId=${encodeURIComponent(branch)}`);
    if (id === null) { setError("Nhập mã chi nhánh hợp lệ."); return; }
    navigate(`/inspections/checklists?branchId=${id}`);
  }}>
    <h1>Danh sách kiểm tra phòng</h1>
    <label>Mã chi nhánh<input inputMode="numeric" value={branch} onChange={event => setBranch(event.target.value)} /></label>
    <button>Tải danh sách kiểm tra</button>
    {error && <p role="alert">{userMessage(error)}</p>}
  </form>;
}

function AdmittedChecklist({
  generation,
  branch,
  sessionKey,
  principalId,
}: {
  generation: AuthorityGeneration;
  branch: number;
  sessionKey: string;
  principalId: string;
}) {
  const { actions } = useAuth();
  const [admission, setAdmission] = useState<"pending" | "granted" | "denied">(
    "pending",
  );
  useEffect(() => {
    const controller = new AbortController();
    // This is the existing branch-scoped collection read, not a client permission inference.
    // Validate its DTO and scope before mounting forms; every later API call still authorizes.
    const api = createChecklistApi();
    const onSessionInvalid = bindChecklistSessionInvalidation(
      actions.invalidateSession,
      generation,
      controller.signal,
    );
    api
      .list(branch, defaultCollectionQuery(), controller.signal)
      .then(() => {
        if (!controller.signal.aborted) setAdmission("granted");
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) return;
        setAdmission("denied");
        if (error instanceof ChecklistApiError && error.status === 401)
          onSessionInvalid();
      });
    return () => controller.abort();
  }, [branch, actions, generation]);
  if (admission === "pending")
    return <p role="status">Đang kiểm tra quyền truy cập danh sách kiểm tra...</p>;
  if (admission === "denied")
    return (
      <section>
        <p role="alert">Không xác minh được quyền truy cập danh sách kiểm tra.</p>
        <button onClick={() => void actions.revalidate()}>Kiểm tra phiên đăng nhập</button>
      </section>
    );
  return (
    <ChecklistRoute
      context={{
        authorization: "granted",
        sessionKey,
        principalId,
        branchId: branch,
        checklistManage: true,
      }}
    />
  );
}
