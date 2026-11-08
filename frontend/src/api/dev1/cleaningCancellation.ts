import { ApiError, getJson, send } from "./client";
import { positiveId } from "./rooms";

export interface CancellationOptions {
  id: number;
  branchId: number;
  sourceETag: string;
  capability: "PROVISIONAL_V1" | "UNAVAILABLE";
  blocker: string | null;
}
const tag = (v: unknown): v is string =>
  typeof v === "string" && /^"[0-9A-F]{16}"$/.test(v);
export async function readCancellation(
  branch: string,
  task: string,
  signal?: AbortSignal,
): Promise<CancellationOptions> {
  const b = positiveId(branch),
    id = positiveId(task);
  const r = await send(
    `/branches/${b}/cleaning-tasks/${id}/cancellation`,
    signal,
  );
  const d = r.body;
  if (
    !d ||
    d.id !== id ||
    d.branchId !== b ||
    !tag(d.sourceETag) ||
    r.etag !== d.sourceETag ||
    !["PROVISIONAL_V1", "UNAVAILABLE"].includes(d.capability) ||
    !(d.blocker === null || typeof d.blocker === "string") ||
    (d.capability === "PROVISIONAL_V1" && d.blocker !== null)
  )
    throw new ApiError(502, "INVALID_CANCELLATION_RESPONSE");
  return d;
}
export function cancellationCommand(
  options: CancellationOptions,
  confirmed: boolean,
  reason: string,
) {
  const text = reason.trim();
  if (
    options.capability !== "PROVISIONAL_V1" ||
    !tag(options.sourceETag) ||
    !confirmed ||
    !text ||
    text.length > 500 ||
    /[\u0000-\u001f\u007f]/.test(text)
  )
    throw new Error("Confirmation and reason required");
  return { confirmed: true, reason: text };
}
export async function cancelCleaning(
  options: CancellationOptions,
  confirmed: boolean,
  reason: string,
  key: string,
) {
  const body = cancellationCommand(options, confirmed, reason);
  if (
    !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(key)
  )
    throw new Error("Invalid operation key");
  const csrf = (await getJson("/auth/csrf")) as {
    headerName?: string;
    csrfToken?: string;
  };
  if (
    csrf?.headerName !== "X-CSRF-TOKEN" ||
    !csrf.csrfToken?.trim() ||
    /[\r\n]/.test(csrf.csrfToken)
  )
    throw new ApiError(502, "INVALID_CSRF_RESPONSE");
  const r = await send(
    `/branches/${positiveId(String(options.branchId))}/cleaning-tasks/${positiveId(String(options.id))}/cancel`,
    undefined,
    {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "X-CSRF-TOKEN": csrf.csrfToken,
        "If-Match": options.sourceETag,
        "Idempotency-Key": key,
      },
      body: JSON.stringify(body),
    },
  );
  const d = r.body;
  if (
    !d ||
    d.id !== options.id ||
    d.branchId !== options.branchId ||
    d.operationId?.toLowerCase() !== key.toLowerCase() ||
    d.state !== "CANCELLED" ||
    d.readiness !== "PENDING_CLEANING" ||
    d.contract !== "PROVISIONAL_NOT_ACCEPTED" ||
    !tag(d.sourceETag) ||
    r.etag !== d.sourceETag ||
    !Number.isSafeInteger(d.readinessEventId) ||
    d.readinessEventId <= 0
  )
    throw new ApiError(502, "INVALID_CANCELLATION_RECEIPT");
  return d;
}
