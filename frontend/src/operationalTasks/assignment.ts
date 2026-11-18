import { TaskProgressError } from "./progress";

export interface AssignmentSnapshot {
  etag: string;
  data: {
    taskId: number;
    branchId: number;
    state: string;
    assigneeId: number | null;
    asOf: string;
    timezone: string;
    totalCount: number;
    page: number;
    pageSize: number;
    executors: {
      userId: number;
      name: string;
      openTasks: number;
      loggedSecondsToday: number;
    }[];
  };
}
const positive = (n: number) => Number.isSafeInteger(n) && n > 0;
const nonnegative = (n: number) => Number.isSafeInteger(n) && n >= 0;
const tag = (s: string) => /^"[0-9A-F]{16}"$/.test(s);
const states = [
  "PENDING_ASSIGNMENT",
  "ASSIGNED",
  "ACCEPTED",
  "IN_PROGRESS",
  "PAUSED",
];
function route(branch: number, task: number) {
  if (!positive(branch) || !positive(task))
    throw new Error("Định danh công việc không hợp lệ");
  return `/api/branches/${branch}/operational-tasks/${task}/assignment`;
}
async function request(
  url: string,
  signal: AbortSignal,
  init: RequestInit = {},
) {
  const response = await fetch(url, {
    ...init,
    signal,
    credentials: "same-origin",
    cache: "no-store",
    redirect: "error",
  });
  if (!response.ok) throw new TaskProgressError(response.status);
  return response;
}
export async function readAssignment(
  branch: number,
  task: number,
  page: number,
  search: string,
  signal: AbortSignal,
): Promise<AssignmentSnapshot> {
  if (!positive(page) || search.length > 150)
    throw new Error("Bộ lọc người thực hiện không hợp lệ");
  const response = await request(
    route(branch, task) + `?page=${page}&search=${encodeURIComponent(search)}`,
    signal,
  );
  const data = (await response.json()) as AssignmentSnapshot["data"];
  const etag = response.headers.get("ETag") ?? "";
  if (
    !data ||
    data.taskId !== task ||
    data.branchId !== branch ||
    !tag(etag) ||
    !states.includes(data.state) ||
    (data.assigneeId !== null && !positive(data.assigneeId)) ||
    (data.state === "PENDING_ASSIGNMENT") !== (data.assigneeId === null) ||
    !Number.isFinite(Date.parse(data.asOf)) ||
    data.timezone !== "Asia/Ho_Chi_Minh" ||
    data.page !== page ||
    data.pageSize !== 25 ||
    !nonnegative(data.totalCount) ||
    !Array.isArray(data.executors) ||
    data.executors.length > 25 ||
    data.executors.length > data.totalCount ||
    new Set(data.executors.map((e) => e.userId)).size !==
      data.executors.length ||
    data.executors.some(
      (e) =>
        !e ||
        !positive(e.userId) ||
        typeof e.name !== "string" ||
        !nonnegative(e.openTasks) ||
        !nonnegative(e.loggedSecondsToday),
    )
  )
    throw new Error("Bản chụp dữ liệu phân công không hợp lệ");
  return { data, etag };
}
export async function assignTask(
  branch: number,
  task: number,
  snapshot: AssignmentSnapshot,
  assigneeId: number,
  reason: string,
  signal: AbortSignal,
) {
  const url = route(branch, task);
  if (
    snapshot.data.taskId !== task ||
    snapshot.data.branchId !== branch ||
    !tag(snapshot.etag) ||
    !states.includes(snapshot.data.state) ||
    !positive(assigneeId) ||
    !snapshot.data.executors.some((e) => e.userId === assigneeId) ||
    assigneeId === snapshot.data.assigneeId ||
    (snapshot.data.assigneeId !== null && !reason.trim()) ||
    reason.length > 4000 ||
    /[\u0000-\u0008\u000b\u000c\u000e-\u001f\u007f-\u009f]/.test(reason)
  )
    throw new Error("Phân công không hợp lệ");
  const csrf = await (await request("/api/auth/csrf", signal)).json();
  if (
    csrf?.headerName !== "X-CSRF-TOKEN" ||
    typeof csrf.csrfToken !== "string" ||
    !csrf.csrfToken.trim() ||
    /[\r\n]/.test(csrf.csrfToken)
  )
    throw new Error("Phản hồi CSRF không hợp lệ");
  signal.throwIfAborted();
  const response = await request(url, signal, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-CSRF-TOKEN": csrf.csrfToken,
      "If-Match": snapshot.etag,
    },
    body: JSON.stringify({ assigneeId, reason: reason.trim() || null }),
  });
  const body = await response.json();
  const etag = response.headers.get("ETag") ?? "";
  if (
    body?.taskId !== task ||
    body?.branchId !== branch ||
    body?.assigneeId !== assigneeId ||
    body?.state !== "ASSIGNED" ||
    body?.messageCode !== "MSG08" ||
    !tag(etag) ||
    etag === snapshot.etag
  )
    throw new Error("Chưa xác nhận kết quả phân công");
  return body;
}
