import { getJson } from "./client";

export interface ReworkRecord {
  id: number;
  branchId: number;
  roomNumber: string;
  taskNumber: string;
  inspectionId: number;
  recordedStatus: string;
  reason: string;
  note: string | null;
  requestedAt: string;
  completedAt: string | null;
}

export async function readReworkRecord(
  branch: string,
  id: string,
  signal: AbortSignal,
): Promise<ReworkRecord> {
  if (
    ![branch, id].every(
      (v) =>
        /^\d+$/.test(v) && Number.isSafeInteger(Number(v)) && Number(v) > 0,
    )
  )
    throw new Error("Định danh không hợp lệ");
  const raw = (await getJson(
    `/branches/${branch}/reworks/${id}/record`,
    signal,
  )) as ReworkRecord;
  if (
    !raw ||
    raw.id !== Number(id) ||
    raw.branchId !== Number(branch) ||
    !Number.isSafeInteger(raw.inspectionId) ||
    raw.inspectionId <= 0 ||
    ![raw.roomNumber, raw.taskNumber, raw.recordedStatus, raw.reason].every(
      (v) => typeof v === "string",
    ) ||
    !(raw.note === null || typeof raw.note === "string") ||
    typeof raw.requestedAt !== "string" ||
    !Number.isFinite(Date.parse(raw.requestedAt)) ||
    !(
      raw.completedAt === null ||
      (typeof raw.completedAt === "string" &&
        Number.isFinite(Date.parse(raw.completedAt)))
    )
  )
    throw new Error("Phản hồi hồ sơ làm lại không hợp lệ");
  return raw;
}
