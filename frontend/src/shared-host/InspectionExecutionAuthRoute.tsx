import { useEffect, useRef, useState } from "react";
import { Navigate, useLocation, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";
import { InspectionRoute } from "../features/inspectionExecution/InspectionRoute";
import {
  createInspectionApi,
  InspectionApiError,
} from "../features/inspectionExecution/api";
import { positiveId } from "../features/inspectionExecution/model";
import { loginDestination } from "./returnUrl";
import { Recovery } from "./AuthForms";

export function InspectionExecutionAuthRoute() {
  const { state } = useAuth();
  const { branch, id } = useParams();
  const location = useLocation();
  const entered = useRef(false);
  const [approved, setApproved] = useState<AuthorityGeneration | null>(null);
  useEffect(() => {
    if (!entered.current && state.status === "AUTHENTICATED" && state.session) {
      entered.current = true;
      setApproved(state.authorityGeneration);
    }
  }, [state.status, state.session, state.authorityGeneration]);

  if (state.status === "INITIALIZING" || state.status === "ERROR_UNAVAILABLE")
    return <Recovery />;
  if (state.status === "RESTRICTED")
    return <Navigate replace to="/change-password" />;
  if (state.status !== "AUTHENTICATED" || !state.session)
    return (
      <Navigate
        replace
        to={loginDestination(location.pathname, location.search)}
      />
    );

  let branchId: number, inspectionId: number;
  try {
    branchId = positiveId(branch ?? "");
    inspectionId = positiveId(id ?? "");
  } catch {
    return <p role="alert">Mã kiểm tra hoặc chi nhánh không hợp lệ.</p>;
  }

  // Do not let an authority refresh silently remount an interrupted mutation.
  // The explicit continuation starts with a new authorized read, never a write replay.
  if (approved !== state.authorityGeneration)
    return (
      <section aria-label="Khôi phục phiên kiểm tra">
        <p role="alert">
          Quyền phiên đã đổi. Cập nhật kiểm tra bị gián đoạn có thể đã lưu. Không gửi lại cập nhật.
        </p>
        <button onClick={() => setApproved(state.authorityGeneration)}>
          Xác minh quyền kiểm tra
        </button>
      </section>
    );
  return (
    <AdmittedInspection
      key={JSON.stringify([branchId, inspectionId])}
      generation={approved}
      branch={branchId}
      id={inspectionId}
      principal={state.session.user.id}
    />
  );
}

function AdmittedInspection({
  generation,
  branch,
  id,
  principal,
}: {
  generation: AuthorityGeneration;
  branch: number;
  id: number;
  principal: string;
}) {
  const { actions } = useAuth();
  const [admission, setAdmission] = useState<"pending" | "granted" | "denied">(
    "pending",
  );
  const lifetime = useRef<AbortController | null>(null);
  const invalid = () => {
    if (lifetime.current && !lifetime.current.signal.aborted)
      actions.invalidateSession(generation);
  };
  useEffect(() => {
    const controller = new AbortController();
    lifetime.current = controller;
    const api = createInspectionApi({
      signal: controller.signal,
      onSessionInvalid: () => {
        if (!controller.signal.aborted) actions.invalidateSession(generation);
      },
    });
    void api
      .read(branch, id)
      .then(() => {
        if (!controller.signal.aborted) setAdmission("granted");
      })
      .catch((error: unknown) => {
        if (!controller.signal.aborted) {
          setAdmission("denied");
          if (error instanceof InspectionApiError && error.status === 401)
            actions.invalidateSession(generation);
        }
      });
    return () => controller.abort();
  }, [actions, generation, branch, id]);
  if (admission === "pending")
    return <p role="status">Đang kiểm tra quyền thực hiện kiểm tra...</p>;
  if (admission === "denied")
    return (
      <section>
        <p role="alert">Không xác minh được quyền thực hiện kiểm tra.</p>
        <button onClick={() => void actions.revalidate()}>Kiểm tra phiên đăng nhập</button>
      </section>
    );
  return (
    <InspectionRoute
      initialId={String(id)}
      onSessionInvalid={invalid}
      context={{
        authorization: "granted",
        sessionKey: "admitted-inspection",
        principalId: principal,
        branchId: branch,
        inspectionPerform: true,
      }}
    />
  );
}
