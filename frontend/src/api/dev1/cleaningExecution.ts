import { ApiError, getJson, send } from "./client";
import { positiveId } from "./rooms";
export interface CleaningExecution {
  id: number;
  branchId: number;
  taskNumber: string;
  state: string | null;
  assignmentId: number | null;
  assigneeId: number | null;
  actualStartAtUtc: string | null;
  sourceETag: string;
  startCapability: string;
  reassignCapability: string;
}
const tag = (v: unknown): v is string =>
  typeof v === "string" && /^"[0-9A-F]{16}"$/.test(v);
const guid = (v: unknown): v is string =>
  typeof v === "string" &&
  /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(
    v,
  );
export async function readCleaningExecution(
  branch: string,
  task: string,
  signal?: AbortSignal,
): Promise<CleaningExecution> {
  const b = positiveId(branch),
    id = positiveId(task);
  const r = await send(
    `/branches/${b}/cleaning-tasks/${id}/execution`,
    signal,
    { method: "GET", headers: { Accept: "application/json" } },
  );
  const d = r.body;
  if (
    !d ||
    d.id !== id ||
    d.branchId !== b ||
    typeof d.taskNumber !== "string" ||
    !(d.state === null || typeof d.state === "string") ||
    !(
      d.assignmentId === null ||
      (Number.isSafeInteger(d.assignmentId) && d.assignmentId > 0)
    ) ||
    !(
      d.assigneeId === null ||
      (Number.isSafeInteger(d.assigneeId) && d.assigneeId > 0)
    ) ||
    !(
      d.actualStartAtUtc === null ||
      (typeof d.actualStartAtUtc === "string" &&
        /Z$/.test(d.actualStartAtUtc) &&
        Number.isFinite(Date.parse(d.actualStartAtUtc)))
    ) ||
    !tag(d.sourceETag) ||
    r.etag !== d.sourceETag ||
    ![
      "AVAILABLE",
      "UNAVAILABLE",
      "STATE_CONFLICT",
      "BLOCKER_RESOLUTION_CONTRACT_REQUIRED",
    ].includes(d.startCapability) ||
    !["PROVISIONAL_NOT_ACCEPTED", "UNAVAILABLE"].includes(d.reassignCapability)
  )
    throw new ApiError(502, "INVALID_CLEANING_EXECUTION");
  return d as CleaningExecution;
}
export interface ReassignmentCommand {
  assigneeId: number;
  dueAt: string;
  reason: string;
}
export function reassignmentCommand(
  plan: CleaningExecution,
  assignee: string,
  due: string,
  reason: string,
  now = Date.now(),
): ReassignmentCommand {
  const next = positiveId(assignee),
    note = reason.trim();
  if (
    plan.reassignCapability !== "PROVISIONAL_NOT_ACCEPTED" ||
    !["ASSIGNED", "IN_PROGRESS", "ON_HOLD"].includes(plan.state ?? "") ||
    next === plan.assigneeId ||
    !/Z$/.test(due) ||
    !Number.isFinite(Date.parse(due)) ||
    Date.parse(due) <= now ||
    note.length < 10 ||
    note.length > 500 ||
    /[\u0000-\u001f\u007f-\u009f]/.test(note)
  )
    throw Error("INVALID_REASSIGNMENT_COMMAND");
  return { assigneeId: next, dueAt: new Date(due).toISOString(), reason: note };
}
export async function mutateCleaningExecution(
  branch: string,
  task: string,
  action: "start" | "reassign",
  originalTag: string,
  command: ReassignmentCommand | { resolveBlocker: false },
  operation?: string,
) {
  const b = positiveId(branch),
    id = positiveId(task);
  if (!tag(originalTag) || (action === "reassign" && !guid(operation)))
    throw Error("INVALID_LIFECYCLE_COMMAND");
  const csrf = (await getJson("/auth/csrf")) as {
    headerName?: string;
    csrfToken?: string;
  };
  if (
    csrf?.headerName !== "X-CSRF-TOKEN" ||
    typeof csrf.csrfToken !== "string" ||
    !csrf.csrfToken.trim() ||
    /[\r\n]/.test(csrf.csrfToken)
  )
    throw new ApiError(502, "INVALID_CSRF_RESPONSE");
  const r = await send(
    `/branches/${b}/cleaning-tasks/${id}/${action}`,
    undefined,
    {
      method: "POST",
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json",
        "X-CSRF-TOKEN": csrf.csrfToken,
        "If-Match": originalTag,
        ...(action === "reassign" ? { "Idempotency-Key": operation! } : {}),
      },
      body: JSON.stringify(command),
    },
  );
  const d = r.body;
  if (
    !d ||
    d.id !== id ||
    d.branchId !== b ||
    !Number.isSafeInteger(d.assignmentId) ||
    d.assignmentId <= 0 ||
    !tag(r.etag)
  )
    throw new ApiError(502, "INVALID_LIFECYCLE_RESULT_RELOAD_SOURCE");
  if (
    action === "start" &&
    (d.state !== "IN_PROGRESS" ||
      !Number.isSafeInteger(d.intervalId) ||
      d.intervalId <= 0 ||
      !/Z$/.test(d.actualStartAtUtc))
  )
    throw new ApiError(502, "INVALID_START_RESULT_RELOAD_SOURCE");
  if (
    action === "reassign" &&
    (d.state !== "ASSIGNED" ||
      d.contract !== "PROVISIONAL_NOT_ACCEPTED" ||
      !guid(d.operationId) ||
      d.operationId.toLowerCase() !== operation!.toLowerCase() ||
      d.sourceETag !== r.etag ||
      d.notification !== "PENDING_DELIVERY" ||
      d.assigneeId !== (command as ReassignmentCommand).assigneeId)
  )
    throw new ApiError(502, "INVALID_REASSIGN_RESULT_RELOAD_SOURCE");
  return d;
}
