import { ApiError, getJson, send } from "./client";
import { positiveId } from "./rooms";
import type { AssignmentDraft } from "./cleaningAssignment";
// Prepared UC029 wire integration. Runtime Save remains unavailable until accepted A01/A02 is supplied.
export async function submitAssignment(
  branch: string,
  task: string,
  draft: AssignmentDraft,
  originalETag: string,
  operationId: string,
  signal?: AbortSignal,
) {
  const b = positiveId(branch),
    id = positiveId(task);
  positiveId(String(draft.assigneeId));
  if (
    !/^"[0-9A-F]{16}"$/.test(originalETag) ||
    !/^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(
      operationId,
    ) ||
    draft.adoptUnassignedLegacy !== false ||
    !draft.reason.trim() ||
    draft.reason.trim().length > 500 ||
    /[\u0000-\u001f\u007f-\u009f]/.test(draft.reason) ||
    !/Z$/.test(draft.dueAt) ||
    !Number.isFinite(Date.parse(draft.dueAt))
  )
    throw Error("INVALID_ASSIGNMENT_COMMAND");
  const csrf = (await getJson("/auth/csrf", signal)) as {
    headerName?: string;
    csrfToken?: string;
  };
  if (
    csrf?.headerName !== "X-CSRF-TOKEN" ||
    typeof csrf.csrfToken !== "string" ||
    !csrf.csrfToken.trim() ||
    /[\r\n]/.test(csrf.csrfToken)
  )
    throw new ApiError(502, "INVALID_CSRF_RESPONSE");
  signal?.throwIfAborted();
  await send(`/branches/${b}/cleaning-tasks/${id}/assign`, signal, {
    method: "POST",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
      "X-CSRF-TOKEN": csrf.csrfToken,
      "If-Match": originalETag,
      "Idempotency-Key": operationId,
    },
    body: JSON.stringify({
      assigneeId: draft.assigneeId,
      dueAt: draft.dueAt,
      reason: draft.reason.trim(),
      adoptUnassignedLegacy: false,
    }),
  });
  // A future accepted publisher result contract must be mapped here before UI activation.
  throw new ApiError(
    502,
    "ASSIGNMENT_RESULT_CONTRACT_NOT_VERIFIED_RELOAD_SOURCE",
  );
}
