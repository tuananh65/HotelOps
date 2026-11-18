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
    return "Vô hiệu hóa thực tế bị khóa, chờ nghiệm thu backend và trưởng dự án phê duyệt.";
  if (e.status === 401) return "Phiên đăng nhập không hợp lệ. Xác minh lại trước khi tiếp tục.";
  if (e.status === 403)
    return "Máy chủ từ chối truy cập. Màn hình này không tự cấp quyền.";
  if (e.status === 412)
    return "Phiên bản đã đổi. Tải lại và xét duyệt; không tự gửi lại.";
  if (e.status === 409)
    return e.code === "STRUCTURE_DEPENDENCIES_EXIST"
      ? "Các ràng buộc đang chặn vô hiệu hóa."
      : "Xung đột: không thể vô hiệu hóa hồ sơ.";
  if (e.status === 503)
    return e.code === "STRUCTURE_WRITER_COORDINATION_REQUIRED"
      ? "Chưa chấp nhận cơ chế phối hợp ghi. Không thể vô hiệu hóa."
      : "Dịch vụ chưa khả dụng. Kết quả vô hiệu hóa có thể chưa xác định.";
  return "Thao tác chưa khả dụng. Không tự gửi lại.";
}
