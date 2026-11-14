export interface InspectionHostContext {
  authorization: "unresolved" | "denied" | "granted";
  sessionKey: string;
  principalId: string;
  branchId: number | string;
  inspectionPerform: boolean;
}
export interface Criterion {
  id: number | null;
  itemId: number;
  criterion: string;
  description: string | null;
  mandatory: boolean;
  passCondition: string | null;
  outcome: string | null;
  note: string | null;
  at: string | null;
  group?: string | null;
  evidenceIds?: number[] | null;
}
export interface Defect {
  id: number;
  itemId: number | null;
  description: string;
  status: string;
  severityId: number | null;
  location: string | null;
  evidenceIds?: number[] | null;
}
export interface Inspection {
  id: number;
  branchId: number;
  roomId: number;
  cleaningTaskId: number;
  inspectorId: number;
  versionId: number;
  state: string;
  dueAt: string | null;
  criteria: Criterion[];
  defects: Defect[];
  evidence: Array<{
    id: number;
    entityType: string;
    entityId: number;
    name: string;
    mimeType: string | null;
    bytes: number | null;
  }>;
}
export interface LoadedInspection {
  value: Inspection;
  etag: string;
}
export interface EvidenceUpload {
  name: string;
  mimeType: string;
  base64: string;
}
export interface CriterionResult {
  itemId: number;
  outcome: "PASS" | "FAIL";
  note: string | null;
  photos?: EvidenceUpload[];
}
export interface SeverityOption {
  id: number;
  code: "MINOR" | "MAJOR";
  name: string;
}
export const MAX_EVIDENCE_BYTES = 10 * 1024 * 1024;
const tag = /^"[0-9A-F]{16}"$/;
export function positiveId(value: number | string): number {
  if (!/^[1-9]\d*$/.test(String(value)) || !Number.isSafeInteger(Number(value)))
    throw new Error("ID must be a positive safe integer");
  return Number(value);
}
export function strongTag(value: string): boolean {
  return tag.test(value);
}
export function scopeOf(
  context?: InspectionHostContext,
): { branch: number; key: string } | null {
  if (
    !context ||
    context.authorization !== "granted" ||
    context.inspectionPerform !== true ||
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
export function reason(value: string): string {
  const s = value.trim();
  if (!s || s.length > 500 || /[\x00-\x1f\x7f-\x9f]/.test(s))
    throw new Error("Reason must contain 1-500 safe characters");
  return s;
}
export function optionalNote(value: string, max = 4000): string | null {
  const s = value.trim();
  if (s.length > max || /[\x00-\x1f\x7f-\x9f]/.test(s))
    throw new Error("Invalid note");
  return s || null;
}
export function decodeInspection(
  raw: unknown,
  branch: number,
  inspection: number,
  etag: string,
): LoadedInspection {
  if (
    !strongTag(etag) ||
    typeof raw !== "object" ||
    raw === null ||
    Array.isArray(raw)
  )
    throw new Error("Invalid execution response");
  const v = raw as Inspection;
  if (
    v.id !== inspection ||
    v.branchId !== branch ||
    ![v.roomId, v.cleaningTaskId, v.inspectorId, v.versionId].every(
      (x) => Number.isSafeInteger(x) && x > 0,
    ) ||
    typeof v.state !== "string" ||
    !Array.isArray(v.criteria) ||
    !Array.isArray(v.defects) ||
    !Array.isArray(v.evidence) ||
    v.criteria.some(
      (c) =>
        !Number.isSafeInteger(c.itemId) ||
        c.itemId <= 0 ||
        typeof c.criterion !== "string" ||
        typeof c.mandatory !== "boolean" ||
        !(c.outcome === null || c.outcome === "PASS" || c.outcome === "FAIL"),
    ) ||
    v.defects.some((d) => !Number.isSafeInteger(d.id) || d.id <= 0)
  )
    throw new Error("Invalid execution response or branch");
  if (new Set(v.criteria.map((c) => c.itemId)).size !== v.criteria.length)
    throw new Error("Ambiguous criterion source");
  return { value: v, etag };
}
export function validateResults(
  value: Inspection,
  results: CriterionResult[],
): CriterionResult[] {
  if (
    value.state !== "INSPECTING" ||
    results.length < 1 ||
    results.length > 200
  )
    throw new Error("Inspection not editable");
  const allowed = new Set(value.criteria.map((c) => c.itemId));
  if (
    new Set(results.map((r) => r.itemId)).size !== results.length ||
    results.some(
      (r) => !allowed.has(r.itemId) || !["PASS", "FAIL"].includes(r.outcome),
    )
  )
    throw new Error("Criterion/version mismatch");
  if (results.reduce((n, r) => n + (r.photos?.length ?? 0), 0) > 5)
    throw new Error("Too many files");
  return results;
}
export function trustedSeverity(
  options: SeverityOption[] | undefined,
  requestedId: number,
): boolean {
  return (
    !!options &&
    options.some(
      (s) =>
        s.id === requestedId &&
        Number.isSafeInteger(s.id) &&
        s.id > 0 &&
        (s.code === "MINOR" || s.code === "MAJOR"),
    )
  );
}
export async function encodeEvidence(
  file: File,
  signal?: AbortSignal,
): Promise<EvidenceUpload> {
  const mime = file.type;
  if (
    !["image/png", "image/jpeg", "application/pdf"].includes(mime) ||
    file.size < 1 ||
    file.size > MAX_EVIDENCE_BYTES ||
    file.name.length > 200 ||
    /[\\/:\x00-\x1f]/.test(file.name)
  )
    throw new Error("Unsupported evidence file");
  const ext = file.name.toLowerCase().split(".").pop();
  if (!(
    (mime === "image/png" && ext === "png") ||
    (mime === "image/jpeg" && (ext === "jpg" || ext === "jpeg")) ||
    (mime === "application/pdf" && ext === "pdf")
  ))
    throw new Error("Evidence extension mismatch");
  signal?.throwIfAborted();
  const bytes = new Uint8Array(await file.arrayBuffer());
  signal?.throwIfAborted();
  const signature =
    mime === "image/png"
      ? [137, 80, 78, 71, 13, 10, 26, 10]
      : mime === "image/jpeg"
        ? [255, 216, 255]
        : [37, 80, 68, 70, 45];
  if (!signature.every((b, i) => bytes[i] === b))
    throw new Error("Evidence signature mismatch");
  let data = "";
  for (let i = 0; i < bytes.length; i += 8192)
    data += String.fromCharCode(...bytes.subarray(i, i + 8192));
  signal?.throwIfAborted();
  return { name: file.name, mimeType: mime, base64: btoa(data) };
}
