import { ApiError, getJson, send } from "./client";
import { positiveId } from "./rooms";
export interface CreateOptions {
  roomId: number;
  roomNumber: string;
  branchId: number;
  cleaningTypes: { id: number; code: string; name: string }[];
  priorities: { id: number; code: string; name: string }[];
  cleaningItemCount: number;
}
export interface CreateInput {
  roomId: number;
  cleaningTypeId: number;
  priorityId: number;
  dueAt: string;
  instruction: string | null;
}
function checkOptions(v: unknown, b: number, r: number): CreateOptions {
  const d = v as CreateOptions;
  if (
    !d ||
    d.roomId !== r ||
    d.branchId !== b ||
    typeof d.roomNumber !== "string" ||
    !Number.isInteger(d.cleaningItemCount) ||
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
export async function createCleaning(
  branch: string,
  input: CreateInput,
  signal?: AbortSignal,
) {
  const b = positiveId(branch);
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
    },
    body: JSON.stringify({
      roomId: input.roomId,
      cleaningTypeId: input.cleaningTypeId,
      priorityId: input.priorityId,
      dueAt: input.dueAt,
      instruction: input.instruction,
    }),
  });
  const d = result.body as {
    id: number;
    branchId: number;
    roomId: number;
    taskNumber: string;
    state: string;
  };
  if (
    !d ||
    !Number.isSafeInteger(d.id) ||
    d.id <= 0 ||
    d.branchId !== b ||
    d.roomId !== input.roomId ||
    typeof d.taskNumber !== "string" ||
    d.state !== "PENDING_ASSIGNMENT"
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
    if (e.status === 409)
      return "Phòng không hợp lệ hoặc đã có công việc đang mở. Tải lại Monitor trước khi tạo tiếp.";
    if (e.status === 400)
      return "Thông tin không hợp lệ hoặc CSRF/HTTPS không hợp lệ.";
    if (e.status === 503) return "Schema hoặc dịch vụ chưa sẵn sàng.";
  }
  return "Không xác định được kết quả. Kiểm tra Monitor trước khi thử tạo lại; không tự gửi lại yêu cầu.";
}
