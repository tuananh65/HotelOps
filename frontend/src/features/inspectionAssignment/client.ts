import { ApiError, getJson, send } from "../../api/dev1/client";
import { strongTag } from "../inspectionReinspection/candidateClient";
export interface AssignmentQueue {
  pending: { taskId: number; roomId: number }[];
  assigned: {
    inspectionId: number;
    roomId: number;
    checklistVersionId: number;
    inspectionRevision: string;
  }[];
}
export interface AssignmentOptions {
  taskId: number;
  roomId: number;
  taskRevision: string;
  cycleRevision: string;
  checklistVersionId: number;
  inspectors: { userId: number; fullName: string }[];
  responsibleSupervisors: { userId: number; fullName: string }[];
  productionMutationAvailable: false;
}
const id = (v: unknown): v is number =>
  Number.isSafeInteger(v) && (v as number) > 0;
function object(v: unknown): Record<string, unknown> {
  if (!v || typeof v !== "object" || Array.isArray(v))
    throw new ApiError(502, "INVALID_ASSIGNMENT_RESPONSE");
  return v as Record<string, unknown>;
}
function people(v: unknown) {
  return (
    Array.isArray(v) &&
    v.every((p) => {
      const x = object(p);
      return id(x.userId) && typeof x.fullName === "string";
    }) &&
    new Set(v.map((p) => p.userId)).size === v.length
  );
}
export class InspectionAssignmentClient {
  private path(branch: number) {
    if (!id(branch)) throw new ApiError(400, "BRANCH_REQUIRED");
    return `/branches/${branch}/inspection-assignment-candidate`;
  }
  async queue(branch: number, signal?: AbortSignal): Promise<AssignmentQueue> {
    const q = object(await getJson(this.path(branch), signal));
    if (
      !Array.isArray(q.pending) ||
      !Array.isArray(q.assigned) ||
      q.pending.some((p) => {
        const x = object(p);
        return !id(x.taskId) || !id(x.roomId);
      }) ||
      q.assigned.some((p) => {
        const x = object(p);
        return (
          !id(x.inspectionId) ||
          !id(x.roomId) ||
          !id(x.checklistVersionId) ||
          !strongTag(x.inspectionRevision)
        );
      })
    )
      throw new ApiError(502, "INVALID_ASSIGNMENT_RESPONSE");
    return q as unknown as AssignmentQueue;
  }
  async options(
    branch: number,
    task: number,
    signal?: AbortSignal,
  ): Promise<AssignmentOptions> {
    if (!id(task)) throw new ApiError(400, "TASK_REQUIRED");
    const x = object(await getJson(this.path(branch) + `/${task}`, signal));
    if (
      x.taskId !== task ||
      !id(x.roomId) ||
      !id(x.checklistVersionId) ||
      !strongTag(x.taskRevision) ||
      !strongTag(x.cycleRevision) ||
      !people(x.inspectors) ||
      !people(x.responsibleSupervisors) ||
      x.productionMutationAvailable !== false
    )
      throw new ApiError(502, "INVALID_ASSIGNMENT_RESPONSE");
    return x as unknown as AssignmentOptions;
  }
  async confirm(
    branch: number,
    source: AssignmentOptions,
    inspectorId: number,
    supervisorId: number,
    dueAt: string,
    reason: string,
    confirmed: boolean,
    signal?: AbortSignal,
  ): Promise<never> {
    if (
      !confirmed ||
      !source.inspectors.some((p) => p.userId === inspectorId) ||
      !source.responsibleSupervisors.some((p) => p.userId === supervisorId) ||
      !reason.trim() ||
      !Number.isFinite(Date.parse(dueAt)) ||
      !strongTag(source.taskRevision) ||
      !strongTag(source.cycleRevision)
    )
      throw new ApiError(400, "INVALID_ASSIGNMENT_INPUT");
    const csrf = object(await getJson("/auth/csrf", signal));
    if (
      csrf.headerName !== "X-CSRF-TOKEN" ||
      typeof csrf.csrfToken !== "string" ||
      !csrf.csrfToken.trim() ||
      /[\r\n]/.test(csrf.csrfToken)
    )
      throw new ApiError(502, "INVALID_CSRF_RESPONSE");
    await send(this.path(branch) + `/${source.taskId}`, signal, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "X-CSRF-TOKEN": csrf.csrfToken,
        "If-Match": source.taskRevision,
      },
      body: JSON.stringify({
        inspectorId,
        responsibleSupervisorId: supervisorId,
        dueAt,
        reason: reason.trim(),
        cycleRevision: source.cycleRevision,
        checklistVersionId: source.checklistVersionId,
        confirmed: true,
      }),
    });
    // No accepted successful mutation contract exists. Never display success from an unexpected 2xx.
    throw new ApiError(502, "UC041_UNEXPECTED_MUTATION_SUCCESS");
  }
}
export function assignmentError(e: unknown) {
  if (e instanceof ApiError) {
    if (e.status === 401) return "Your session expired. Sign in again.";
    if (e.status === 403)
      return "You cannot assign inspections in this branch.";
    if (e.status === 503)
      return "Assignment is unavailable until mandatory notification is accepted.";
    if (e.status === 412 || e.status === 409)
      return "The assignment source changed. Reload before continuing.";
    if (e.status === 0 || e.status >= 500)
      return "The outcome is unconfirmed. Reload; do not retry automatically.";
  }
  return "Review the assignment input or reload.";
}
