export type StructureKind = "floor" | "area";
export interface StructureRecord {
  kind: StructureKind;
  id: number;
  branchId: number;
  code: string;
  name: string;
  description: string | null;
  floorId: number | null;
  floorNumber: number | null;
  isActive: boolean;
}
export interface Snapshot {
  record: StructureRecord;
  etag: string;
}
export type Failure = {
  reason:
    | "INVALID_REQUEST"
    | "DISABLED"
    | "STALE"
    | "CANCELLED"
    | "NETWORK"
    | "INVALID_RESPONSE"
    | "HTTP";
  status?: number;
  code?: string;
  dependencies?: readonly { kind: string; count: number }[];
};
export type ReadResult =
  { ok: true; data: Snapshot } | { ok: false; error: Failure };
export type WriteResult =
  | { outcome: "CONFIRMED"; data: Snapshot }
  | {
      outcome: "NOT_DISPATCHED" | "REJECTED" | "OUTCOME_UNKNOWN";
      error: Failure;
    };
export const safeId = (v: number) => Number.isSafeInteger(v) && v > 0;
export const strongTag = (v: unknown): v is string =>
  typeof v === "string" && /^"[0-9A-F]{64}"$/.test(v);
export const validReason = (v: string) =>
  v.trim().length >= 10 &&
  v.trim().length <= 1000 &&
  !/[\x00-\x1f\x7f-\x9f]/.test(v);
export function decodeRecord(
  v: unknown,
  kind: StructureKind,
  branchId: number,
  id: number,
): StructureRecord | undefined {
  if (!v || typeof v !== "object") return;
  const x = v as Record<string, unknown>;
  if (
    x.kind !== kind ||
    x.id !== id ||
    x.branchId !== branchId ||
    typeof x.code !== "string" ||
    !x.code ||
    x.code.length > 60 ||
    typeof x.name !== "string" ||
    !x.name ||
    x.name.length > (kind === "floor" ? 100 : 150) ||
    !(x.description === null || typeof x.description === "string") ||
    !(
      x.floorId === null ||
      (typeof x.floorId === "number" && safeId(x.floorId))
    ) ||
    !(
      x.floorNumber === null ||
      (typeof x.floorNumber === "number" && Number.isInteger(x.floorNumber))
    ) ||
    typeof x.isActive !== "boolean" ||
    (kind === "floor" && x.floorId !== null) ||
    (kind === "area" && x.floorNumber !== null)
  )
    return;
  return {
    kind,
    id,
    branchId,
    code: x.code,
    name: x.name,
    description: x.description,
    floorId: x.floorId,
    floorNumber: x.floorNumber,
    isActive: x.isActive,
  } as StructureRecord;
}
export function failureMessage(e: Failure): string {
  if (e.reason === "DISABLED")
    return "Live deactivation is disabled pending backend acceptance and Project Leader approval.";
  if (e.status === 401) return "Session invalid. Revalidate before continuing.";
  if (e.status === 403)
    return "Server denied access. No permission is inferred from this screen.";
  if (e.status === 412)
    return "Version changed. Refresh and review again; no automatic replay.";
  if (e.status === 409)
    return e.code === "STRUCTURE_DEPENDENCIES_EXIST"
      ? "Dependencies block deactivation."
      : "Conflict: record cannot be deactivated.";
  if (e.status === 503)
    return e.code === "STRUCTURE_WRITER_COORDINATION_REQUIRED"
      ? "Writer coordination has not been accepted. Deactivation unavailable."
      : "Service unavailable. Deactivation outcome may be unknown.";
  return "Operation unavailable. No automatic retry.";
}
