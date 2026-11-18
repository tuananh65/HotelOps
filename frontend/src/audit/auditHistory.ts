import type { AuditRow } from "./auditApi";

export function recordHistoryQuery(entry: AuditRow, current: string): string {
  const params = new URLSearchParams({
    page: "1",
    pageSize: "25",
    entityType: entry.entityType,
    entityId: entry.entityId,
  });
  const branch = new URLSearchParams(current).get("branchId");
  if (branch) params.set("branchId", branch);
  // A record history covers all dates/actions/actors in the current authorized branch scope.
  return params.toString();
}
type Scalar = string | number | boolean | null;
function fields(value: unknown, prefix = "", depth = 0): Record<string, Scalar> {
  if (!value || typeof value !== "object" || depth > 32) return {};
  const result: Record<string, Scalar> = Object.create(null);
  for (const [key, v] of Object.entries(value)) {
    if (key === "withheld") continue;
    const normalized = key.replaceAll("_", "").toLowerCase();
    // Writers wrap the new business snapshot while often writing the old one
    // directly. Compare those fields, not a fictitious removal/addition of State.
    const envelope = depth === 0 && ["state", "entity", "value"].includes(normalized)
      && v !== null && typeof v === "object" && !Array.isArray(v);
    const path = envelope ? prefix : prefix ? `${prefix}.${normalized}` : normalized;
    if (v === null || typeof v === "string" || typeof v === "boolean" ||
        (typeof v === "number" && Number.isFinite(v))) result[path] = v;
    else Object.assign(result, fields(v, path, depth + 1));
  }
  return result;
}
export function auditChanges(before: unknown, after: unknown) {
  const old = fields(before),
    next = fields(after);
  return [...new Set([...Object.keys(old), ...Object.keys(next)])]
    .sort()
    .filter(
      (key) =>
        !Object.hasOwn(old, key) ||
        !Object.hasOwn(next, key) ||
        old[key] !== next[key],
    )
    .map((key) => ({
      field: key,
      beforeRecorded: Object.hasOwn(old, key),
      afterRecorded: Object.hasOwn(next, key),
      before: old[key],
      after: next[key],
    }));
}
export function auditValue(value: Scalar | undefined, recorded: boolean) {
  return !recorded
    ? "Chưa ghi nhận hoặc không được hiển thị"
    : value === null
      ? "null"
      : String(value);
}
