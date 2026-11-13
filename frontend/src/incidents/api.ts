import { createHttpClient, isObject } from "../auth/http";
import type { ApiResult } from "../auth/contracts";
import { incidentId } from "./navigation";

export interface Option { id: number; name: string }
export interface Options {
  branches: Option[]; rooms: Option[]; areas: Option[]; assets: Option[];
  severities: Option[]; priorities: Option[]; canCreate: boolean;
}
export interface CreateIncident {
  branchId: number; incidentTitle: string; description: string; discoveredAt: string;
  roomId?: number | null; areaId?: number | null; assetId?: number | null;
  severityId?: number | null; priorityId?: number | null;
}
export interface CreatedIncident { incidentId: number; incidentNo: string; branchId: number; roomId: number | null }

function safeId(value: unknown): value is number {
  return typeof value === "number" && Number.isSafeInteger(value) && value > 0;
}
function decodeOptions(value: unknown): Options | undefined {
  if (!isObject(value) || typeof value.canCreate !== "boolean") return;
  for (const key of ["branches", "rooms", "areas", "assets", "severities", "priorities"]) {
    const items = value[key];
    if (!Array.isArray(items) || items.some(v => !isObject(v) || !safeId(v.id) || typeof v.name !== "string")) return;
  }
  return value as unknown as Options;
}
export function options(branchId?: number, roomId?: number, signal?: AbortSignal): Promise<ApiResult<Options>> {
  const query = new URLSearchParams();
  if (branchId !== undefined) query.set("branchId", String(incidentId(String(branchId))));
  if (roomId !== undefined) query.set("roomId", String(incidentId(String(roomId))));
  // Existing auth transport accepts fixed paths only. This FE06 adapter appends
  // an allowlisted numeric query after path validation; cookie/error behavior stays canonical.
  const http = createHttpClient((url, init) => globalThis.fetch(`${url}${query.size ? `?${query}` : ""}`, init));
  return http({ method: "GET", url: "/api/incidents/options", expectedStatus: 200, decode: decodeOptions, signal });
}
export async function create(body: CreateIncident): Promise<ApiResult<CreatedIncident>> {
  const http = createHttpClient();
  const token = await http({ method: "GET", url: "/api/auth/csrf", expectedStatus: 200,
    decode: value => isObject(value) && value.headerName === "X-CSRF-TOKEN" &&
      typeof value.csrfToken === "string" && /^[\x21-\x7e]+$/.test(value.csrfToken) ? value.csrfToken : undefined });
  if (!token.ok) return token;
  return http({ method: "POST", url: "/api/incidents", expectedStatus: 201, csrfToken: token.data, body,
    decode: value => isObject(value) && safeId(value.incidentId) && safeId(value.branchId) &&
      (value.roomId === null || safeId(value.roomId)) && typeof value.incidentNo === "string"
      ? value as unknown as CreatedIncident : undefined });
}
