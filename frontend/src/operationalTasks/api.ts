export const taskWorkflowStates = [
  "PENDING_ASSIGNMENT",
  "ASSIGNED",
  "ACCEPTED",
  "IN_PROGRESS",
  "PAUSED",
  "PENDING_VERIFICATION",
  "CLOSED",
  "CANCELLED",
] as const;
export type TaskWorkflowState = (typeof taskWorkflowStates)[number];
export interface TaskRow {
  id: number;
  code: string;
  branchId: number;
  departmentId: number;
  title: string;
  status: string;
  workflowStatus?: TaskWorkflowState | null;
  progressPercent: number;
  dueAt: string | null;
}
export interface TaskPage {
  items: TaskRow[];
  totalCount: number;
  page: number;
  pageSize: number;
}
export interface TaskDetail {
  task: TaskRow;
  description: string | null;
  completionNote: string | null;
  priority?: string | null;
  progressTotalCount: number;
  progress: {
    id: number;
    progressPercent: number;
    fromStatus: string | null;
    toStatus: string | null;
    description: string | null;
    blockerReason: string | null;
    reportedBy: number;
    reportedAt: string;
  }[];
}
export class TaskReadFailure extends Error {
  constructor(readonly status: number) {
    super("Yêu cầu công việc thất bại");
  }
}
const positive = (x: unknown): x is number =>
  typeof x === "number" && Number.isSafeInteger(x) && x > 0;
const count = (x: unknown): x is number =>
  typeof x === "number" && Number.isSafeInteger(x) && x >= 0;
const text = (x: unknown) => x === null || typeof x === "string";
function row(x: TaskRow, branch: number) {
  return (
    x &&
    positive(x.id) &&
    x.branchId === branch &&
    positive(x.departmentId) &&
    [x.code, x.title, x.status].every((v) => typeof v === "string") &&
    count(x.progressPercent) &&
    x.progressPercent <= 100 &&
    text(x.dueAt) &&
    (x.workflowStatus == null || taskWorkflowStates.includes(x.workflowStatus))
  );
}
export async function readTasks(
  branch: number,
  page: number,
  search: string,
  id: number | null,
  signal: AbortSignal,
  workflowStatus?: TaskWorkflowState,
): Promise<TaskPage | TaskDetail> {
  if (!positive(branch) || !positive(page) || (id !== null && !positive(id)))
    throw new Error("Mã công việc không hợp lệ");
  if (
    workflowStatus !== undefined &&
    !taskWorkflowStates.includes(workflowStatus)
  )
    throw new Error("Bộ lọc quy trình không hợp lệ");
  const query = new URLSearchParams({
    page: String(page),
    pageSize: "25",
    ...(id === null ? { search } : {}),
    ...(id === null && workflowStatus ? { workflowStatus } : {}),
  });
  const response = await fetch(
    `/api/branches/${branch}/operational-tasks${id === null ? "" : `/${id}`}?${query}`,
    {
      credentials: "same-origin",
      cache: "no-store",
      redirect: "error",
      signal,
    },
  );
  if (!response.ok) throw new TaskReadFailure(response.status);
  const data = await response.json();
  if (id === null) {
    if (
      !data ||
      !Array.isArray(data.items) ||
      !data.items.every((v: TaskRow) => row(v, branch)) ||
      !count(data.totalCount) ||
      data.page !== page ||
      data.pageSize !== 25
    )
      throw new Error("Trang công việc không hợp lệ");
  } else if (
    !data ||
    !row(data.task, branch) ||
    data.task.id !== id ||
    !text(data.description) ||
    !text(data.completionNote) ||
    (data.priority !== undefined && !text(data.priority)) ||
    !count(data.progressTotalCount) ||
    !Array.isArray(data.progress) ||
    !data.progress.every(
      (p: TaskDetail["progress"][number]) =>
        p &&
        positive(p.id) &&
        count(p.progressPercent) &&
        p.progressPercent <= 100 &&
        positive(p.reportedBy) &&
        typeof p.reportedAt === "string" &&
        [p.fromStatus, p.toStatus, p.description, p.blockerReason].every(text),
    )
  )
    throw new Error("Chi tiết công việc không hợp lệ");
  return data;
}
