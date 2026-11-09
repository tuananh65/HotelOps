import { ApiError, getJson, send } from "./client";
import { positiveId } from "./rooms";
import type { EvidenceUpload } from "./cleaningProgress";
export type ReworkAction = "accept" | "start" | "complete";
export interface ReworkExecution {
  id: number;
  branchId: number;
  cleaningTaskId: number;
  inspectionId: number;
  roomId: number;
  assigneeId: number;
  state: string;
  dueAtUtc: string | null;
  sourceETag: string;
  inspectionETag: string;
  capability: "PROVISIONAL_NOT_ACCEPTED";
  sourceAuthority: "SYNTHETIC_TEST_ONLY" | "FE05_COMMITTED_V1";
  defects: {
    id: number;
    description: string;
    remediation: string | null;
    inspectionAttachmentIds: number[];
    remediationAttachmentIds: number[];
  }[];
}
export interface ReworkCommand {
  note: string;
  remediations?: {
    defectId: number;
    note: string;
    photos?: EvidenceUpload[];
  }[];
}
const validTag = (v: unknown): v is string =>
  typeof v === "string" && /^"[0-9A-F]{16}"$/.test(v);
const validId = (v: unknown): v is number =>
  typeof v === "number" && Number.isSafeInteger(v) && v > 0;
const validGuid = (v: unknown): v is string =>
  typeof v === "string" &&
  /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(
    v,
  );
const validNote = (v: unknown): v is string =>
  typeof v === "string" &&
  v.trim().length > 0 &&
  v.trim().length <= 500 &&
  !/[\u0000-\u001f\u007f-\u009f]/.test(v);
function invalid(): never {
  throw new ApiError(502, "INVALID_REWORK_RESPONSE");
}
export async function readRework(
  branch: string,
  id: string,
  signal?: AbortSignal,
): Promise<ReworkExecution> {
  const b = positiveId(branch),
    r = positiveId(id);
  const result = await send(`/branches/${b}/reworks/${r}/execution`, signal, {
    method: "GET",
    headers: { Accept: "application/json" },
  });
  const d = result.body;
  if (
    !d ||
    d.id !== r ||
    d.branchId !== b ||
    ![d.cleaningTaskId, d.inspectionId, d.roomId, d.assigneeId].every(
      validId,
    ) ||
    ![
      "PENDING_ACCEPTANCE",
      "ACCEPTED",
      "IN_PROGRESS",
      "PENDING_REINSPECTION",
    ].includes(d.state) ||
    d.capability !== "PROVISIONAL_NOT_ACCEPTED" ||
    !["SYNTHETIC_TEST_ONLY", "FE05_COMMITTED_V1"].includes(d.sourceAuthority) ||
    !validTag(d.sourceETag) ||
    result.etag !== d.sourceETag ||
    !validTag(d.inspectionETag) ||
    !(
      d.dueAtUtc === null ||
      (typeof d.dueAtUtc === "string" &&
        /Z$/.test(d.dueAtUtc) &&
        Number.isFinite(Date.parse(d.dueAtUtc)))
    ) ||
    !Array.isArray(d.defects) ||
    d.defects.length < 1 ||
    d.defects.length > 200 ||
    d.defects.some(
      (x: ReworkExecution["defects"][number]) =>
        !x ||
        !validId(x.id) ||
        typeof x.description !== "string" ||
        !(x.remediation === null || typeof x.remediation === "string") ||
        ![x.inspectionAttachmentIds, x.remediationAttachmentIds].every(
          (a) => Array.isArray(a) && a.every(validId),
        ),
    ) ||
    new Set(d.defects.map((x: { id: number }) => x.id)).size !==
      d.defects.length
  )
    invalid();
  return d;
}
export function validateReworkCommand(
  action: ReworkAction,
  command: ReworkCommand,
) {
  if (
    !["accept", "start", "complete"].includes(action) ||
    !validNote(command.note)
  )
    throw Error("INVALID_REWORK_COMMAND");
  if (action === "complete") {
    const rows = command.remediations;
    if (
      !rows?.length ||
      rows.length > 200 ||
      rows.some((x) => !validId(x.defectId) || !validNote(x.note)) ||
      new Set(rows.map((x) => x.defectId)).size !== rows.length ||
      rows.reduce((n, x) => n + (x.photos?.length || 0), 0) > 5
    )
      throw Error("INVALID_REMEDIATION");
  } else if (command.remediations?.length) throw Error("INVALID_REMEDIATION");
}
export async function mutateRework(
  branch: string,
  id: string,
  action: ReworkAction,
  tag: string,
  operation: string,
  command: ReworkCommand,
) {
  const b = positiveId(branch),
    r = positiveId(id);
  if (!validTag(tag) || !validGuid(operation))
    throw Error("INVALID_REWORK_COMMAND");
  validateReworkCommand(action, command);
  const csrf = (await getJson("/auth/csrf")) as {
    headerName?: string;
    csrfToken?: string;
  };
  if (
    csrf?.headerName !== "X-CSRF-TOKEN" ||
    !csrf.csrfToken?.trim() ||
    /[\r\n]/.test(csrf.csrfToken)
  )
    throw new ApiError(502, "INVALID_CSRF_RESPONSE");
  const response = await send(
    `/branches/${b}/reworks/${r}/${action}`,
    undefined,
    {
      method: "POST",
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json",
        "X-CSRF-TOKEN": csrf.csrfToken,
        "If-Match": tag,
        "Idempotency-Key": operation,
      },
      body: JSON.stringify(command),
    },
  );
  const d = response.body;
  if (
    !d ||
    d.id !== r ||
    d.branchId !== b ||
    d.action !== action ||
    d.operationId?.toLowerCase() !== operation.toLowerCase() ||
    ![d.cleaningTaskId, d.inspectionId, d.assigneeId].every(validId) ||
    !validTag(d.sourceETag) ||
    response.etag !== d.sourceETag ||
    d.contract !== "PROVISIONAL_NOT_ACCEPTED" ||
    typeof d.occurredAtUtc !== "string" ||
    !/Z$/.test(d.occurredAtUtc) ||
    !Number.isFinite(Date.parse(d.occurredAtUtc)) ||
    d.state !==
      {
        accept: "ACCEPTED",
        start: "IN_PROGRESS",
        complete: "PENDING_REINSPECTION",
      }[action] ||
    d.readiness !==
      (action === "complete" ? "PENDING_INSPECTION" : "REWORK_REQUIRED") ||
    (action === "accept" ? d.intervalId !== null : !validId(d.intervalId)) ||
    (action === "complete"
      ? !validGuid(d.publicationId) || d.notification !== "PENDING_DELIVERY"
      : d.publicationId !== null || d.notification !== "NOT_REQUIRED")
  )
    invalid();
  return d;
}
