import { decodeWorkday, safeId } from "./contracts";
export interface ScheduledWorkdayResponse {
  workdayId: number;
  branchId: number;
  departmentId: number;
  workDate: string;
  shiftAssignmentId: number | null;
  isUnscheduled: boolean;
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
        code?: string;
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
    assignmentId: number | null;
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
    x.isUnscheduled !== (expected.assignmentId === null) ||
    x.shiftAssignmentId !== expected.assignmentId ||
    (x.shiftAssignmentId !== null && !safeId(x.shiftAssignmentId)) ||
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
    shiftAssignmentId: day.shiftAssignmentId,
    isUnscheduled: day.isUnscheduled,
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
    return "Chưa xác định kết quả. Không gửi lại. Nhờ người có trách nhiệm xác minh trước khi mở quy trình mới.";
  if (r.outcome === "REJECTED" && r.error.status === 409) {
    const blockers: Record<string, string> = {
      PAUSE_ALL_CLEANING_RECONCILIATION_REQUIRED: "công việc dọn phòng",
      PAUSE_ALL_TASK_RECONCILIATION_REQUIRED: "công việc được giao",
      PAUSE_ALL_MAINTENANCE_RECONCILIATION_REQUIRED: "công việc bảo trì",
      PAUSE_ALL_REWORK_RECONCILIATION_REQUIRED: "công việc làm lại phòng",
      RUNNING_OCCURRENCE_REQUIRES_COMPLETION: "công việc bảo trì định kỳ",
    };
    const work = blockers[r.error.code ?? ""];
    if (work)
      return `Chưa thể kết thúc ca vì bạn còn ${work} đang thực hiện. Hãy hoàn tất hoặc tạm dừng công việc theo quy trình, sau đó tải lại dữ liệu và kết thúc ca. Nếu công việc cũ không thao tác được, hãy nhờ quản lý kiểm tra. Ca của bạn vẫn đang mở.`;
    if (r.error.code === "MSG31_PAUSE_ALL_REQUIRED")
      return "Bạn còn công việc đang chạy. Tải lại dữ liệu, kiểm tra và xác nhận tạm dừng công việc trước khi kết thúc ca.";
  }
  if (r.error.reason === "BUSY")
    return "Một yêu cầu ngày làm việc khác đang chờ. Vui lòng chờ kết quả.";
  if (r.error.reason === "DISABLED")
    return "Thao tác ghi theo ca bị khóa, chờ phê duyệt vận hành.";
  switch (r.error.status) {
    case 401:
      return "Phiên đăng nhập không hợp lệ. Vui lòng xác minh lại phiên.";
    case 403:
      return "Máy chủ từ chối thao tác này.";
    case 409:
      return "MSG03 / MSG31: Trạng thái ngày làm việc đã đổi hoặc còn công việc đang chạy. Tải lại, kiểm tra việc đang mở và dùng Tạm dừng tất cả nếu có.";
    case 412:
      return "Phiên bản ngày làm việc đã đổi. Tải lại và xem xét; không gửi lại yêu cầu cũ.";
    case 503:
      return "Dịch vụ chưa khả dụng. Không tự gửi lại.";
    default:
      return "Chưa xác nhận thao tác. Tải lại và kiểm tra trước khi thử tiếp.";
  }
}
