import { ApiError, getJson, send } from "./client";
import { positiveId } from "./rooms";
export interface CreateOptions {
  roomId: number;
  roomNumber: string;
  branchId: number;
  cleaningTypes: { id: number; code: string; name: string }[];
  priorities: { id: number; code: string; name: string }[];
  cleaningItemCount: number;
  cleaningItems?: {
    id: number;
    label: string;
    isRequired: boolean;
    sortOrder: number;
  }[];
  existingTaskId?: number | null;
  standardCleaningMinutes?: number | null;
}
export interface CreateInput {
  roomId: number;
  cleaningTypeId: number;
  priorityId: number;
  dueAt: string;
  instruction: string | null;
  internalRequestId?: number;
  requestLink?: { version: string; mandatory: boolean };
}
export interface CleaningSource { requestId: number; branchId: number; roomId: number | null; priorityId: number | null; instruction: string; deadline: string | null; version: string }
export async function cleaningSource(branch: string, request: string, signal: AbortSignal): Promise<CleaningSource> {
  const b=positiveId(branch), id=positiveId(request);
  const v=await getJson(`/branches/${b}/cleaning-request-context/${id}`,signal) as CleaningSource;
  if (!v || v.requestId!==id || v.branchId!==b || typeof v.instruction!=="string" || !/^[A-Za-z0-9+/]{11}=$/.test(v.version)
    || ![v.roomId,v.priorityId].every(n=>n===null || Number.isSafeInteger(n)&&n!>0)
    || !(v.deadline===null || typeof v.deadline==="string" && /Z$/.test(v.deadline) && Number.isFinite(Date.parse(v.deadline)))) throw new ApiError(502,"INVALID_CLEANING_SOURCE");
  return v;
}
function checkOptions(v: unknown, b: number, r: number): CreateOptions {
  const d = v as CreateOptions;
  if (
    !d ||
    d.roomId !== r ||
    d.branchId !== b ||
    typeof d.roomNumber !== "string" ||
    !Number.isInteger(d.cleaningItemCount) ||
    (d.standardCleaningMinutes != null && (!Number.isSafeInteger(d.standardCleaningMinutes) || d.standardCleaningMinutes <= 0 || d.standardCleaningMinutes > 2147483647)) ||
    ![d.cleaningTypes, d.priorities].every(
      (a) =>
        Array.isArray(a) &&
        a.every(
          (x) =>
            Number.isSafeInteger(x.id) &&
            x.id > 0 &&
            typeof x.code === "string" &&
            typeof x.name === "string",
        ),
    )
  )
    throw new ApiError(502, "INVALID_CREATE_OPTIONS");
  if (
    d.cleaningItems !== undefined &&
    (!Array.isArray(d.cleaningItems) ||
      d.cleaningItems.length !== d.cleaningItemCount ||
      !d.cleaningItems.every(
        (x) =>
          Number.isSafeInteger(x.id) &&
          x.id > 0 &&
          typeof x.label === "string" &&
          typeof x.isRequired === "boolean" &&
          Number.isInteger(x.sortOrder),
      ))
  )
    throw new ApiError(502, "INVALID_CREATE_ITEMS");
  if (d.existingTaskId !== undefined && d.existingTaskId !== null)
    positiveId(String(d.existingTaskId));
  return d;
}
export async function cleaningOptions(
  branch: string,
  room: string,
  signal?: AbortSignal,
) {
  const b = positiveId(branch),
    r = positiveId(room);
  return checkOptions(
    await getJson(`/branches/${b}/cleaning-task-options?roomId=${r}`, signal),
    b,
    r,
  );
}
export function validateCleaningInput(input: CreateInput) {
  if (input.internalRequestId !== undefined) {
    positiveId(String(input.internalRequestId));
    if (!input.requestLink || !/^[A-Za-z0-9+/]{11}=$/.test(input.requestLink.version) || typeof input.requestLink.mandatory!=="boolean") throw Error("SOURCE_VERSION_REQUIRED");
  } else if (input.requestLink !== undefined) throw Error("SOURCE_REQUIRED");
  for (const n of [input.roomId, input.cleaningTypeId, input.priorityId])
    positiveId(String(n));
  if (
    !/Z$/.test(input.dueAt) ||
    !Number.isFinite(Date.parse(input.dueAt)) ||
    Date.parse(input.dueAt) <= Date.now() ||
    (input.instruction?.length ?? 0) > 4000 ||
    /[\u0000-\u001f\u007f-\u009f]/.test(input.instruction ?? "")
  )
    throw Error("INVALID_CREATE_INPUT");
}
export async function createCleaning(branch: string, input: CreateInput, signal?: AbortSignal) {
  const b = positiveId(branch);
  validateCleaningInput(input);
  const token = (await getJson("/auth/csrf", signal)) as {
    csrfToken?: string;
    headerName?: string;
  };
  if (
    token?.headerName !== "X-CSRF-TOKEN" ||
    typeof token.csrfToken !== "string" ||
    !token.csrfToken.trim() ||
    /[\r\n]/.test(token.csrfToken)
  )
    throw new ApiError(502, "INVALID_CSRF_RESPONSE");
  signal?.throwIfAborted();
  const result = await send(`/branches/${b}/cleaning-tasks`, signal, {
    method: "POST",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
      "X-CSRF-TOKEN": token.csrfToken,
      ...(input.requestLink ? { "If-Match": `"${input.requestLink.version}"` } : {}),
    },
    body: JSON.stringify({
      roomId: input.roomId,
      cleaningTypeId: input.cleaningTypeId,
      priorityId: input.priorityId,
      dueAt: input.dueAt,
      instruction: input.instruction,
      ...(input.internalRequestId ? { internalRequestId: input.internalRequestId, requestLink: input.requestLink } : {}),
    }),
  });
  const d = result.body as {
    id: number;
    branchId: number;
    roomId: number;
    taskNumber: string;
    state: string;
    readiness: string;
    hasEffectiveBlock: boolean;
  };
  if (
    !d ||
    !Number.isSafeInteger(d.id) ||
    d.id <= 0 ||
    d.branchId !== b ||
    d.roomId !== input.roomId ||
    typeof d.taskNumber !== "string" ||
    d.state !== "PENDING_ASSIGNMENT" || d.readiness !== "PENDING_CLEANING" || typeof d.hasEffectiveBlock !== "boolean"
  )
    throw new ApiError(502, "CREATE_COMMITTED_RESPONSE_INVALID_RELOAD_MONITOR");
  return d;
}
export function createError(e: unknown) {
  if (e instanceof ApiError) {
    if (e.status === 401) return "Phiên đăng nhập đã hết hạn.";
    if (e.status === 403)
      return "Không có quyền CLEANING_ASSIGN tại chi nhánh này.";
    if (e.status === 404) return "Không tìm thấy phòng trong chi nhánh.";
    if (e.status === 409 && e.code === "INACTIVE") return "MSG24: Phòng hoặc cấu hình bắt buộc đã ngừng hoạt động. Tạo mới bị từ chối.";
    if (e.status === 409)
      return "Phòng không hợp lệ hoặc đã có công việc đang mở. Tải lại Monitor trước khi tạo tiếp.";
    if (e.status === 400)
      return "Thông tin không hợp lệ hoặc CSRF/HTTPS không hợp lệ.";
    if (e.status === 503) return "Schema hoặc dịch vụ chưa sẵn sàng.";
  }
  return "Không xác định được kết quả. Kiểm tra Monitor trước khi thử tạo lại; không tự gửi lại yêu cầu.";
}
