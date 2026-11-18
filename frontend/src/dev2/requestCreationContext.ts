import { isObject } from "../auth/http";
import type { Values } from "./contracts";

export async function requestCreationDefaults(request: string, branch: string, version: string,
  incident: boolean, signal: AbortSignal): Promise<Values> {
  const positive = (v: unknown) => Number.isSafeInteger(Number(v)) && Number(v) > 0;
  if (!positive(request) || !positive(branch) || !/^[A-Za-z0-9+/]{11}=$/.test(version))
    throw Error("Tải lại yêu cầu nguồn trước khi tạo công việc liên quan.");
  const response = await fetch(`/api/internal-requests/${encodeURIComponent(request)}/creation-context`, {
    credentials: "same-origin", cache: "no-store", redirect: "error", signal,
  });
  if (!response.ok) throw Error(response.status === 403
    ? "MSG02: Cần thẩm quyền yêu cầu hiện tại. Liên kết công việc sẵn có hoặc nhờ người có trách nhiệm."
    : "Yêu cầu nguồn chưa khả dụng. Quay lại yêu cầu và tải lại trước khi tạo việc.");
  const v: unknown = await response.json();
  if (!isObject(v) || v.requestId !== Number(request) || v.branchId !== Number(branch) || v.version !== version
    || typeof v.subject !== "string" || typeof v.description !== "string"
    || ![v.roomId, v.areaId, v.priorityId].every(n => n === null || typeof n === "number" && positive(n))
    || v.roomId !== null && v.areaId !== null)
    throw Error("Yêu cầu nguồn đã đổi. Quay lại yêu cầu và tải lại trước khi tạo việc.");
  const location = incident ? { roomId: v.roomId ?? "", areaId: v.areaId ?? "" }
    : { receivingRoomId: v.roomId ?? "", receivingAreaId: v.areaId ?? "" };
  return { branchId: branch, priorityId: v.priorityId ?? "", ...location,
    ...(incident ? { incidentTitle: v.subject, description: v.description } : { purpose: v.subject }) } as Values;
}
