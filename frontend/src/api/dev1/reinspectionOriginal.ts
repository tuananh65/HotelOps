import { ApiError, send } from "./client";
import { positiveId } from "./rooms";

export interface ReinspectionOriginal {
  reinspectionId: number;
  reworkId: number;
  branchId: number;
  id: number;
  roomId: number;
  cleaningTaskId: number;
  inspectorId: number;
  versionId: number;
  state: "FAILED";
  sourceETag: string;
  summary: string | null;
  criteria: {
    itemId: number;
    criterion: string;
    description: string | null;
    outcome: string | null;
    note: string | null;
    evidenceIds: number[];
  }[];
  defects: {
    id: number;
    itemId: number | null;
    description: string;
    location: string | null;
    evidenceIds: number[];
  }[];
  evidence: {
    id: number;
    entityType: "INSPECTION";
    entityId: number;
    name: string;
    downloadPath: string;
  }[];
}
const id = (value: unknown): value is number =>
  Number.isSafeInteger(value) && (value as number) > 0;
const nullableText = (value: unknown) =>
  value === null || typeof value === "string";
const path = (branch: string, inspection: string) =>
  `/branches/${positiveId(branch)}/reinspections/${positiveId(inspection)}/original`;
export function reinspectionOriginalEvidenceUrl(
  branch: string,
  inspection: string,
  file: number,
) {
  if (!id(file)) throw new Error("INVALID_ATTACHMENT");
  return `/api${path(branch, inspection)}/evidence/${file}`;
}
export async function readReinspectionOriginal(
  branch: string,
  inspection: string,
  signal: AbortSignal,
): Promise<ReinspectionOriginal> {
  const response = await send(path(branch, inspection), signal, {
    method: "GET",
    headers: { Accept: "application/json" },
  });
  const d = response.body as ReinspectionOriginal;
  const invalid = (): never => {
    throw new ApiError(502, "INVALID_ORIGINAL_INSPECTION_RESPONSE");
  };
  if (
    !d ||
    d.reinspectionId !== positiveId(inspection) ||
    d.branchId !== positiveId(branch) ||
    ![
      d.reworkId,
      d.id,
      d.roomId,
      d.cleaningTaskId,
      d.inspectorId,
      d.versionId,
    ].every(id) ||
    d.id === d.reinspectionId ||
    d.state !== "FAILED" ||
    typeof d.sourceETag !== "string" ||
    !/^"[0-9A-F]{16}"$/.test(d.sourceETag) ||
    response.etag !== d.sourceETag ||
    !nullableText(d.summary) ||
    !Array.isArray(d.evidence) ||
    d.evidence.some(
      (e) =>
        !e ||
        !id(e.id) ||
        e.entityType !== "INSPECTION" ||
        e.entityId !== d.id ||
        typeof e.name !== "string" ||
        e.downloadPath !==
          reinspectionOriginalEvidenceUrl(branch, inspection, e.id),
    ) ||
    new Set(d.evidence.map((e) => e.id)).size !== d.evidence.length
  )
    invalid();
  const allowed = new Set(d.evidence.map((e) => e.id));
  const files = (value: unknown) =>
    Array.isArray(value) &&
    value.every((v) => id(v) && allowed.has(v)) &&
    new Set(value).size === value.length;
  if (
    !Array.isArray(d.criteria) ||
    !d.criteria.length ||
    d.criteria.some(
      (c) =>
        !c ||
        !id(c.itemId) ||
        typeof c.criterion !== "string" ||
        !nullableText(c.description) ||
        !nullableText(c.note) ||
        !(c.outcome === null || ["PASS", "FAIL"].includes(c.outcome)) ||
        !files(c.evidenceIds),
    ) ||
    new Set(d.criteria.map((c) => c.itemId)).size !== d.criteria.length ||
    !Array.isArray(d.defects) ||
    !d.defects.length ||
    d.defects.some(
      (v) =>
        !v ||
        !id(v.id) ||
        !(v.itemId === null || d.criteria.some((c) => c.itemId === v.itemId)) ||
        typeof v.description !== "string" ||
        !nullableText(v.location) ||
        !files(v.evidenceIds),
    ) ||
    new Set(d.defects.map((v) => v.id)).size !== d.defects.length
  )
    invalid();
  return d;
}
