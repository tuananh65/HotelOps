import { isObject } from "../auth/http";
import type { Values } from "./contracts";

export async function requestCreationDefaults(request: string, branch: string, version: string,
  incident: boolean, signal: AbortSignal): Promise<Values> {
  const positive = (v: unknown) => Number.isSafeInteger(Number(v)) && Number(v) > 0;
  if (!positive(request) || !positive(branch) || !/^[A-Za-z0-9+/]{11}=$/.test(version))
    throw Error("Reload the source request before creating linked work.");
  const response = await fetch(`/api/internal-requests/${encodeURIComponent(request)}/creation-context`, {
    credentials: "same-origin", cache: "no-store", redirect: "error", signal,
  });
  if (!response.ok) throw Error(response.status === 403
    ? "MSG02: Current request authority is required. Link existing work or ask the responsible role."
    : "Source request unavailable. Return to the request and reload before creating work.");
  const v: unknown = await response.json();
  if (!isObject(v) || v.requestId !== Number(request) || v.branchId !== Number(branch) || v.version !== version
    || typeof v.subject !== "string" || typeof v.description !== "string"
    || ![v.roomId, v.areaId, v.priorityId].every(n => n === null || typeof n === "number" && positive(n))
    || v.roomId !== null && v.areaId !== null)
    throw Error("Source request changed. Return to the request and reload before creating work.");
  const location = incident ? { roomId: v.roomId ?? "", areaId: v.areaId ?? "" }
    : { receivingRoomId: v.roomId ?? "", receivingAreaId: v.areaId ?? "" };
  return { branchId: branch, priorityId: v.priorityId ?? "", ...location,
    ...(incident ? { incidentTitle: v.subject, description: v.description } : { purpose: v.subject }) } as Values;
}
