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
    return <p role="alert">Invalid inspection or branch ID.</p>;
  }

  // Do not let an authority refresh silently remount an interrupted mutation.
  // The explicit continuation starts with a new authorized read, never a write replay.
  if (approved !== state.authorityGeneration)
    return (
      <section aria-label="Inspection session recovery">
        <p role="alert">
          Session authority changed. An interrupted inspection update may
          already have committed. No update has been retried.
        </p>
        <button onClick={() => setApproved(state.authorityGeneration)}>
          Verify inspection access
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
    return <p role="status">Checking inspection access...</p>;
  if (admission === "denied")
    return (
      <section>
        <p role="alert">Inspection access could not be confirmed.</p>
        <button onClick={() => void actions.revalidate()}>Check session</button>
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
