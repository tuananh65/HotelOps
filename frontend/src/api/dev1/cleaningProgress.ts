import { ApiError, getJson, send } from "./client";
import { positiveId } from "./rooms";

export interface CleaningProgressItem {
  id: number;
  label: string;
  required: boolean;
  order: number;
  done: boolean | null;
  outcome: null;
  note: string | null;
}
export interface ProgressHistory {
  id: number;
  percent: number;
  status: string | null;
  blockerTypeId: number | null;
  note: string | null;
  actorId: number;
  at: string;
}
export interface ProgressEvidence {
  id: number;
  entityType: string;
  entityId: number;
  name: string;
  mimeType: string | null;
  bytes: number | null;
  uploadedAt: string;
  downloadPath: string;
}
export interface CleaningProgress {
  id: number;
  branchId: number;
  kind: "cleaning";
  state: "IN_PROGRESS";
  percent: number;
  items: CleaningProgressItem[];
  timeline: ProgressHistory[];
  evidence: ProgressEvidence[];
}
export interface EvidenceUpload {
  name: string;
  mimeType: string;
  base64: string;
}
export interface SaveCleaningProgress {
  percent: number;
  items: { id: number; done: boolean; note: string | null }[];
  note: string | null;
  photos?: EvidenceUpload[];
}
export interface ProgressSnapshot {
  data: CleaningProgress;
  etag: string;
}
export const strongProgressTag = (value: string) =>
  /^"[0-9A-F]{16}"$/.test(value);
const idValid = (value: unknown): value is number =>
  typeof value === "number" && Number.isSafeInteger(value) && value > 0;
const percentValid = (value: unknown) =>
  typeof value === "number" &&
  Number.isInteger(value) &&
  value >= 0 &&
  value <= 100;
const nullableText = (value: unknown) =>
  value === null || typeof value === "string";
const cleanText = (value: string | null, max: number) =>
  value === null ||
  (typeof value === "string" &&
    value.trim().length <= max &&
    !/[\u0000-\u001f\u007f-\u009f]/.test(value));
function resource(branch: string, task: string) {
  return `/branches/${positiveId(branch)}/cleaning-tasks/${positiveId(task)}/progress`;
}
function parseProgress(
  value: unknown,
  branch: string,
  task: string,
): CleaningProgress {
  const data = value as CleaningProgress;
  if (
    !data ||
    data.id !== positiveId(task) ||
    data.branchId !== positiveId(branch) ||
    data.kind !== "cleaning" ||
    data.state !== "IN_PROGRESS" ||
    !percentValid(data.percent) ||
    !Array.isArray(data.items) ||
    data.items.length > 100 ||
    new Set(data.items.map((i) => i?.id)).size !== data.items.length ||
    data.items.some(
      (i) =>
        !i ||
        !idValid(i.id) ||
        typeof i.label !== "string" ||
        typeof i.required !== "boolean" ||
        !Number.isInteger(i.order) ||
        (i.done !== null && typeof i.done !== "boolean") ||
        i.outcome !== null ||
        !nullableText(i.note),
    ) ||
    !Array.isArray(data.timeline) ||
    data.timeline.length > 100 ||
    data.timeline.some(
      (i) =>
        !i ||
        !idValid(i.id) ||
        !idValid(i.actorId) ||
        !percentValid(i.percent) ||
        !nullableText(i.status) ||
        !nullableText(i.note) ||
        (i.blockerTypeId !== null && !idValid(i.blockerTypeId)) ||
        typeof i.at !== "string",
    ) ||
    !Array.isArray(data.evidence) ||
    data.evidence.length > 100 ||
    data.evidence.some(
      (i) =>
        !i ||
        !idValid(i.id) ||
        i.entityType !== "CLEANING_TASK" ||
        i.entityId !== data.id ||
        typeof i.name !== "string" ||
        !nullableText(i.mimeType) ||
        (i.bytes !== null && (!Number.isSafeInteger(i.bytes) || i.bytes < 0)) ||
        typeof i.uploadedAt !== "string" ||
        i.downloadPath !==
          `/api/branches/${data.branchId}/cleaning-tasks/${data.id}/progress/evidence/${i.id}`,
    )
  )
    throw new ApiError(502, "INVALID_PROGRESS_RESPONSE");
  return data;
}

function snapshot(
  result: Awaited<ReturnType<typeof send>>,
  branch: string,
  task: string,
): ProgressSnapshot {
  if (!strongProgressTag(result.etag))
    throw new ApiError(502, "INVALID_PROGRESS_ETAG");
  return { data: parseProgress(result.body, branch, task), etag: result.etag };
}
export async function readCleaningProgress(
  branch: string,
  task: string,
  signal?: AbortSignal,
) {
  return snapshot(
    await send(resource(branch, task), signal, {
      headers: { Accept: "application/json" },
    }),
    branch,
    task,
  );
}
export async function saveCleaningProgress(
  branch: string,
  task: string,
  current: ProgressSnapshot,
  input: SaveCleaningProgress,
  signal?: AbortSignal,
) {
  const path = resource(branch, task);
  parseProgress(current.data, branch, task);
  if (
    !strongProgressTag(current.etag) ||
    !percentValid(input.percent) ||
    !cleanText(input.note, 4000) ||
    !Array.isArray(input.items) ||
    input.items.length > 100 ||
    new Set(input.items.map((i) => i.id)).size !== input.items.length ||
    input.items.some(
      (i) =>
        !current.data.items.some((item) => item.id === i.id) ||
        typeof i.done !== "boolean" ||
        !cleanText(i.note, 1000),
    ) ||
    (input.photos?.length ?? 0) > 5
  )
    throw new Error("INVALID_PROGRESS_INPUT");
  for (const photo of input.photos ?? []) {
    if (
      !photo ||
      !photo.name ||
      photo.name.length > 200 ||
      /[\\/:\u0000-\u001f\u007f-\u009f]/.test(photo.name) ||
      !/^(image\/png|image\/jpeg|application\/pdf)$/.test(photo.mimeType) ||
      !photo.base64 ||
      photo.base64.length > 14 * 1024 * 1024
    )
      throw new Error("INVALID_EVIDENCE");
  }
  // Fetch a token bound to the current cookie identity for each user-initiated save.
  // Never store it, reuse prototype auth, or retry an unsafe request automatically.
  const csrf = (await getJson("/auth/csrf", signal)) as {
    csrfToken?: unknown;
    headerName?: unknown;
  };
  if (
    !csrf ||
    csrf.headerName !== "X-CSRF-TOKEN" ||
    typeof csrf.csrfToken !== "string" ||
    !csrf.csrfToken.trim() ||
    /[\r\n]/.test(csrf.csrfToken)
  )
    throw new ApiError(502, "INVALID_CSRF_RESPONSE");
  signal?.throwIfAborted();
  const body = {
    percent: input.percent,
    items: input.items.map((i) => ({ id: i.id, done: i.done, note: i.note })),
    note: input.note,
    ...(input.photos?.length
      ? {
          photos: input.photos.map((p) => ({
            name: p.name,
            mimeType: p.mimeType,
            base64: p.base64,
          })),
        }
      : {}),
  };
  return snapshot(
    await send(path, signal, {
      method: "PUT",
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json",
        "X-CSRF-TOKEN": csrf.csrfToken,
        "If-Match": current.etag,
      },
      body: JSON.stringify(body),
    }),
    branch,
    task,
  );
}
export async function uploadFile(file: File): Promise<EvidenceUpload> {
  if (
    file.size < 1 ||
    file.size > 10 * 1024 * 1024 ||
    !/^(image\/png|image\/jpeg|application\/pdf)$/.test(file.type) ||
    file.name.length > 200 ||
    /[\\/:\u0000-\u001f]/.test(file.name) ||
    !(
      file.type === "image/png"
        ? /\.png$/i
        : file.type === "image/jpeg"
          ? /\.jpe?g$/i
          : /\.pdf$/i
    ).test(file.name)
  )
    throw new Error("INVALID_EVIDENCE");
  const bytes = new Uint8Array(await file.arrayBuffer());
  const prefix = [...bytes.slice(0, 8)].join(",");
  if (
    !(file.type === "image/png"
      ? prefix === "137,80,78,71,13,10,26,10"
      : file.type === "image/jpeg"
        ? prefix.startsWith("255,216,255")
        : prefix.startsWith("37,80,68,70,45"))
  )
    throw new Error("INVALID_EVIDENCE");
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(new Error("FILE_READ_FAILED"));
    reader.onload = () =>
      resolve({
        name: file.name,
        mimeType: file.type,
        base64: String(reader.result).split(",")[1],
      });
    reader.readAsDataURL(file);
  });
}
export function progressError(error: unknown): string {
  if (!(error instanceof ApiError))
    return "Dữ liệu không hợp lệ. Kiểm tra tiến độ, ghi chú một dòng và tệp JPG/PNG/PDF tối đa 10 MiB/tệp, tối đa 5 tệp.";
  if (error.status === 412)
    return "Dữ liệu đã thay đổi. Tải lại để xem bản mới trước khi sửa và lưu lại.";
  if (error.status === 428)
    return "Thiếu phiên bản dữ liệu. Cần tải lại trước khi lưu.";
  if (error.status === 409)
    return "Công việc hoặc chi nhánh không còn ở trạng thái cho phép. Cần tải lại.";
  if (error.status === 400)
    return error.code === "CSRF_VALIDATION_FAILED"
      ? "Phiên xác thực đã thay đổi. Tải lại và đăng nhập lại nếu cần; yêu cầu chưa được tự gửi lại."
      : "Dữ liệu hoặc tệp đính kèm không hợp lệ.";
  if (error.status === 401)
    return "Phiên đăng nhập hết hạn hoặc không hợp lệ. Hãy đăng nhập bằng phiên máy chủ.";
  if (error.status === 403)
    return "Bạn không có quyền cập nhật hoặc không còn là người được phân công.";
  if (error.status === 404)
    return "Không tìm thấy công việc trong chi nhánh này.";
  if (error.status === 503)
    return "Dịch vụ hoặc cấu hình tiến độ chưa khả dụng.";
  return "Không xác nhận được kết quả yêu cầu. Tải lại dữ liệu trước khi lưu tiếp; hệ thống không tự gửi lại thao tác lưu.";
}
