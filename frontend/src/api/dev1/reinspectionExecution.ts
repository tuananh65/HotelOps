import { ApiError, getJson, send } from "./client";
import { positiveId } from "./rooms";
import type { EvidenceUpload } from "../../features/inspectionExecution/model";

export interface ReinspectionDefect {
  id: number;
  description: string;
  remediation: string;
  fixed: boolean | null;
  note: string | null;
  originalPhotos: number[];
  remediationPhotos: number[];
  reinspectionPhotos: number[];
}
export interface ReinspectionExecution {
  id: number;
  reworkId: number;
  branchId: number;
  roomId: number;
  cleaningTaskId: number;
  originalInspectionId: number;
  inspectorId: number;
  versionId: number;
  state: "ASSIGNED" | "PASSED" | "FAILED";
  sourceETag: string;
  reworkETag: string;
  bindingETag: string;
  defects: ReinspectionDefect[];
  effectiveReadiness: string;
  messageCode: string | null;
  conclusion?: ReinspectionConclusion | null;
}
export interface ReinspectionConclusion {
  id: number;
  reworkId: number;
  branchId: number;
  roomId: number;
  cleaningTaskId: number;
  state: "PASSED" | "FAILED";
  reworkState: "COMPLETED" | "FAILED";
  sourceETag: string;
  reworkETag: string;
  effectiveReadiness: string;
  messageCode: string;
}
export interface DefectDecision {
  defectId: number;
  fixed: boolean;
  note: string;
  photos: EvidenceUpload[];
}
const id = (v: unknown): v is number =>
  Number.isSafeInteger(v) && (v as number) > 0;
const tag = (v: unknown): v is string =>
  typeof v === "string" && /^"[0-9A-F]{16}"$/.test(v);
const invalid = (): never => {
  throw new ApiError(502, "INVALID_REINSPECTION_RESPONSE");
};
const path = (branch: string, inspection: string) =>
  `/branches/${positiveId(branch)}/reinspections/${positiveId(inspection)}`;
export const reinspectionEvidenceUrl = (
  branch: string,
  inspection: string,
  file: number,
) => {
  if (!id(file)) throw new Error("INVALID_ATTACHMENT");
  return `/api${path(branch, inspection)}/evidence/${file}`;
};
export async function readReinspection(
  branch: string,
  inspection: string,
  signal: AbortSignal,
): Promise<ReinspectionExecution> {
  const response = await send(path(branch, inspection) + "/execution", signal, {
    method: "GET",
    headers: { Accept: "application/json" },
  });
  const d = response.body;
  const terminal = d?.state === "PASSED" || d?.state === "FAILED";
  if (
    !d ||
    d.id !== positiveId(inspection) ||
    d.branchId !== positiveId(branch) ||
    ![
      d.reworkId,
      d.roomId,
      d.cleaningTaskId,
      d.originalInspectionId,
      d.inspectorId,
      d.versionId,
    ].every(id) ||
    ![d.sourceETag, d.reworkETag, d.bindingETag].every(tag) ||
    response.etag !== d.sourceETag ||
    (!terminal && d.state !== "ASSIGNED") ||
    !Array.isArray(d.defects) ||
    !d.defects.length ||
    d.defects.length > 200 ||
    d.defects.some(
      (v: ReinspectionDefect) =>
        !v ||
        !id(v.id) ||
        typeof v.description !== "string" ||
        typeof v.remediation !== "string" ||
        !v.remediation.trim() ||
        (terminal
          ? typeof v.fixed !== "boolean" ||
            !(
              v.note === null ||
              (typeof v.note === "string" && v.note.length <= 500)
            )
          : v.fixed !== null || v.note !== null) ||
        ![v.originalPhotos, v.remediationPhotos, v.reinspectionPhotos].every(
          (a) => Array.isArray(a) && a.every(id),
        ) ||
        (!terminal && v.reinspectionPhotos.length !== 0),
    ) ||
    new Set(d.defects.map((v: ReinspectionDefect) => v.id)).size !==
      d.defects.length ||
    ![
      "PENDING_CLEANING",
      "PENDING_INSPECTION",
      "REWORK_REQUIRED",
      "BLOCKED",
      ...(terminal
        ? ["READY", "UNKNOWN", "INACTIVE", "CLEANING_IN_PROGRESS"]
        : []),
    ].includes(d.effectiveReadiness) ||
    d.messageCode !== (d.effectiveReadiness === "BLOCKED" ? "MSG17" : null)
  )
    invalid();
  if (terminal) {
    const c = d.conclusion;
    if (
      !c ||
      c.contract !== "FE05_REINSPECTION_CONCLUSION_V1" ||
      c.notification !== "PENDING_DELIVERY" ||
      ![
        "id",
        "branchId",
        "reworkId",
        "roomId",
        "cleaningTaskId",
        "state",
        "sourceETag",
        "reworkETag",
      ].every((key) => c[key] === d[key]) ||
      typeof c.operationId !== "string" ||
      c.operationId.replaceAll("-", "") === "0".repeat(32) ||
      !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(
        c.operationId,
      ) ||
      c.reworkState !== (d.state === "PASSED" ? "COMPLETED" : "FAILED") ||
      !["BLOCKED", d.state === "PASSED" ? "READY" : "REWORK_REQUIRED"].includes(
        c.effectiveReadiness,
      ) ||
      c.messageCode !==
        (c.effectiveReadiness === "BLOCKED" ? "MSG17" : "MSG08") ||
      (d.state === "PASSED"
        ? d.defects.some((x: ReinspectionDefect) => x.fixed !== true)
        : !d.defects.some((x: ReinspectionDefect) => x.fixed === false))
    )
      invalid();
  } else if (d.conclusion != null) invalid();
  return d;
}
export async function concludeReinspection(
  branch: string,
  inspection: string,
  source: ReinspectionExecution,
  operation: string,
  reason: string,
  defects: DefectDecision[],
  scope: { signal: AbortSignal; isCurrent: () => boolean },
): Promise<ReinspectionConclusion> {
  if (
    !/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(
      operation,
    ) ||
    source.id !== positiveId(inspection) ||
    source.branchId !== positiveId(branch) ||
    source.state !== "ASSIGNED" ||
    ![source.sourceETag, source.reworkETag, source.bindingETag].every(tag) ||
    !reason.trim() ||
    reason.length > 500 ||
    /[\u0000-\u001f\u007f-\u009f]/.test(reason) ||
    defects.length !== source.defects.length ||
    new Set(defects.map((d) => d.defectId)).size !== defects.length ||
    defects.some(
      (d) =>
        !source.defects.some((s) => s.id === d.defectId) ||
        typeof d.fixed !== "boolean" ||
        d.note.length > 500,
    ) ||
    defects.reduce((n, d) => n + d.photos.length, 0) > 5
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
  const response = await send(
    path(branch, inspection) + "/conclude",
    scope.signal,
    {
      method: "POST",
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json",
        "X-CSRF-TOKEN": csrf.csrfToken,
        "If-Match": source.sourceETag,
        "Idempotency-Key": operation,
      },
      body: JSON.stringify({
        reworkETag: source.reworkETag,
        bindingETag: source.bindingETag,
        reason,
        defects,
      }),
    },
  );
  current();
  const d = response.body,
    passed = defects.every((d) => d.fixed);
  if (
    !d ||
    d.id !== source.id ||
    d.branchId !== source.branchId ||
    d.reworkId !== source.reworkId ||
    d.roomId !== source.roomId ||
    d.cleaningTaskId !== source.cleaningTaskId ||
    d.contract !== "FE05_REINSPECTION_CONCLUSION_V1" ||
    d.operationId?.toLowerCase() !== operation.toLowerCase() ||
    d.notification !== "PENDING_DELIVERY" ||
    !tag(d.sourceETag) ||
    !tag(d.reworkETag) ||
    response.etag !== d.sourceETag ||
    d.state !== (passed ? "PASSED" : "FAILED") ||
    d.reworkState !== (passed ? "COMPLETED" : "FAILED") ||
    !["BLOCKED", passed ? "READY" : "REWORK_REQUIRED"].includes(
      d.effectiveReadiness,
    ) ||
    d.messageCode !== (d.effectiveReadiness === "BLOCKED" ? "MSG17" : "MSG08")
  )
    invalid();
  return d;
}
