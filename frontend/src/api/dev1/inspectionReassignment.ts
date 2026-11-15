import { ApiError, getJson, send } from "./client";
import { positiveId } from "./rooms";

export interface ReassignmentOptions {
  id: number;
  branchId: number;
  taskId: number;
  roomId: number;
  versionId: number;
  inspectorId: number;
  state: string;
  dueAt: string | null;
  sourceETag: string;
  cycleETag: string;
  inspectors: { id: number; name: string }[];
  canReassign: boolean;
  reworkId?: number;
  reworkETag?: string;
  bindingETag?: string;
}
const validId = (v: unknown) => Number.isSafeInteger(v) && (v as number) > 0;
const validTag = (v: unknown) =>
  typeof v === "string" && /^"[0-9A-F]{16}"$/.test(v);
const states = ["ASSIGNED", "INSPECTING", "PENDING_CONCLUSION"];
const path = (branch: string, id: number, reinspection: boolean) =>
  `/branches/${positiveId(branch)}/${reinspection ? "reinspections" : "inspections"}/${positiveId(String(id))}`;
const invalid = (): never => {
  throw new ApiError(502, "INVALID_REASSIGNMENT_RESPONSE");
};

export function inspectionAssignmentRejection(error: unknown): string | null {
  if (!(error instanceof ApiError)) return null;
  if (error.status === 400 && error.code === "INSPECTOR_CLEANER_CONFLICT")
    return "MSG22: This inspector performed cleaning or rework in this cycle. Refresh and select another eligible inspector.";
  if (error.status === 409 && error.code === "INSPECTION_ALREADY_ASSIGNED")
    return "MSG03: An open inspection already exists. Refresh to view it or change its inspector.";
  if (
    [400, 503].includes(error.status) &&
    [
      "CHECKLIST_UNAVAILABLE",
      "ORIGINAL_CHECKLIST_VERSION_UNAVAILABLE",
    ].includes(error.code)
  )
    return "MSG03: The required published checklist is unavailable. Publish the eligible checklist before assignment; refresh to review its current version.";
  return null;
}

export async function readReassignment(
  branch: string,
  id: number,
  signal: AbortSignal,
  reinspection = false,
): Promise<ReassignmentOptions> {
  const response = await send(
    path(branch, id, reinspection) + "/reassignment-options",
    signal,
    { method: "GET", headers: { Accept: "application/json" } },
  );
  const d = response.body;
  if (
    !d ||
    d.id !== id ||
    d.branchId !== positiveId(branch) ||
    ![d.taskId, d.roomId, d.versionId, d.inspectorId].every(validId) ||
    !states.includes(d.state) ||
    (reinspection &&
      (d.state !== "ASSIGNED" ||
        !validId(d.reworkId) ||
        !validTag(d.reworkETag) ||
        !validTag(d.bindingETag))) ||
    !validTag(d.sourceETag) ||
    response.etag !== d.sourceETag ||
    !validTag(d.cycleETag) ||
    !(
      d.dueAt === null ||
      (typeof d.dueAt === "string" && Number.isFinite(Date.parse(d.dueAt)))
    ) ||
    typeof d.canReassign !== "boolean" ||
    !Array.isArray(d.inspectors) ||
    d.inspectors.some(
      (p: { id: unknown; name: unknown }) =>
        !p ||
        !validId(p.id) ||
        p.id === d.inspectorId ||
        typeof p.name !== "string" ||
        !p.name.trim(),
    ) ||
    new Set(d.inspectors.map((p: { id: number }) => p.id)).size !==
      d.inspectors.length ||
    (d.canReassign && !d.inspectors.length)
  )
    invalid();
  return d;
}

export async function reassignInspection(
  branch: string,
  source: ReassignmentOptions,
  inspectorId: number,
  reason: string,
  operation: string,
  scope: { signal: AbortSignal; isCurrent: () => boolean },
  reinspection = false,
) {
  const endpoint = path(branch, source.id, reinspection) + "/reassign";
  if (
    source.branchId !== positiveId(branch) ||
    !source.canReassign ||
    !validTag(source.sourceETag) ||
    !validTag(source.cycleETag) ||
    (reinspection &&
      (source.state !== "ASSIGNED" ||
        !validId(source.reworkId) ||
        !validTag(source.reworkETag) ||
        !validTag(source.bindingETag))) ||
    !source.inspectors.some((p) => p.id === inspectorId) ||
    inspectorId === source.inspectorId ||
    !reason.trim() ||
    reason.trim().length > 500 ||
    /[\u0000-\u001f\u007f-\u009f]/.test(reason) ||
    !/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(
      operation,
    )
  )
    throw new Error("INVALID_REASSIGNMENT_COMMAND");
  const current = () => {
    scope.signal.throwIfAborted();
    if (!scope.isCurrent())
      throw new DOMException("Session or source changed", "AbortError");
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
      "If-Match": source.sourceETag,
      "Idempotency-Key": operation,
    },
    body: JSON.stringify({
      inspectorId,
      cycleETag: source.cycleETag,
      ...(reinspection
        ? { reworkETag: source.reworkETag, bindingETag: source.bindingETag }
        : {}),
      reason: reason.trim(),
    }),
  });
  current();
  const d = response.body;
  if (
    !d ||
    d.id !== source.id ||
    d.branchId !== source.branchId ||
    d.taskId !== source.taskId ||
    d.roomId !== source.roomId ||
    d.versionId !== source.versionId ||
    d.previousInspectorId !== source.inspectorId ||
    d.inspectorId !== inspectorId ||
    d.state !== source.state ||
    d.contract !==
      (reinspection
        ? "FE05_REINSPECTION_REASSIGNMENT_V1"
        : "FE05_INSPECTION_REASSIGNMENT_V1") ||
    (reinspection &&
      (d.reworkId !== source.reworkId ||
        d.reworkETag !== source.reworkETag ||
        d.bindingETag !== source.bindingETag)) ||
    d.notification !== "PENDING_DELIVERY" ||
    d.operationId?.toLowerCase() !== operation.toLowerCase() ||
    !validTag(d.sourceETag) ||
    response.etag !== d.sourceETag
  )
    invalid();
  return d.sourceETag as string;
}
