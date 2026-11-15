import { TaskProgressError } from "./progress";

export interface VerificationDecision {
  submissionId: number;
  decision: "ACCEPT" | "RETURN";
  note: string;
  requestedBy: number;
  submittedBy: number;
  performedBy: number[];
  verifiedBy: number;
  decidedAt: string;
  actualEffortSeconds: number;
}
export interface VerificationReview {
  taskId: number;
  branchId: number;
  submissionId: number;
  state: string;
  actualEffortSeconds: number;
  canDecide: boolean;
  denialCode: string | null;
  decisions: VerificationDecision[];
}
export interface VerificationSnapshot {
  data: VerificationReview;
  etag: string;
}
const positive = (n: number) => Number.isSafeInteger(n) && n > 0;
const tag = (s: string) => /^"[0-9A-F]{16}"$/.test(s);
function route(branch: number, task: number) {
  if (!positive(branch) || !positive(task))
    throw new Error("Invalid task identity");
  return `/api/branches/${branch}/operational-tasks/${task}/verification`;
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
      p?.messageCode === "MSG22" ? "MSG22" : undefined,
    );
  }
  return response;
}
export async function readVerification(
  branch: number,
  task: number,
  signal: AbortSignal,
): Promise<VerificationSnapshot> {
  const response = await request(route(branch, task), signal);
  const data = (await response.json()) as VerificationReview;
  const etag = response.headers.get("ETag") ?? "";
  if (
    !data ||
    data.taskId !== task ||
    data.branchId !== branch ||
    !positive(data.submissionId) ||
    !tag(etag) ||
    ![
      "IN_PROGRESS",
      "PENDING_VERIFICATION",
      "ACCEPTED",
      "CLOSED",
      "PAUSED",
    ].includes(data.state) ||
    typeof data.canDecide !== "boolean" ||
    !Number.isSafeInteger(data.actualEffortSeconds) ||
    data.actualEffortSeconds < 0 ||
    (data.canDecide && data.state !== "PENDING_VERIFICATION") ||
    !Array.isArray(data.decisions) ||
    data.decisions.some(
      (d) =>
        !d ||
        !positive(d.submissionId) ||
        !positive(d.requestedBy) ||
        !positive(d.submittedBy) ||
        !positive(d.verifiedBy) ||
        !["ACCEPT", "RETURN"].includes(d.decision) ||
        typeof d.note !== "string" ||
        d.note.length > 4000 ||
        !Array.isArray(d.performedBy) ||
        d.performedBy.some((p) => !positive(p)) ||
        d.performedBy.includes(d.verifiedBy) ||
        d.requestedBy === d.verifiedBy ||
        d.submittedBy === d.verifiedBy ||
        !Number.isFinite(Date.parse(d.decidedAt)) ||
        !Number.isSafeInteger(d.actualEffortSeconds) ||
        d.actualEffortSeconds < 0,
    )
  )
    throw new Error("Invalid verification snapshot");
  return { data, etag };
}
export async function decideVerification(
  branch: number,
  task: number,
  snapshot: VerificationSnapshot,
  decision: "ACCEPT" | "RETURN",
  note: string,
  signal: AbortSignal,
) {
  const url = route(branch, task);
  const clean = note.trim();
  if (
    snapshot.data.taskId !== task ||
    snapshot.data.branchId !== branch ||
    !positive(snapshot.data.submissionId) ||
    !snapshot.data.canDecide ||
    snapshot.data.state !== "PENDING_VERIFICATION" ||
    !tag(snapshot.etag) ||
    !["ACCEPT", "RETURN"].includes(decision) ||
    clean.length < (decision === "RETURN" ? 10 : 1) ||
    note.length > 4000 ||
    /[\u0000-\u0008\u000b\u000c\u000e-\u001f\u007f-\u009f]/.test(note)
  )
    throw new Error("Invalid verification decision");
  const csrf = await (await request("/api/auth/csrf", signal)).json();
  if (
    csrf?.headerName !== "X-CSRF-TOKEN" ||
    typeof csrf.csrfToken !== "string" ||
    !csrf.csrfToken.trim() ||
    /[\r\n]/.test(csrf.csrfToken)
  )
    throw new Error("Invalid CSRF response");
  signal.throwIfAborted();
  const response = await request(url, signal, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-CSRF-TOKEN": csrf.csrfToken,
      "If-Match": snapshot.etag,
    },
    body: JSON.stringify({
      submissionId: snapshot.data.submissionId,
      decision,
      note: clean,
    }),
  });
  const body = await response.json();
  const etag = response.headers.get("ETag") ?? "";
  const state = decision === "ACCEPT" ? "CLOSED" : "ACCEPTED";
  const messageCode = decision === "ACCEPT" ? "MSG08" : "MSG20";
  if (
    body?.taskId !== task ||
    body?.branchId !== branch ||
    body?.submissionId !== snapshot.data.submissionId ||
    body?.state !== state ||
    body?.messageCode !== messageCode ||
    !tag(etag) ||
    etag === snapshot.etag
  )
    throw new Error("Verification outcome unconfirmed");
  return { state, messageCode };
}
