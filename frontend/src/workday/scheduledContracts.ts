import { decodeWorkday, safeId } from "./contracts";
export interface ScheduledWorkdayResponse {
  workdayId: number;
  branchId: number;
  departmentId: number;
  workDate: string;
  shiftAssignmentId: number;
  isUnscheduled: false;
  status: "WORKING" | "ENDED";
  startedAtUtc: string;
  endedAtUtc: string | null;
  rowVersion: string;
}
export type ScheduledResult =
  | { outcome: "CONFIRMED"; data: ScheduledWorkdayResponse }
  | {
      outcome: "NOT_DISPATCHED" | "REJECTED" | "OUTCOME_UNKNOWN";
      error: {
        reason:
          | "DISABLED"
          | "BUSY"
          | "INVALID_REQUEST"
          | "STALE"
          | "CSRF"
          | "NETWORK"
          | "CANCELLED"
          | "INVALID_RESPONSE"
          | "HTTP";
        status?: number;
      };
    };
export const strongWorkdayTag = (v: unknown): v is string =>
  typeof v === "string" && /^"[A-Za-z0-9+/]{10}[AEIMQUYcgkosw048]="$/.test(v);
// Date.parse normalizes invalid dates and accepts 24:00. Do not confirm those
// values as DateTimeOffset responses from the server.
function validEndTimestamp(value: unknown): value is string {
  if (typeof value !== "string") return false;
  const match =
    /^(\d{4}-\d{2}-\d{2})T(?:[01]\d|2[0-3]):[0-5]\d:[0-5]\d(?:\.\d{1,7})?(?:Z|[+-](0\d|1[0-4]):([0-5]\d))$/.exec(
      value,
    );
  if (!match || match[1].startsWith("0000")) return false;
  const date = new Date(match[1]);
  return (
    Number.isFinite(date.getTime()) &&
    date.toISOString().slice(0, 10) === match[1] &&
    !(match[2] === "14" && match[3] !== "00") &&
    Number.isFinite(Date.parse(value))
  );
}
export function decodeScheduledResponse(
  v: unknown,
  branch: number,
  expected: {
    assignmentId: number;
    workdayId?: number;
    status: "WORKING" | "ENDED";
  },
  etag: unknown,
): ScheduledWorkdayResponse | undefined {
  if (!v || typeof v !== "object" || Array.isArray(v)) return;
  const x = v as Record<string, unknown>;
  const day = decodeWorkday({ ...x, openInterval: null }, branch);
  if (
    !day ||
    x.isUnscheduled !== false ||
    x.shiftAssignmentId !== expected.assignmentId ||
    !safeId(x.shiftAssignmentId) ||
    x.status !== expected.status ||
    (expected.workdayId !== undefined && x.workdayId !== expected.workdayId) ||
    !strongWorkdayTag(etag) ||
    etag !== `"${day.rowVersion}"`
  )
    return;
  if (
    expected.status === "WORKING"
      ? x.endedAtUtc !== null
      : !validEndTimestamp(x.endedAtUtc) ||
        Date.parse(x.endedAtUtc) < Date.parse(day.startedAtUtc)
  )
    return;
  return {
    workdayId: day.workdayId,
    branchId: branch,
    departmentId: day.departmentId,
    workDate: day.workDate,
    shiftAssignmentId: x.shiftAssignmentId,
    isUnscheduled: false,
    status: expected.status,
    startedAtUtc: day.startedAtUtc,
    endedAtUtc: x.endedAtUtc as string | null,
    rowVersion: day.rowVersion,
  };
}
export function scheduledMessage(
  r: Exclude<ScheduledResult, { outcome: "CONFIRMED" }>,
): string {
  if (r.outcome === "OUTCOME_UNKNOWN")
    return "Outcome unknown. Do not replay. Verify with an authorized owner before starting a new flow.";
  if (r.error.reason === "BUSY")
    return "Another workday request is pending. Wait for its result.";
  if (r.error.reason === "DISABLED")
    return "Scheduled mutation dispatch is disabled pending runtime approval.";
  switch (r.error.status) {
    case 401:
      return "Session invalid. Revalidate session.";
    case 403:
      return "Server denied this operation.";
    case 409:
      return "Workday conflict or open interval. Refresh and review; Pause All is not supported.";
    case 412:
      return "Workday version changed. Refresh and review; no replay.";
    case 503:
      return "Service unavailable. No automatic retry.";
    default:
      return "Operation not confirmed. Refresh and review before a new attempt.";
  }
}
