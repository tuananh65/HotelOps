import { ApiError, getJson, send } from "./client";
import { positiveId } from "./rooms";
import type { EvidenceUpload, CleaningProgressItem } from "./cleaningProgress";
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
  reportBlockerCapability: "UNAVAILABLE" | "PROVISIONAL_V1";
  completeCapability: "UNAVAILABLE" | "PROVISIONAL_V1";
  resumeCapability: "UNAVAILABLE" | "PROVISIONAL_V1";
  blockerReasons?: { id: number; code: string; name: string }[];
  items?: CleaningProgressItem[];
  progressETag?: string | null;
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
    !["PROVISIONAL_NOT_ACCEPTED", "UNAVAILABLE"].includes(
      d.reassignCapability,
    ) ||
    ![undefined, "UNAVAILABLE", "PROVISIONAL_V1"].includes(
      d.reportBlockerCapability,
    ) ||
    ![undefined, "UNAVAILABLE", "PROVISIONAL_V1"].includes(
      d.completeCapability,
    ) ||
    ![undefined, "UNAVAILABLE", "PROVISIONAL_V1"].includes(d.resumeCapability)
  )
    throw new ApiError(502, "INVALID_CLEANING_EXECUTION");
  if (
    (d.reportBlockerCapability === "PROVISIONAL_V1" ||
      d.completeCapability === "PROVISIONAL_V1") &&
    (d.state !== "IN_PROGRESS" ||
      !tag(d.progressETag) ||
      !Array.isArray(d.blockerReasons) ||
      d.blockerReasons.some(
        (r: any) =>
          !Number.isSafeInteger(r.id) ||
          r.id <= 0 ||
          typeof r.code !== "string" ||
          typeof r.name !== "string",
      ) ||
      !Array.isArray(d.items) ||
      d.items.length < 1 ||
      d.items.some(
        (i: any) =>
          !Number.isSafeInteger(i.id) ||
          i.id <= 0 ||
          typeof i.label !== "string" ||
          typeof i.required !== "boolean" ||
          typeof i.done !== "boolean",
      ))
  )
    throw new ApiError(502, "INVALID_CLOSURE_METADATA");
  return {
    ...d,
    reportBlockerCapability: d.reportBlockerCapability ?? "UNAVAILABLE",
    completeCapability: d.completeCapability ?? "UNAVAILABLE",
    resumeCapability: d.resumeCapability ?? "UNAVAILABLE",
  } as CleaningExecution;
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
  action: "start" | "reassign" | "blockers" | "complete",
  originalTag: string,
  command:
    | ReassignmentCommand
    | { resolveBlocker: boolean; note?: string }
    | ClosureCommand,
  operation?: string,
) {
  const b = positiveId(branch),
    id = positiveId(task);
  if (!tag(originalTag) || (action !== "start" && !guid(operation)))
    throw Error("INVALID_LIFECYCLE_COMMAND");
  if (
    action === "start" &&
    "resolveBlocker" in command &&
    command.resolveBlocker &&
    (typeof command.note !== "string" ||
      !command.note.trim() ||
      command.note.length > 500 ||
      /[\u0000-\u001f\u007f-\u009f]/.test(command.note))
  )
    throw Error("INVALID_BLOCKER_RESOLUTION_NOTE");
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
        ...(action !== "start" ? { "Idempotency-Key": operation! } : {}),
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
      typeof d.actualStartAtUtc !== "string" ||
      !/Z$/.test(d.actualStartAtUtc) ||
      !Number.isFinite(Date.parse(d.actualStartAtUtc)))
  )
    throw new ApiError(502, "INVALID_START_RESULT_RELOAD_SOURCE");
  if (
    (action === "blockers" || action === "complete") &&
    (d.state !== (action === "blockers" ? "ON_HOLD" : "PENDING_INSPECTION") ||
      d.operationId?.toLowerCase() !== operation!.toLowerCase() ||
      !guid(d.publicationId) ||
      !Number.isSafeInteger(d.progressId) ||
      d.progressId <= 0 ||
      !Number.isSafeInteger(d.closedIntervalId) ||
      d.closedIntervalId <= 0 ||
      d.sourceETag !== r.etag ||
      d.notification !== "PENDING_DELIVERY" ||
      d.contract !== "PROVISIONAL_NOT_ACCEPTED" ||
      typeof d.occurredAtUtc !== "string" ||
      !/Z$/.test(d.occurredAtUtc) ||
      !Number.isFinite(Date.parse(d.occurredAtUtc)) ||
      d.readiness !==
        (action === "blockers" ? "PENDING_CLEANING" : "PENDING_INSPECTION") ||
      typeof d.hasEffectiveBlock !== "boolean")
  )
    throw new ApiError(502, "INVALID_CLOSURE_RESULT_RELOAD_SOURCE");
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

export interface ClosureCommand {
  note: string;
  reasonId?: number;
  progressETag?: string;
  photos?: EvidenceUpload[];
}
export function closureCommand(
  plan: CleaningExecution,
  action: "blockers" | "complete",
  note: string,
  reason?: string,
  photos: EvidenceUpload[] = [],
): ClosureCommand {
  const text = note.trim();
  if (
    plan.state !== "IN_PROGRESS" ||
    (action === "blockers"
      ? plan.reportBlockerCapability
      : plan.completeCapability) !== "PROVISIONAL_V1" ||
    !text ||
    text.length > 500 ||
    /[\u0000-\u001f\u007f-\u009f]/.test(text) ||
    photos.length > 5
  )
    throw Error("INVALID_CLOSURE_COMMAND");
  if (action === "blockers") {
    const reasonId = positiveId(reason ?? "");
    if (!plan.blockerReasons?.some((r) => r.id === reasonId))
      throw Error("INVALID_BLOCKER_REFERENCE");
    return { note: text, reasonId, ...(photos.length ? { photos } : {}) };
  }
  if (
    !tag(plan.progressETag) ||
    !plan.items?.length ||
    plan.items.some((i) => i.required && i.done !== true)
  )
    throw Error("REQUIRED_ITEMS_INCOMPLETE");
  return {
    note: text,
    progressETag: plan.progressETag,
    ...(photos.length ? { photos } : {}),
  };
}
