import { ApiError, getJson, send } from "./client";
import { positiveId } from "./rooms";

export interface InitialInspectionOptions {
  taskId: number;
  branchId: number;
  roomId: number;
  cycleETag: string;
  checklistVersionId: number | null;
  existingInspectorId: number | null;
  taskETag: string;
  existingInspectionId: number | null;
  inspectors: { id: number; name: string }[];
  roomNumber: string;
  checklistName: string | null;
  versionNo: number | null;
  canAssign: boolean;
  unavailableReason: string | null;
}
export interface InitialInspectionAssignmentCommand {
  inspectorId: number;
  checklistVersionId: number;
  cycleETag: string;
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
const path = (branch: string, task: string) =>
  `/branches/${positiveId(branch)}/cleaning-tasks/${positiveId(task)}`;
const invalid = (): never => {
  throw new ApiError(502, "INVALID_INITIAL_INSPECTION_RESPONSE");
};

export async function readInitialInspectionOptions(
  branch: string,
  task: string,
  signal: AbortSignal,
): Promise<InitialInspectionOptions> {
  const response = await send(
    path(branch, task) + "/initial-inspection-options",
    signal,
    { method: "GET", headers: { Accept: "application/json" } },
  );
  const d = response.body;
  if (
    !d ||
    d.branchId !== positiveId(branch) ||
    d.taskId !== positiveId(task) ||
    ![d.roomId].every(validId) ||
    !validTag(d.taskETag) ||
    !validTag(d.cycleETag) ||
    !(d.checklistVersionId === null || validId(d.checklistVersionId)) ||
    !(d.versionNo === null || validId(d.versionNo)) ||
    !(d.existingInspectorId === null || validId(d.existingInspectorId)) ||
    !(
      d.checklistName === null ||
      (typeof d.checklistName === "string" && d.checklistName.trim())
    ) ||
    response.etag !== d.taskETag ||
    !(d.existingInspectionId === null || validId(d.existingInspectionId)) ||
    ![d.roomNumber].every(
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
      (!validId(d.checklistVersionId) ||
        !validId(d.versionNo) ||
        !d.checklistName ||
        d.existingInspectionId !== null ||
        d.inspectors.length === 0 ||
        d.unavailableReason !== null))
  )
    invalid();
  return d;
}

export async function assignInitialInspection(
  branch: string,
  task: string,
  tag: string,
  operation: string,
  command: InitialInspectionAssignmentCommand,
  scope: { signal: AbortSignal; isCurrent: () => boolean },
) {
  const endpoint = path(branch, task) + "/inspections";
  if (
    !validTag(tag) ||
    !guid(operation) ||
    !validId(command.inspectorId) ||
    !validId(command.checklistVersionId) ||
    !validTag(command.cycleETag) ||
    typeof command.dueAt !== "string" ||
    !/Z$/.test(command.dueAt) ||
    !Number.isFinite(Date.parse(command.dueAt)) ||
    typeof command.reason !== "string" ||
    !command.reason.trim() ||
    command.reason.trim().length > 500 ||
    /[\u0000-\u001f\u007f-\u009f]/.test(command.reason)
  )
    throw new Error("INVALID_INITIAL_INSPECTION_COMMAND");
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
    d.taskId !== positiveId(task) ||
    d.contract !== "FE05_INITIAL_INSPECTION_ASSIGNMENT_V1" ||
    d.versionId !== command.checklistVersionId ||
    d.inspectorId !== command.inspectorId ||
    !validId(d.roomId) ||
    d.state !== "ASSIGNED" ||
    d.operationId?.toLowerCase() !== operation.toLowerCase() ||
    d.notification !== "PENDING_DELIVERY" ||
    !validTag(d.sourceETag) ||
    response.etag !== d.sourceETag
  )
    invalid();
  return { id: d.id as number, sourceETag: d.sourceETag as string };
}

export interface InitialInspectionQueue {
  items: {
    taskId: number;
    roomId: number;
    roomNumber: string;
    createdAt: string;
  }[];
  totalCount: number;
  page: number;
  pageSize: number;
}
export async function readInitialInspectionQueue(
  branch: string,
  room: string | null,
  page: number,
  signal: AbortSignal,
): Promise<InitialInspectionQueue> {
  if (!Number.isSafeInteger(page) || page < 1) throw new Error("Invalid page");
  const data = (
    await send(
      `/branches/${positiveId(branch)}/inspections/pending-assignment?page=${page}&pageSize=25${room === null ? "" : "&roomId=" + positiveId(room)}`,
      signal,
      { method: "GET", headers: { Accept: "application/json" } },
    )
  ).body;
  if (
    !data ||
    data.page !== page ||
    data.pageSize !== 25 ||
    !Number.isSafeInteger(data.totalCount) ||
    data.totalCount < 0 ||
    !Array.isArray(data.items) ||
    data.items.length > 25 ||
    data.items.length > data.totalCount ||
    data.items.some(
      (r: {
        taskId: unknown;
        roomId: unknown;
        roomNumber: unknown;
        createdAt: unknown;
      }) =>
        !r ||
        !validId(r.taskId) ||
        !validId(r.roomId) ||
        (room !== null && r.roomId !== positiveId(room)) ||
        typeof r.roomNumber !== "string" ||
        typeof r.createdAt !== "string" ||
        !Number.isFinite(Date.parse(r.createdAt)),
    ) ||
    new Set(data.items.map((r: { taskId: number }) => r.taskId)).size !==
      data.items.length
  )
    invalid();
  return data;
}
