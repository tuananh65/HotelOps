import {
  TaskProgressError,
  type Criterion,
  type TaskEvidence,
  type TaskProgressSnapshot,
} from "./progress";

export interface TaskSubmission {
  id: number;
  taskId: number;
  branchId: number;
  submittedBy: number;
  submittedAt: string;
  resultNote: string | null;
  handoverNote: string | null;
  criteria: Criterion[];
  evidence: TaskEvidence[];
}
const id = (n: number) => Number.isSafeInteger(n) && n > 0;
const note = (s: string | null) =>
  s === null ||
  (typeof s === "string" &&
    s.length <= 4000 &&
    !/[\u0000-\u0008\u000b\u000c\u000e-\u001f\u007f-\u009f]/.test(s));
function path(branch: number, task: number) {
  if (!id(branch) || !id(task)) throw new Error("Invalid task identity");
  return `/api/branches/${branch}/operational-tasks/${task}`;
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
  if (!response.ok) {
    const p = await response.json().catch(() => null);
    throw new TaskProgressError(
      response.status,
      ["MSG03", "MSG23"].includes(p?.messageCode) ? p.messageCode : undefined,
      Array.isArray(p?.missingCriteria) &&
        p.missingCriteria.length <= 100 &&
        p.missingCriteria.every(
          (s: unknown) => typeof s === "string" && s.length <= 250,
        )
        ? p.missingCriteria
        : [],
    );
  }
  return response;
}
export async function submitTask(
  branch: number,
  task: number,
  current: TaskProgressSnapshot,
  input: {
    items: { id: number; outcome: "PASS" | "FAIL"; note: string | null }[];
    resultNote: string | null;
    handoverNote: string | null;
    photos: { name: string; mimeType: string; base64: string }[];
  },
  signal: AbortSignal,
) {
  const url = path(branch, task);
  if (
    current.data.id !== task ||
    current.data.branchId !== branch ||
    current.data.kind !== "task" ||
    current.data.state !== "IN_PROGRESS" ||
    !/^"[0-9A-F]{16}"$/.test(current.etag) ||
    !note(input.resultNote) ||
    !note(input.handoverNote) ||
    input.photos.length > 5 ||
    input.items.length > 100 ||
    new Set(input.items.map((i) => i.id)).size !== input.items.length ||
    input.items.some(
      (i) =>
        !current.data.items.some((c) => c.id === i.id) ||
        !["PASS", "FAIL"].includes(i.outcome),
    )
  )
    throw new Error("Invalid submission");
  const csrf = await (await request("/api/auth/csrf", signal)).json();
  if (
    csrf?.headerName !== "X-CSRF-TOKEN" ||
    typeof csrf.csrfToken !== "string" ||
    !csrf.csrfToken.trim() ||
    /[\r\n]/.test(csrf.csrfToken)
  )
    throw new Error("Invalid CSRF response");
  signal.throwIfAborted();
  const response = await request(url + "/submit", signal, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-CSRF-TOKEN": csrf.csrfToken,
      "If-Match": current.etag,
    },
    body: JSON.stringify(input),
  });
  const data = await response.json();
  const etag = response.headers.get("ETag") ?? "";
  if (
    data?.id !== task ||
    data?.branchId !== branch ||
    data?.kind !== "task" ||
    data?.state !== "PENDING_VERIFICATION" ||
    !/^"[0-9A-F]{16}"$/.test(etag) ||
    etag === current.etag
  )
    throw new Error("Submission could not be confirmed");
  // The editor reloads the validated canonical progress snapshot after commit.
  return { etag };
}
export async function readTaskSubmission(
  branch: number,
  task: number,
  signal: AbortSignal,
  submission?: number,
): Promise<TaskSubmission> {
  const url = path(branch, task);
  if (submission !== undefined && !id(submission))
    throw new Error("Invalid submission identity");
  const suffix = submission === undefined ? "" : `?submission=${submission}`;
  const data = (await (
    await request(url + "/submission" + suffix, signal)
  ).json()) as TaskSubmission;
  if (
    !data ||
    !id(data.id) ||
    (submission !== undefined && data.id !== submission) ||
    data.taskId !== task ||
    data.branchId !== branch ||
    !id(data.submittedBy) ||
    !Number.isFinite(Date.parse(data.submittedAt)) ||
    !note(data.resultNote) ||
    !note(data.handoverNote) ||
    !Array.isArray(data.criteria) ||
    data.criteria.length > 100 ||
    new Set(data.criteria.map((i) => i?.id)).size !== data.criteria.length ||
    data.criteria.some(
      (i) =>
        !i ||
        !id(i.id) ||
        typeof i.label !== "string" ||
        typeof i.required !== "boolean" ||
        ![null, "PASS", "FAIL"].includes(i.outcome),
    ) ||
    !Array.isArray(data.evidence) ||
    data.evidence.length > 100 ||
    data.evidence.some(
      (f) =>
        !f ||
        !id(f.id) ||
        f.entityType !== "TASK" ||
        f.entityId !== task ||
        typeof f.name !== "string" ||
        f.downloadPath !== url + `/submission/evidence/${f.id}` + suffix,
    )
  )
    throw new Error("Invalid task submission response");
  return data;
}
