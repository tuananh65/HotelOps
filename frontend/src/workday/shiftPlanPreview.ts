/** Internal ShiftPlanPreview projection only. No HTTP, admission, version or save contract. */
export interface ShiftDraft {
  readonly employeeId: number;
  readonly branchId: number;
  readonly departmentId: number;
  readonly templateId: number;
  readonly shiftDate: string;
}
export const issueLabels = [
  "Invalid draft",
  "Scope unavailable",
  "Template unavailable",
  "Employee unavailable",
  "Employee department unconfirmed",
  "Timezone unavailable",
  "Invalid time",
  "Existing assignment overlap",
  "Proposed assignment overlap",
  "Data unavailable",
] as const;
export interface PreviewRow {
  readonly index: number;
  readonly startUtc: string | null;
  readonly endUtc: string | null;
  readonly issues: readonly number[];
}
export interface Preview {
  readonly rows: readonly PreviewRow[];
  readonly requiresServerRevalidation: true;
}
export type PreviewAdapter = (
  drafts: readonly ShiftDraft[],
  signal: AbortSignal,
) => Promise<unknown>;
const id = (v: unknown): v is number =>
  typeof v === "number" && Number.isSafeInteger(v) && v > 0;
export function calendarDate(v: unknown): v is string {
  if (
    typeof v !== "string" ||
    !/^\d{4}-\d{2}-\d{2}$/.test(v) ||
    v.startsWith("0000")
  )
    return false;
  const d = new Date(v + "T00:00:00Z");
  return Number.isFinite(d.getTime()) && d.toISOString().slice(0, 10) === v;
}
export function draftSnapshot(
  v: readonly ShiftDraft[],
): readonly ShiftDraft[] | undefined {
  if (v.length === 0 || v.length > 500) return;
  if (
    v.some(
      (d) =>
        !d ||
        !id(d.employeeId) ||
        !id(d.branchId) ||
        !id(d.departmentId) ||
        !id(d.templateId) ||
        !calendarDate(d.shiftDate),
    )
  )
    return;
  return Object.freeze(
    v.map((d) =>
      Object.freeze({
        employeeId: d.employeeId,
        branchId: d.branchId,
        departmentId: d.departmentId,
        templateId: d.templateId,
        shiftDate: d.shiftDate,
      }),
    ),
  );
}
function utc(v: unknown): v is string {
  if (
    typeof v !== "string" ||
    !/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|\+00:00)$/.test(v)
  )
    return false;
  const [day] = v.split("T");
  return (
    calendarDate(day) &&
    Number.isFinite(Date.parse(v)) &&
    +v.slice(11, 13) < 24 &&
    +v.slice(14, 16) < 60 &&
    +v.slice(17, 19) < 60
  );
}
export function decodePreview(v: unknown, count: number): Preview | undefined {
  if (
    !v ||
    typeof v !== "object" ||
    !("requiresServerRevalidation" in v) ||
    v.requiresServerRevalidation !== true ||
    !("rows" in v) ||
    !Array.isArray(v.rows) ||
    v.rows.length !== count ||
    count < 1 ||
    count > 500
  )
    return;
  const rows: PreviewRow[] = [];
  for (let i = 0; i < count; i++) {
    const r = v.rows[i];
    if (
      !r ||
      typeof r !== "object" ||
      r.index !== i ||
      !Array.isArray(r.issues) ||
      r.issues.some(
        (x: unknown) =>
          !Number.isInteger(x) ||
          Number(x) < 0 ||
          Number(x) >= issueLabels.length,
      ) ||
      new Set(r.issues).size !== r.issues.length
    )
      return;
    if (r.startUtc === null && r.endUtc === null) {
      if (r.issues.length === 0) return;
    } else if (
      !utc(r.startUtc) ||
      !utc(r.endUtc) ||
      Date.parse(r.endUtc) <= Date.parse(r.startUtc)
    )
      return;
    rows.push(
      Object.freeze({
        index: i,
        startUtc: r.startUtc,
        endUtc: r.endUtc,
        issues: Object.freeze([...r.issues]),
      }),
    );
  }
  return Object.freeze({
    rows: Object.freeze(rows),
    requiresServerRevalidation: true,
  });
}
