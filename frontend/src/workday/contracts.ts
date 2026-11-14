/** Mirrors OwnEligibleShift and OwnWorkingDaySnapshot, not WorkdayResponse. */
export interface OwnShift {
  shiftAssignmentId: number;
  shiftId: number;
  branchId: number;
  departmentId: number;
  shiftDate: string;
  scheduledStartAtUtc: string;
  scheduledEndAtUtc: string;
}
export interface OwnWorkday {
  workdayId: number;
  branchId: number;
  departmentId: number;
  workDate: string;
  shiftAssignmentId: number | null;
  isUnscheduled: boolean;
  startedAtUtc: string;
  rowVersion: string;
  openInterval: {
    intervalId: number;
    workItemKind: string;
    workItemId: number;
    openedAtUtc: string;
    rowVersion: string;
  } | null;
}
export type ReadFailure =
  | "invalid-request"
  | "invalid-response"
  | "unauthorized"
  | "forbidden"
  | "unavailable"
  | "network"
  | "cancelled"
  | "http";
export type ReadResult<T> =
  { ok: true; data: T } | { ok: false; error: ReadFailure };
const object = (v: unknown): v is Record<string, unknown> =>
  typeof v === "object" && v !== null && !Array.isArray(v);
export const safeId = (v: unknown): v is number =>
  typeof v === "number" && Number.isSafeInteger(v) && v > 0;
const date = (v: unknown): v is string =>
  typeof v === "string" &&
  /^\d{4}-\d{2}-\d{2}$/.test(v) &&
  Number.isFinite(Date.parse(v)) &&
  new Date(v).toISOString().slice(0, 10) === v;
const stamp = (v: unknown): v is string =>
  typeof v === "string" &&
  /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})$/.test(
    v,
  ) &&
  date(v.slice(0, 10)) &&
  Number.isFinite(Date.parse(v));
const version = (v: unknown): v is string =>
  typeof v === "string" && /^[A-Za-z0-9+/]{10}[AEIMQUYcgkosw048]=$/.test(v);
export function decodeShifts(
  value: unknown,
  branchId: number,
): OwnShift[] | undefined {
  if (!Array.isArray(value) || value.length > 10000) return;
  const result: OwnShift[] = [];
  const seen = new Set<number>();
  for (const v of value) {
    if (
      !object(v) ||
      !safeId(v.shiftAssignmentId) ||
      !safeId(v.shiftId) ||
      v.branchId !== branchId ||
      !safeId(v.departmentId) ||
      !date(v.shiftDate) ||
      !stamp(v.scheduledStartAtUtc) ||
      !stamp(v.scheduledEndAtUtc) ||
      Date.parse(v.scheduledEndAtUtc) < Date.parse(v.scheduledStartAtUtc) ||
      seen.has(v.shiftAssignmentId)
    )
      return;
    seen.add(v.shiftAssignmentId);
    result.push({
      shiftAssignmentId: v.shiftAssignmentId,
      shiftId: v.shiftId,
      branchId,
      departmentId: v.departmentId,
      shiftDate: v.shiftDate,
      scheduledStartAtUtc: v.scheduledStartAtUtc,
      scheduledEndAtUtc: v.scheduledEndAtUtc,
    });
  }
  return result;
}
export function decodeWorkday(
  v: unknown,
  branchId: number,
): OwnWorkday | undefined {
  if (
    !object(v) ||
    !safeId(v.workdayId) ||
    v.branchId !== branchId ||
    !safeId(v.departmentId) ||
    !date(v.workDate) ||
    !(v.shiftAssignmentId === null || safeId(v.shiftAssignmentId)) ||
    typeof v.isUnscheduled !== "boolean" ||
    !stamp(v.startedAtUtc) ||
    !version(v.rowVersion)
  )
    return;
  let openInterval: OwnWorkday["openInterval"] = null;
  if (v.openInterval !== null) {
    const i = v.openInterval;
    if (
      !object(i) ||
      !safeId(i.intervalId) ||
      !safeId(i.workItemId) ||
      typeof i.workItemKind !== "string" ||
      !["CLEANING", "REWORK", "TASK"].includes(i.workItemKind) ||
      !stamp(i.openedAtUtc) ||
      !version(i.rowVersion)
    )
      return;
    openInterval = {
      intervalId: i.intervalId,
      workItemId: i.workItemId,
      workItemKind: i.workItemKind,
      openedAtUtc: i.openedAtUtc,
      rowVersion: i.rowVersion,
    };
  }
  return {
    workdayId: v.workdayId,
    branchId,
    departmentId: v.departmentId,
    workDate: v.workDate,
    shiftAssignmentId: v.shiftAssignmentId,
    isUnscheduled: v.isUnscheduled,
    startedAtUtc: v.startedAtUtc,
    rowVersion: v.rowVersion,
    openInterval,
  };
}
