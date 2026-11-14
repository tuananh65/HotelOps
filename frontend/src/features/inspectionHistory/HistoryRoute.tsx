import { useCallback, useEffect, useState } from "react";
import { positiveId } from "./model";
import { HistoryWorkspace } from "./HistoryWorkspace";

/** Isolated DEV3 integration input, not an AuthProvider or permission authority.
 * Real host must resolve branch-scoped INSPECTION_VIEW and rotate sessionKey on
 * session replacement/revalidation. No production route is registered here.
 */
export interface HistoryHostContext {
  authorization: "unresolved" | "denied" | "granted";
  sessionKey: string;
  principalId: string;
  branchId: string | number;
  inspectionView: boolean;
}
export function historyScope(context?: HistoryHostContext) {
  if (
    context?.authorization !== "granted" ||
    context.inspectionView !== true ||
    typeof context.sessionKey !== "string" ||
    !context.sessionKey.trim() ||
    typeof context.principalId !== "string" ||
    !context.principalId.trim()
  )
    return null;
  try {
    const branch = positiveId(context.branchId);
    return {
      branch,
      key: JSON.stringify([context.sessionKey, context.principalId, branch]),
    };
  } catch {
    return null;
  }
}
export function HistoryRoute({
  context,
  initialId,
  onAccessDenied,
}: {
  context?: HistoryHostContext;
  initialId?: string;
  onAccessDenied?: (status: 401 | 403) => void;
}) {
  const scope = historyScope(context);
  let id: number | undefined;
  let invalidId = false;
  try {
    if (initialId !== undefined) id = positiveId(initialId);
  } catch {
    invalidId = true;
  }
  if (!scope)
    return (
      <p role="alert">Chưa được cấp quyền xem lịch sử trong chi nhánh này.</p>
    );
  return (
    <AuthorizedHistory
      key={scope.key}
      branch={scope.branch}
      initialId={id}
      invalidId={invalidId}
      onAccessDenied={onAccessDenied}
    />
  );
}
function AuthorizedHistory({
  branch,
  initialId,
  invalidId,
  onAccessDenied,
}: {
  branch: number;
  initialId?: number;
  invalidId: boolean;
  onAccessDenied?: (status: 401 | 403) => void;
}) {
  const [denied, setDenied] = useState<401 | 403 | null>(null);
  const deny = useCallback((status: 401 | 403) => setDenied(status), []);
  useEffect(() => {
    if (denied) onAccessDenied?.(denied);
  }, [denied, onAccessDenied]);
  if (denied)
    return (
      <p role="alert">
        {denied === 401
          ? "Phiên không hợp lệ. Cần hệ thống xác thực lại."
          : "Không có quyền xem lịch sử trong phạm vi này."}
      </p>
    );
  if (invalidId) return <p role="alert">ID lịch sử không hợp lệ.</p>;
  return (
    <HistoryWorkspace
      key={initialId ?? "list"}
      branch={branch}
      initialId={initialId}
      onDenied={deny}
    />
  );
}
