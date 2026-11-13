import { useEffect, useState } from "react";
import { Navigate, useLocation } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { ChecklistRoute } from "../features/inspectionChecklists/ChecklistRoute";
import { createChecklistApi, ChecklistApiError } from "../features/inspectionChecklists/api";
import { defaultCollectionQuery } from "../features/inspectionChecklists/collection";
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
  const key = JSON.stringify([state.authorityGeneration, state.session.user.id, branch]);
  return (
    <AdmittedChecklist
      key={key}
      branch={branch}
      sessionKey={key}
      principalId={state.session.user.id}
      authorityGeneration={state.authorityGeneration}
    />
  );
}

function AdmittedChecklist({
  branch,
  sessionKey,
  principalId,
  authorityGeneration,
}: {
  branch: number;
  sessionKey: string;
  principalId: string;
  authorityGeneration: import("../auth/authStore").AuthorityGeneration;
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
        if (error instanceof ChecklistApiError && error.status === 401)
          actions.invalidateSession(authorityGeneration);
        // Resource 403 denies only this checklist admission.
        setAdmission("denied");
      });
    return () => controller.abort();
  }, [branch, actions, authorityGeneration]);
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
