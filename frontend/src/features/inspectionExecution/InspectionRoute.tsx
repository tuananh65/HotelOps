import { useCallback, useEffect, useState } from "react";
import { InspectionWorkspace } from "./InspectionWorkspace";
import {
  positiveId,
  scopeOf,
  type InspectionHostContext,
  type SeverityOption,
} from "./model";
/** Isolated source candidate. The DEV3 Host owns permission and AuthorityGeneration. */
export function InspectionRoute({
  context,
  initialId,
  onSessionInvalid,
  onAccessDenied,
  severities,
}: {
  context?: InspectionHostContext;
  initialId?: string;
  onSessionInvalid?: () => void;
  onAccessDenied?: (status: 401 | 403) => void;
  /** Only an owner-accepted, backend-derived authoritative catalog may populate this. */
  severities?: SeverityOption[];
}) {
  const scope = scopeOf(context);
  if (!scope) return <p role="alert">Inspection execution is not admitted.</p>;
  let id: number | undefined;
  try {
    id = positiveId(initialId ?? "");
  } catch {
    id = undefined;
  }
  return (
    <AuthorizedExecution
      key={scope.key}
      branch={scope.branch}
      id={id}
      onAccessDenied={onAccessDenied}
      onSessionInvalid={onSessionInvalid}
      severities={severities}
    />
  );
}
function AuthorizedExecution({
  branch,
  id,
  onSessionInvalid,
  onAccessDenied,
  severities,
}: {
  branch: number;
  id?: number;
  onSessionInvalid?: () => void;
  onAccessDenied?: (s: 401 | 403) => void;
  severities?: SeverityOption[];
}) {
  const [denied, setDenied] = useState<{
    status: 401 | 403;
    id: number;
  } | null>(null);
  const notify = useCallback(
    (status: 401 | 403) => {
      if (id !== undefined) setDenied({ status, id });
    },
    [id],
  );
  useEffect(() => {
    if (denied !== null) onAccessDenied?.(denied.status);
  }, [denied, onAccessDenied]);
  if (denied !== null && (denied.status === 401 || denied.id === id))
    return (
      <p role="alert">
        {denied.status === 401
          ? "Session expired; verify access again."
          : "Inspection permission denied in this branch."}
      </p>
    );
  if (id === undefined)
    return (
      <p role="alert">
        A valid Inspection ID is required; Directory is not integrated.
      </p>
    );
  return (
    <InspectionWorkspace
      key={id}
      branch={branch}
      id={id}
      severities={severities}
      onDenied={notify}
      onSessionInvalid={onSessionInvalid}
    />
  );
}
