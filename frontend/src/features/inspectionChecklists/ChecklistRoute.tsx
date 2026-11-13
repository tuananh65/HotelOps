import { useState } from "react";
import { ChecklistWorkspace } from "./ChecklistWorkspace";
import { positiveId } from "./model";

/** DEV3 integration input, not an authentication authority. No default grant.
 * Host must derive this snapshot from its real session and branch-scoped
 * CHECKLIST_MANAGE decision. Change sessionKey on logout/login, principal change
 * or authorization refresh (even when the same user signs in again).
 * This candidate is deliberately not registered in main.tsx.
 */
export interface ChecklistHostContext {
  authorization: "unresolved" | "denied" | "granted";
  sessionKey: string;
  principalId: string;
  branchId: string | number;
  checklistManage: boolean;
}

export function checklistRouteScope(context?: ChecklistHostContext) {
  if (
    context?.authorization !== "granted" ||
    context.checklistManage !== true ||
    typeof context.sessionKey !== "string" ||
    typeof context.principalId !== "string" ||
    !context.sessionKey?.trim() ||
    !context.principalId?.trim()
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

export function ChecklistRoute({
  context,
  initialId,
}: {
  context?: ChecklistHostContext;
  initialId?: string;
}) {
  const scope = checklistRouteScope(context);
  if (!scope)
    return (
      <p role="alert">
        Chưa được cấp quyền quản lý checklist trong chi nhánh này.
      </p>
    );
  return (
    <AuthorizedChecklist
      key={scope.key}
      branch={scope.branch}
      initialId={initialId}
    />
  );
}

function AuthorizedChecklist({
  branch,
  initialId,
}: {
  branch: number;
  initialId?: string;
}) {
  const [denied, setDenied] = useState(false);
  if (denied)
    return (
      <p role="alert">
        Phiên hoặc quyền truy cập đã thay đổi. Yêu cầu hệ thống xác thực lại.
      </p>
    );
  return (
    <ChecklistWorkspace
      branchId={branch}
      initialId={initialId}
      onAccessDenied={() => setDenied(true)}
    />
  );
}
