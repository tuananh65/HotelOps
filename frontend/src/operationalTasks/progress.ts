export class TaskProgressError extends Error {
  constructor(
    public readonly status: number,
    public readonly messageCode?: string,
    public readonly missingCriteria: string[] = [],
    public readonly problemCode?: string,
  ) {
    super(`Yêu cầu tiến độ công việc thất bại (${status})`);
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
  state:
    "ASSIGNED" | "ACCEPTED" | "IN_PROGRESS" | "PAUSED" | "PENDING_VERIFICATION";
  percent: number;
  items: Criterion[];
  evidence: TaskEvidence[];
}
export interface TaskEvidence {
  id: number;
  entityType: "TASK";
  entityId: number;
  name: string;
  mimeType: string | null;
  bytes: number | null;
  uploadedAt: string;
  downloadPath: string;
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
  if (!id(branch) || !id(task))
    throw new Error("Định danh công việc không hợp lệ");
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
  if (!response.ok) {
    const problem = await response.json().catch(() => null);
    throw new TaskProgressError(
      response.status,
      ["MSG03", "MSG23", "MSG30", "MSG31"].includes(problem?.messageCode)
        ? problem.messageCode
        : undefined,
      Array.isArray(problem?.missingCriteria) &&
        problem.missingCriteria.length <= 100 &&
        problem.missingCriteria.every(
          (s: unknown) => typeof s === "string" && s.length <= 250,
        )
        ? problem.missingCriteria
        : [],
      typeof problem?.title === "string" ? problem.title : undefined,
    );
  }
  return response;
}
function validate(data: TaskProgress, branch: number, task: number) {
  if (
    !data ||
    data.id !== task ||
    data.branchId !== branch ||
    data.kind !== "task" ||
    ![
      "ASSIGNED",
      "ACCEPTED",
      "IN_PROGRESS",
      "PAUSED",
      "PENDING_VERIFICATION",
    ].includes(data.state) ||
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
    ) ||
    !Array.isArray(data.evidence) ||
    data.evidence.length > 100 ||
    new Set(data.evidence.map((e) => e?.id)).size !== data.evidence.length ||
    data.evidence.some(
      (e) =>
        !e ||
        !id(e.id) ||
        e.entityType !== "TASK" ||
        e.entityId !== task ||
        typeof e.name !== "string" ||
        !e.name.trim() ||
        !text(e.name, 200) ||
        (e.mimeType !== null &&
          !["image/png", "image/jpeg", "application/pdf"].includes(
            e.mimeType,
          )) ||
        (e.bytes !== null && (!Number.isSafeInteger(e.bytes) || e.bytes < 1)) ||
        typeof e.uploadedAt !== "string" ||
        !Number.isFinite(Date.parse(e.uploadedAt)) ||
        e.downloadPath !== `${path(branch, task)}/evidence/${e.id}`,
    )
  )
    throw new Error("Phản hồi tiến độ công việc không hợp lệ");
}
async function snapshot(
  response: Response,
  branch: number,
  task: number,
): Promise<TaskProgressSnapshot> {
  const data = (await response.json()) as TaskProgress;
  validate(data, branch, task);
  const etag = response.headers.get("ETag") ?? "";
  if (!tag(etag)) throw new Error("Phiên bản tiến độ công việc không hợp lệ");
  return { data, etag };
}
export async function readTaskProgress(
  branch: number,
  task: number,
  signal: AbortSignal,
) {
  return snapshot(await request(path(branch, task), signal), branch, task);
}
export async function resumeTask(
  branch: number,
  task: number,
  current: TaskProgressSnapshot,
  signal: AbortSignal,
) {
  validate(current.data, branch, task);
  if (!tag(current.etag) || current.data.state !== "PAUSED")
    throw new Error("Chỉ công việc đang tạm dừng mới được tiếp tục");
  const csrf = await (await request("/api/auth/csrf", signal)).json();
  if (
    csrf?.headerName !== "X-CSRF-TOKEN" ||
    typeof csrf.csrfToken !== "string" ||
    !csrf.csrfToken.trim() ||
    /[\r\n]/.test(csrf.csrfToken)
  )
    throw new Error("Phản hồi CSRF không hợp lệ");
  signal.throwIfAborted();
  const result = await snapshot(
    await request(
      path(branch, task).replace(/\/progress$/, "/resume"),
      signal,
      {
        method: "POST",
        headers: { "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": current.etag },
      },
    ),
    branch,
    task,
  );
  if (result.data.state !== "IN_PROGRESS" || result.etag === current.etag)
    throw new Error("Chưa xác nhận tiếp tục công việc");
  return result;
}
export async function enterTask(
  branch: number,
  task: number,
  current: TaskProgressSnapshot,
  action: "accept" | "start",
  signal: AbortSignal,
) {
  validate(current.data, branch, task);
  if (
    !["accept", "start"].includes(action) ||
    !tag(current.etag) ||
    current.data.state !== (action === "accept" ? "ASSIGNED" : "ACCEPTED")
  )
    throw new Error("Chuyển trạng thái bắt đầu công việc không hợp lệ");
  const csrf = await (await request("/api/auth/csrf", signal)).json();
  if (
    csrf?.headerName !== "X-CSRF-TOKEN" ||
    typeof csrf.csrfToken !== "string" ||
    !csrf.csrfToken.trim() ||
    /[\r\n]/.test(csrf.csrfToken)
  )
    throw new Error("Phản hồi CSRF không hợp lệ");
  signal.throwIfAborted();
  const result = await snapshot(
    await request(
      path(branch, task).replace(/\/progress$/, `/${action}`),
      signal,
      {
        method: "POST",
        headers: { "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": current.etag },
      },
    ),
    branch,
    task,
  );
  if (
    result.data.state !== (action === "accept" ? "ACCEPTED" : "IN_PROGRESS") ||
    result.etag === current.etag
  )
    throw new Error("Chưa xác nhận bắt đầu công việc");
  return result;
}
export async function declineTask(
  branch: number,
  task: number,
  current: TaskProgressSnapshot,
  reason: string,
  signal: AbortSignal,
) {
  validate(current.data, branch, task);
  if (
    !tag(current.etag) ||
    current.data.state !== "ASSIGNED" ||
    !reason.trim() ||
    reason.length > 4000 ||
    /[\u0000-\u0008\u000b\u000c\u000e-\u001f\u007f-\u009f]/.test(reason)
  )
    throw new Error("Cần công việc đã được giao và lý do từ chối hợp lệ");
  const csrf = await (await request("/api/auth/csrf", signal)).json();
  if (
    csrf?.headerName !== "X-CSRF-TOKEN" ||
    typeof csrf.csrfToken !== "string" ||
    !csrf.csrfToken.trim() ||
    /[\r\n]/.test(csrf.csrfToken)
  )
    throw new Error("Phản hồi CSRF không hợp lệ");
  signal.throwIfAborted();
  const response = await request(
    path(branch, task).replace(/\/progress$/, "/decline"),
    signal,
    {
      method: "POST",
      headers: {
        "X-CSRF-TOKEN": csrf.csrfToken,
        "If-Match": current.etag,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ reason: reason.trim() }),
    },
  );
  const result = await response.json();
  const etag = response.headers.get("etag");
  if (
    !etag ||
    !tag(etag) ||
    etag === current.etag ||
    result?.taskId !== task ||
    result.branchId !== branch ||
    result.state !== "PENDING_ASSIGNMENT" ||
    result.messageCode !== "MSG19"
  )
    throw new Error("Chưa xác nhận từ chối công việc");
  return result;
}

export async function pauseTask(
  branch: number,
  task: number,
  current: TaskProgressSnapshot,
  signal: AbortSignal,
  reasonId: number,
  note: string,
) {
  validate(current.data, branch, task);
  if (
    !tag(current.etag) ||
    current.data.state !== "IN_PROGRESS" ||
    !Number.isSafeInteger(reasonId) ||
    reasonId <= 0 ||
    note.length > 1000
  )
    throw new Error("Cần công việc đang thực hiện và lý do tạm dừng hợp lệ");
  const csrf = await (await request("/api/auth/csrf", signal)).json();
  if (
    csrf?.headerName !== "X-CSRF-TOKEN" ||
    typeof csrf.csrfToken !== "string" ||
    !csrf.csrfToken.trim() ||
    /[\r\n]/.test(csrf.csrfToken)
  )
    throw new Error("Phản hồi CSRF không hợp lệ");
  signal.throwIfAborted();
  const result = await snapshot(
    await request(path(branch, task).replace(/\/progress$/, "/pause"), signal, {
      method: "POST",
      headers: {
        "X-CSRF-TOKEN": csrf.csrfToken,
        "If-Match": current.etag,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ reasonId, note }),
    }),
    branch,
    task,
  );
  if (result.data.state !== "PAUSED" || result.etag === current.etag)
    throw new Error("Chưa xác nhận tạm dừng công việc");
  return result;
}
export type PauseReason = { id: number; code: string; label: string };
export async function readPauseReasons(
  branch: number,
  task: number,
  signal: AbortSignal,
): Promise<PauseReason[]> {
  const data = await (
    await request(
      path(branch, task).replace(/\/progress$/, "/pause-reasons"),
      signal,
    )
  ).json();
  if (
    !Array.isArray(data) ||
    data.some(
      (r) =>
        !Number.isSafeInteger(r.id) ||
        r.id <= 0 ||
        typeof r.code !== "string" ||
        typeof r.label !== "string" ||
        !r.label.trim(),
    )
  )
    throw new Error("Lý do tạm dừng không hợp lệ");
  return data;
}
export async function initializeTaskProgress(
  branch: number,
  task: number,
  items: { label: string; required: boolean }[],
  reason: string,
  signal: AbortSignal,
) {
  const url = path(branch, task).replace(
    /\/progress$/,
    "/progress-initialization",
  );
  if (
    !Array.isArray(items) ||
    items.length < 1 ||
    items.length > 100 ||
    items.some(
      (i) =>
        !i ||
        typeof i.label !== "string" ||
        !i.label.trim() ||
        !text(i.label, 250) ||
        typeof i.required !== "boolean",
    ) ||
    new Set(items.map((i) => i.label.trim().toLowerCase())).size !==
      items.length ||
    typeof reason !== "string" ||
    reason.trim().length < 10 ||
    !text(reason, 500)
  )
    throw new Error("Dữ liệu tiêu chí công việc không hợp lệ");
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
    },
    body: JSON.stringify({
      items: items.map((i) => ({
        label: i.label.trim(),
        required: i.required,
      })),
      reason: reason.trim(),
    }),
  });
  const result = await response.json();
  if (
    response.status !== 201 ||
    result?.id !== task ||
    result?.kind !== "task" ||
    result?.initialized !== true
  )
    throw new Error("Phản hồi khởi tạo công việc không hợp lệ");
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
    !["IN_PROGRESS", "PAUSED"].includes(current.data.state) ||
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
    throw new Error("Dữ liệu tiến độ công việc không hợp lệ");
  const csrf = await (await request("/api/auth/csrf", signal)).json();
  if (
    csrf?.headerName !== "X-CSRF-TOKEN" ||
    typeof csrf.csrfToken !== "string" ||
    !csrf.csrfToken.trim() ||
    /[\r\n]/.test(csrf.csrfToken)
  )
    throw new Error("Phản hồi CSRF không hợp lệ");
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
