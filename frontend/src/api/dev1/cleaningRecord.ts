import { getJson } from "./client";
export interface CleaningRecord {
  id: number;
  branchId: number;
  roomNumber: string;
  taskNumber: string;
  recordedStatus: string;
  cleaningType: string;
  instructions: string | null;
  dueAt: string | null;
}
export async function readCleaningRecord(
  branch: string,
  task: string,
  signal: AbortSignal,
): Promise<CleaningRecord> {
  if (
    ![branch, task].every(
      (v) =>
        /^\d+$/.test(v) && Number.isSafeInteger(Number(v)) && Number(v) > 0,
    )
  )
    throw new Error("Định danh không hợp lệ");
  const d = (await getJson(
    `/branches/${branch}/cleaning-tasks/${task}/record`,
    signal,
  )) as CleaningRecord;
  if (
    !d ||
    d.id !== Number(task) ||
    d.branchId !== Number(branch) ||
    ![d.roomNumber, d.taskNumber, d.recordedStatus, d.cleaningType].every(
      (v) => typeof v === "string",
    ) ||
    !(d.instructions === null || typeof d.instructions === "string") ||
    !(
      d.dueAt === null ||
      (typeof d.dueAt === "string" && Number.isFinite(Date.parse(d.dueAt)))
    )
  )
    throw new Error("Hồ sơ công việc không hợp lệ");
  return d;
}
