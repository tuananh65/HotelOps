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
function fields(value: unknown): Record<string, Scalar> {
  if (!value || typeof value !== "object" || Array.isArray(value)) return {};
  return Object.fromEntries(
    Object.entries(value).filter(
      ([key, v]) =>
        key !== "withheld" &&
        (v === null ||
          typeof v === "string" ||
          typeof v === "boolean" ||
          (typeof v === "number" && Number.isFinite(v))),
    ),
  );
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
    ? "Not recorded or withheld"
    : value === null
      ? "null"
      : String(value);
}
