export interface AuditRow {
  id: number;
  actor: string | null;
  action: string;
  entityType: string;
  entityId: string;
  occurredAt: string;
  changedFields?: string[] | null;
}
export interface AuditPage {
  items: AuditRow[];
  totalCount: number;
  page: number;
  pageSize: number;
}
export interface AuditDetail {
  entry: AuditRow;
  before: unknown;
  after: unknown;
  redacted: boolean;
  provenance: string;
  retention: AuditRetention | null;
}
export interface AuditRetention {
  archiveStatus: string;
  archivedAt: string | null;
  retentionUntil: string | null;
  archivedBy: string | null;
  legalHold: boolean;
  legalHoldUntil: string | null;
  purgeEligibility: string;
  reason: string;
}
function retention(v: unknown): AuditRetention | null {
  if (v == null) return null;
  if (!object(v) || ![v.archiveStatus, v.purgeEligibility, v.reason].every(x => typeof x === "string") ||
      ![v.archivedAt, v.retentionUntil, v.archivedBy, v.legalHoldUntil].every(x => x === null || typeof x === "string") ||
      typeof v.legalHold !== "boolean") throw new Error("Phản hồi lưu giữ nhật ký không hợp lệ");
  return v as unknown as AuditRetention;
}
const object = (v: unknown): v is Record<string, unknown> =>
  !!v && typeof v === "object" && !Array.isArray(v);
function row(v: unknown): AuditRow {
  if (
    !object(v) ||
    !Number.isSafeInteger(v.id) ||
    Number(v.id) <= 0 ||
    !(v.actor === null || typeof v.actor === "string") ||
    ![v.action, v.entityType, v.entityId, v.occurredAt].every(
      (x) => typeof x === "string",
    ) ||
    (v.changedFields != null &&
      (!Array.isArray(v.changedFields) ||
        !v.changedFields.every((x) => typeof x === "string")))
  )
    throw new Error("Phản hồi nhật ký không hợp lệ");
  return {
    id: v.id as number,
    actor: v.actor as string | null,
    action: v.action as string,
    entityType: v.entityType as string,
    entityId: v.entityId as string,
    occurredAt: v.occurredAt as string,
    changedFields: v.changedFields as string[] | null | undefined,
  };
}
export async function readAudit(
  query: string,
  id: number | null,
  signal: AbortSignal,
): Promise<AuditPage | AuditDetail> {
  if (id !== null && (!Number.isSafeInteger(id) || id <= 0))
    throw new Error("Bản ghi không hợp lệ");
  const response = await fetch(
    id === null ? `/api/audit-logs?${query}` : `/api/audit-logs/${id}`,
    {
      method: "GET",
      credentials: "same-origin",
      cache: "no-store",
      redirect: "error",
      signal,
      headers: { Accept: "application/json" },
    },
  );
  if (!response.ok) throw new AuditFailure(response.status);
  const v: unknown = await response.json();
  if (!object(v)) throw new Error("Phản hồi nhật ký không hợp lệ");
  if (id !== null) {
    const entry = row(v.entry);
    if (entry.id !== id || v.redacted !== true || typeof v.provenance !== "string")
      throw new Error("Chi tiết nhật ký không hợp lệ");
    return { entry, before: v.before, after: v.after, redacted: true,
      provenance: v.provenance, retention: retention(v.retention) };
  }
  if (
    !Array.isArray(v.items) ||
    !Number.isSafeInteger(v.totalCount) ||
    Number(v.totalCount) < 0 ||
    !Number.isSafeInteger(v.page) ||
    Number(v.page) < 1 ||
    !Number.isSafeInteger(v.pageSize) ||
    Number(v.pageSize) < 1 ||
    Number(v.pageSize) > 100
  )
    throw new Error("Trang nhật ký không hợp lệ");
  return {
    items: v.items.map(row),
    totalCount: v.totalCount as number,
    page: v.page as number,
    pageSize: v.pageSize as number,
  };
}
export class AuditFailure extends Error {
  constructor(readonly status: number) {
    super("Yêu cầu nhật ký thay đổi thất bại");
  }
}
