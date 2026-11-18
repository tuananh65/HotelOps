import { ApiError, getJson, send } from "./client";
import { positiveId } from "./rooms";

export interface ReinspectionOptions {
  reworkId: number;
  branchId: number;
  roomId: number;
  cleaningTaskId: number;
  originalInspectionId: number;
  versionId: number;
  reworkETag: string;
  existingInspectionId: number | null;
  inspectors: { id: number; name: string }[];
  roomNumber: string;
  checklistName: string;
  checklistVersionNumber: number;
  canAssign: boolean;
  unavailableReason: string | null;
}
export interface ReinspectionAssignmentCommand {
  inspectorId: number;
  dueAt: string;
  reason: string;
}
const validId = (value: unknown): value is number =>
  Number.isSafeInteger(value) && (value as number) > 0;
const validTag = (value: unknown): value is string =>
  typeof value === "string" && /^"[0-9A-F]{16}"$/.test(value);
const guid = (value: string) =>
  /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(
    value,
  );
const path = (branch: string, rework: string) =>
  `/branches/${positiveId(branch)}/reworks/${positiveId(rework)}`;
const invalid = (): never => {
  throw new ApiError(502, "INVALID_REINSPECTION_RESPONSE");
};

export async function readReinspectionOptions(
  branch: string,
  rework: string,
  signal: AbortSignal,
): Promise<ReinspectionOptions> {
  const response = await send(
    path(branch, rework) + "/reinspection-options",
    signal,
    { method: "GET", headers: { Accept: "application/json" } },
  );
  const d = response.body;
  if (
    !d ||
    d.branchId !== positiveId(branch) ||
    d.reworkId !== positiveId(rework) ||
    ![
      d.roomId,
      d.cleaningTaskId,
      d.originalInspectionId,
      d.versionId,
      d.checklistVersionNumber,
    ].every(validId) ||
    !validTag(d.reworkETag) ||
    response.etag !== d.reworkETag ||
    !(d.existingInspectionId === null || validId(d.existingInspectionId)) ||
    ![d.roomNumber, d.checklistName].every(
      (v) => typeof v === "string" && v.trim().length > 0,
    ) ||
    typeof d.canAssign !== "boolean" ||
    !Array.isArray(d.inspectors) ||
    d.inspectors.some(
      (p: { id: unknown; name: unknown }) =>
        !p || !validId(p.id) || typeof p.name !== "string",
    ) ||
    new Set(d.inspectors.map((p: { id: number }) => p.id)).size !==
      d.inspectors.length ||
    ![
      null,
      "ALREADY_ASSIGNED",
      "CHECKLIST_UNAVAILABLE",
      "ASSIGNMENT_UNAVAILABLE",
      "NO_ELIGIBLE_INSPECTOR",
    ].includes(d.unavailableReason) ||
    (d.canAssign &&
      (d.existingInspectionId !== null ||
        d.inspectors.length === 0 ||
        d.unavailableReason !== null))
  )
    invalid();
  return d;
}

export async function assignReinspection(
  branch: string,
  rework: string,
  tag: string,
  operation: string,
  command: ReinspectionAssignmentCommand,
  scope: { signal: AbortSignal; isCurrent: () => boolean },
) {
  const endpoint = path(branch, rework) + "/reinspection";
  if (
    !validTag(tag) ||
    !guid(operation) ||
    !validId(command.inspectorId) ||
    typeof command.dueAt !== "string" ||
    !/Z$/.test(command.dueAt) ||
    !Number.isFinite(Date.parse(command.dueAt)) ||
    typeof command.reason !== "string" ||
    !command.reason.trim() ||
    command.reason.trim().length > 500 ||
    /[\u0000-\u001f\u007f-\u009f]/.test(command.reason)
  )
    throw new Error("INVALID_REINSPECTION_COMMAND");
  const current = () => {
    scope.signal.throwIfAborted();
    if (!scope.isCurrent())
      throw new DOMException("Phiên đăng nhập hoặc dữ liệu nguồn đã thay đổi", "AbortError");
  };
  current();
  const csrf = (await getJson("/auth/csrf", scope.signal)) as {
    headerName?: string;
    csrfToken?: string;
  };
  if (
    csrf?.headerName !== "X-CSRF-TOKEN" ||
    !csrf.csrfToken?.trim() ||
    /[\r\n]/.test(csrf.csrfToken)
  )
    throw new ApiError(502, "INVALID_CSRF_RESPONSE");
  current();
  const response = await send(endpoint, scope.signal, {
    method: "POST",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
      "X-CSRF-TOKEN": csrf.csrfToken,
      "If-Match": tag,
      "Idempotency-Key": operation,
    },
    body: JSON.stringify(command),
  });
  current();
  const d = response.body;
  if (
    !d ||
    !validId(d.id) ||
    d.branchId !== positiveId(branch) ||
    d.reworkId !== positiveId(rework) ||
    d.contract !== "FE05_REINSPECTION_ASSIGNMENT_V1" ||
    d.state !== "ASSIGNED" ||
    d.operationId?.toLowerCase() !== operation.toLowerCase() ||
    d.notification !== "PENDING_DELIVERY" ||
    !validTag(d.sourceETag) ||
    response.etag !== d.sourceETag
  )
    invalid();
  return { id: d.id as number, sourceETag: d.sourceETag as string };
}
