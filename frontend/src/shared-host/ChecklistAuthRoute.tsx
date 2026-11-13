import { useEffect, useRef, useState } from "react";
import { Navigate, useLocation } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { ChecklistRoute } from "../features/inspectionChecklists/ChecklistRoute";
import {
  ChecklistApiError,
  createChecklistApi,
} from "../features/inspectionChecklists/api";
import { defaultCollectionQuery } from "../features/inspectionChecklists/collection";
import type { AuthorityGeneration } from "../auth/authStore";
import { Recovery } from "./AuthForms";

/** Local route selector only. The collection API independently checks CHECKLIST_MANAGE. */
export function checklistBranch(search: string): number | null {
  const params = new URLSearchParams(search);
  if ([...params.keys()].some((key) => key !== "branchId")) return null;
  const ids = params.getAll("branchId");
  if (ids.length !== 1 || !/^[1-9][0-9]*$/.test(ids[0])) return null;
  const id = Number(ids[0]);
  return Number.isSafeInteger(id) ? id : null;
}

/** Isolated composition: existing main.tsx keeps the gate OFF; no URL/environment override. */
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
    return <p role="alert">Checklist destination is not activated.</p>;
  if (state.status === "INITIALIZING" || state.status === "ERROR_UNAVAILABLE")
    return <Recovery />;
  if (state.status === "RESTRICTED")
    return <Navigate replace to="/change-password" />;
  if (state.status !== "AUTHENTICATED" || !state.session)
    return <Navigate replace to="/login" />;
  const branch = checklistBranch(search);
  if (branch === null)
    return <p role="alert">Invalid checklist branch context.</p>;
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
    api
      .list(branch, defaultCollectionQuery(), controller.signal)
      .then(() => {
        if (!controller.signal.aborted) setAdmission("granted");
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) return;
        setAdmission("denied");
        if (error instanceof ChecklistApiError && error.status === 401)
          actions.invalidateSession(generation);
      });
    return () => controller.abort();
  }, [branch, actions, generation]);
  if (admission === "pending")
    return <p role="status">Checking checklist access...</p>;
  if (admission === "denied")
    return (
      <section>
        <p role="alert">Checklist access could not be confirmed.</p>
        <button onClick={() => void actions.revalidate()}>Check session</button>
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
