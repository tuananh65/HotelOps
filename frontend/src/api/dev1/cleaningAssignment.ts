import { ApiError, send } from "./client";
import { positiveId } from "./rooms";
export interface AssignmentPlan {
  assignmentCapability?: string;
  sourceETag?: string;
  task: {
    id: number;
    number: string;
    roomId: number;
    roomNumber: string;
    branchId: number;
    recordedStatus: string;
    dueAt: string | null;
    workflowState: string | null;
  };
  candidates: {
    id: number;
    name: string;
    departmentId: number | null;
    openTasks: number;
    openTasksInWindow: number;
  }[];
  recordedShifts: {
    id: number;
    userId: number;
    from: string;
    to: string;
    recordedShiftStatus: string;
    recordedAssignmentStatus: string;
  }[];
  total: number;
  filters: {
    search: string | null;
    from: string;
    to: string;
    page: number;
    pageSize: number;
  };
  checklist: null;
  capturedAt: string;
  workflowStateVerified: boolean;
}
// Existing backend DTO only. No POST function until mandatory notification/lifecycle contracts are approved.
export interface AssignmentDraft {
  assigneeId: number;
  dueAt: string;
  reason: string;
  adoptUnassignedLegacy: false;
}
const object = (v: unknown): v is Record<string, unknown> =>
  !!v && typeof v === "object" && !Array.isArray(v);
const integer = (v: unknown, min = 1): v is number =>
  typeof v === "number" && Number.isSafeInteger(v) && v >= min;
const text = (v: unknown): v is string => typeof v === "string";
const stamp = (v: unknown): v is string =>
  text(v) && /Z$/.test(v) && Number.isFinite(Date.parse(v));
// EF DateTime fields in this legacy DTO may omit the zone. Preserve/display the wire text;
// never silently interpret those fields as a local deadline for a future write.
const recordedStamp = (v: unknown): v is string =>
  text(v) &&
  /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})?$/.test(
    v,
  ) &&
  Number.isFinite(Date.parse(v));
function invalid(): never {
  throw new ApiError(502, "INVALID_ASSIGNMENT_PLAN");
}
export async function readAssignmentPlan(
  branch: string,
  task: string,
  page = 1,
  search = "",
  signal?: AbortSignal,
): Promise<AssignmentPlan> {
  const b = positiveId(branch),
    id = positiveId(task);
  if (
    !integer(page) ||
    page > Math.floor(2147483647 / 20) ||
    search.trim().length > 150 ||
    /[\u0000-\u001f\u007f-\u009f]/.test(search)
  )
    throw Error("Invalid planning filters");
  const q = new URLSearchParams({ page: String(page), pageSize: "20" });
  if (search.trim()) q.set("search", search.trim());
  const response = await send(
    `/branches/${b}/cleaning-tasks/${id}/assignment-options?${q}`,
    signal,
    { method: "GET", headers: { Accept: "application/json" } },
  );
  const v = response.body;
  if (
    !object(v) ||
    !object(v.task) ||
    v.task.id !== id ||
    v.task.branchId !== b ||
    !integer(v.task.roomId) ||
    ![v.task.number, v.task.roomNumber, v.task.recordedStatus].every(text) ||
    !(v.task.dueAt === null || recordedStamp(v.task.dueAt)) ||
    !(v.task.workflowState === null || text(v.task.workflowState)) ||
    !integer(v.total, 0) ||
    !Array.isArray(v.candidates) ||
    v.candidates.length > 20 ||
    v.candidates.length > v.total ||
    !Array.isArray(v.recordedShifts) ||
    v.checklist !== null ||
    !stamp(v.capturedAt) ||
    typeof v.workflowStateVerified !== "boolean" ||
    !object(v.filters) ||
    v.filters.page !== page ||
    v.filters.pageSize !== 20 ||
    !stamp(v.filters.from) ||
    !stamp(v.filters.to) ||
    !(v.filters.search === null || text(v.filters.search))
  )
    invalid();
  const ids = new Set<number>();
  for (const c of v.candidates) {
    if (
      !object(c) ||
      !integer(c.id) ||
      ids.has(c.id) ||
      !text(c.name) ||
      !(c.departmentId === null || integer(c.departmentId)) ||
      !integer(c.openTasks, 0) ||
      !integer(c.openTasksInWindow, 0)
    )
      invalid();
    ids.add(c.id);
  }
  for (const s of v.recordedShifts) {
    if (
      !object(s) ||
      !integer(s.id) ||
      !integer(s.userId) ||
      !ids.has(s.userId) ||
      !recordedStamp(s.from) ||
      !recordedStamp(s.to) ||
      Date.parse(s.from) >= Date.parse(s.to) ||
      ![s.recordedShiftStatus, s.recordedAssignmentStatus].every(text)
    )
      invalid();
  }
  if (
    v.assignmentCapability === "PROVISIONAL_NOT_ACCEPTED" &&
    !/^"[0-9A-F]{16}"$/.test(response.etag)
  )
    invalid();
  return { ...v, sourceETag: response.etag } as unknown as AssignmentPlan;
}
export function buildAssignmentDraft(
  plan: AssignmentPlan,
  assignee: string,
  due: string,
  reason: string,
  now = Date.now(),
): AssignmentDraft {
  const assigneeId = positiveId(assignee);
  const note = reason.trim();
  if (
    !plan.workflowStateVerified ||
    plan.task.workflowState !== "PENDING_ASSIGNMENT"
  )
    throw Error(
      "Chỉ chuẩn bị phân công lần đầu khi máy chủ xác nhận PENDING_ASSIGNMENT.",
    );
  if (!plan.candidates.some((c) => c.id === assigneeId))
    throw Error("Chọn nhân viên trong trang dữ liệu máy chủ hiện tại.");
  if (!stamp(due) || Date.parse(due) <= now)
    throw Error(
      "Hạn phải là thời điểm UTC tương lai (ISO 8601 kết thúc bằng Z).",
    );
  if (!note || note.length > 500 || /[\u0000-\u001f\u007f-\u009f]/.test(note))
    throw Error(
      "Lý do bắt buộc, tối đa 500 ký tự, không chứa ký tự điều khiển.",
    );
  return {
    assigneeId,
    dueAt: new Date(due).toISOString(),
    reason: note,
    adoptUnassignedLegacy: false,
  };
}
export function assignmentError(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 401)
      return "Phiên đăng nhập không hợp lệ hoặc đã hết hạn.";
    if (error.status === 403)
      return "Không có quyền CLEANING_ASSIGN tại chi nhánh này.";
    if (error.status === 404)
      return "Không tìm thấy công việc trong chi nhánh được phép.";
    if (error.status === 409)
      return "Phòng hoặc phạm vi công việc không hoạt động.";
    if (error.status === 400)
      return "Bộ lọc không hợp lệ hoặc yêu cầu chưa dùng HTTPS.";
    if (error.status === 503) return "Schema hoặc dịch vụ chưa sẵn sàng.";
    if (error.status === 502) return "Phản hồi phân công từ API không hợp lệ.";
  }
  return "Không tải được dữ liệu phân công. Kiểm tra chi nhánh, mã công việc và kết nối.";
}
