import { taskWorkflowStates } from "./api";
import { planningRequest, planningMutation } from "./planning";

export interface ReopenSnapshot {
  etag: string;
  data: {
    taskId: number;
    branchId: number;
    state: string;
    canReopen: boolean;
    previousAssigneeId: number | null;
    executors: {
      userId: number;
      name: string;
      openTasks: number;
      loggedSecondsToday: number;
    }[];
    totalCount: number;
    page: number;
    pageSize: number;
    history: {
      previousSubmissionId: number;
      assignmentId: number;
      assigneeId: number;
      reopenedBy: number;
      reopenedAt: string;
      reason: string;
      instructions: string;
    }[];
  };
}
const positive = (n: number) => Number.isSafeInteger(n) && n > 0;
const nonnegative = (n: number) => Number.isSafeInteger(n) && n >= 0;
const tag = (s: string) => /^"[0-9A-F]{16}"$/.test(s);
const validText = (s: string, min = 1) =>
  typeof s === "string" &&
  s.trim().length >= min &&
  s.length <= 4000 &&
  !/[\u0000-\u0008\u000b\u000c\u000e-\u001f\u007f-\u009f]/.test(s);
function route(branch: number, task: number) {
  if (!positive(branch) || !positive(task))
    throw new Error("Invalid task identity");
  return `/api/branches/${branch}/operational-tasks/${task}/reopen`;
}
export async function readReopen(
  branch: number,
  task: number,
  page: number,
  search: string,
  signal: AbortSignal,
): Promise<ReopenSnapshot> {
  if (
    !positive(page) ||
    search.length > 150 ||
    /[\u0000-\u001f\u007f-\u009f]/.test(search)
  )
    throw new Error("Invalid executor filters");
  const response = await planningRequest(
    route(branch, task) + `?page=${page}&search=${encodeURIComponent(search)}`,
    signal,
  );
  const data = (await response.json()) as ReopenSnapshot["data"];
  const etag = response.headers.get("ETag") ?? "";
  if (
    !data ||
    data.taskId !== task ||
    data.branchId !== branch ||
    !tag(etag) ||
    !(taskWorkflowStates as readonly string[]).includes(data.state) ||
    typeof data.canReopen !== "boolean" ||
    (data.previousAssigneeId !== null && !positive(data.previousAssigneeId)) ||
    (data.canReopen &&
      (data.state !== "CLOSED" || data.previousAssigneeId === null)) ||
    data.page !== page ||
    data.pageSize !== 25 ||
    !nonnegative(data.totalCount) ||
    !Array.isArray(data.executors) ||
    data.executors.length > 25 ||
    data.executors.length > data.totalCount ||
    (!data.canReopen &&
      (data.executors.length !== 0 || data.totalCount !== 0)) ||
    new Set(data.executors.map((e) => e.userId)).size !==
      data.executors.length ||
    data.executors.some(
      (e) =>
        !e ||
        !positive(e.userId) ||
        typeof e.name !== "string" ||
        !nonnegative(e.openTasks) ||
        !nonnegative(e.loggedSecondsToday),
    ) ||
    !Array.isArray(data.history) ||
    new Set(data.history.map((e) => e.previousSubmissionId)).size !==
      data.history.length ||
    data.history.some(
      (e) =>
        !e ||
        !positive(e.previousSubmissionId) ||
        !positive(e.assignmentId) ||
        !positive(e.assigneeId) ||
        !positive(e.reopenedBy) ||
        typeof e.reopenedAt !== "string" ||
        !Number.isFinite(Date.parse(e.reopenedAt)) ||
        !validText(e.reason, 10) ||
        !validText(e.instructions),
    )
  )
    throw new Error("Invalid reopen snapshot");
  return { data, etag };
}
export async function reopenTask(
  branch: number,
  task: number,
  snapshot: ReopenSnapshot,
  assigneeId: number,
  reason: string,
  instructions: string,
  signal: AbortSignal,
) {
  const url = route(branch, task);
  if (
    snapshot.data.taskId !== task ||
    snapshot.data.branchId !== branch ||
    !tag(snapshot.etag) ||
    snapshot.data.state !== "CLOSED" ||
    !snapshot.data.canReopen ||
    !positive(assigneeId) ||
    !snapshot.data.executors.some((e) => e.userId === assigneeId) ||
    !validText(reason, 10) ||
    !validText(instructions)
  )
    throw new Error("Invalid reopen request");
  const response = await planningMutation(
    url,
    "POST",
    { assigneeId, reason: reason.trim(), instructions: instructions.trim() },
    signal,
    snapshot.etag,
  );
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
    throw new Error("Reopen outcome unconfirmed");
  return body;
}
