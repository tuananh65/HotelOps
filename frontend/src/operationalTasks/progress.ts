export class TaskProgressError extends Error {
  constructor(public readonly status: number) {
    super(`Task progress request failed (${status})`);
  }
}
export interface Criterion {
  id: number;
  label: string;
  required: boolean;
  order: number;
  done: null;
  outcome: "PASS" | "FAIL" | null;
  note: string | null;
}
export interface TaskProgress {
  id: number;
  branchId: number;
  kind: "task";
  state: "IN_PROGRESS" | "PAUSED";
  percent: number;
  items: Criterion[];
}
export interface TaskProgressSnapshot {
  data: TaskProgress;
  etag: string;
}
export interface TaskProgressInput {
  percent: number;
  items: { id: number; outcome: "PASS" | "FAIL"; note: string | null }[];
  note: string | null;
  photos?: { name: string; mimeType: string; base64: string }[];
}
const id = (n: number) => Number.isSafeInteger(n) && n > 0;
const percent = (n: number) => Number.isInteger(n) && n >= 0 && n <= 100;
const tag = (s: string) => /^"[0-9A-F]{16}"$/.test(s);
const text = (s: string | null, max: number) =>
  s === null ||
  (typeof s === "string" &&
    s.trim().length <= max &&
    !/[\u0000-\u001f\u007f-\u009f]/.test(s));
function path(branch: number, task: number) {
  if (!id(branch) || !id(task)) throw new Error("Invalid task identity");
  return `/api/branches/${branch}/operational-tasks/${task}/progress`;
}
async function request(
  url: string,
  signal: AbortSignal,
  init: RequestInit = {},
) {
  const response = await fetch(url, {
    ...init,
    credentials: "same-origin",
    cache: "no-store",
    redirect: "error",
    signal,
  });
  if (!response.ok) throw new TaskProgressError(response.status);
  return response;
}
function validate(data: TaskProgress, branch: number, task: number) {
  if (
    !data ||
    data.id !== task ||
    data.branchId !== branch ||
    data.kind !== "task" ||
    !["IN_PROGRESS", "PAUSED"].includes(data.state) ||
    !percent(data.percent) ||
    !Array.isArray(data.items) ||
    data.items.length > 100 ||
    new Set(data.items.map((i) => i?.id)).size !== data.items.length ||
    data.items.some(
      (i) =>
        !i ||
        !id(i.id) ||
        typeof i.label !== "string" ||
        typeof i.required !== "boolean" ||
        !Number.isInteger(i.order) ||
        i.done !== null ||
        ![null, "PASS", "FAIL"].includes(i.outcome) ||
        !text(i.note, 1000),
    )
  )
    throw new Error("Invalid task progress response");
}
async function snapshot(
  response: Response,
  branch: number,
  task: number,
): Promise<TaskProgressSnapshot> {
  const data = (await response.json()) as TaskProgress;
  validate(data, branch, task);
  const etag = response.headers.get("ETag") ?? "";
  if (!tag(etag)) throw new Error("Invalid task progress version");
  return { data, etag };
}
export async function readTaskProgress(
  branch: number,
  task: number,
  signal: AbortSignal,
) {
  return snapshot(await request(path(branch, task), signal), branch, task);
}
export async function saveTaskProgress(
  branch: number,
  task: number,
  current: TaskProgressSnapshot,
  input: TaskProgressInput,
  signal: AbortSignal,
) {
  const url = path(branch, task);
  validate(current.data, branch, task);
  if (
    !tag(current.etag) ||
    !percent(input.percent) ||
    !text(input.note, 4000) ||
    !Array.isArray(input.items) ||
    input.items.length > 100 ||
    new Set(input.items.map((i) => i.id)).size !== input.items.length ||
    input.items.some(
      (i) =>
        !current.data.items.some((c) => c.id === i.id) ||
        !["PASS", "FAIL"].includes(i.outcome) ||
        !text(i.note, 1000),
    ) ||
    (input.photos?.length ?? 0) > 5
  )
    throw new Error("Invalid task progress input");
  const csrf = await (await request("/api/auth/csrf", signal)).json();
  if (
    csrf?.headerName !== "X-CSRF-TOKEN" ||
    typeof csrf.csrfToken !== "string" ||
    !csrf.csrfToken.trim() ||
    /[\r\n]/.test(csrf.csrfToken)
  )
    throw new Error("Invalid CSRF response");
  signal.throwIfAborted();
  return snapshot(
    await request(url, signal, {
      method: "PUT",
      headers: {
        "Content-Type": "application/json",
        "X-CSRF-TOKEN": csrf.csrfToken,
        "If-Match": current.etag,
      },
      body: JSON.stringify(input),
    }),
    branch,
    task,
  );
}
