import { ApiError, getJson } from "./client";

// Mirrors RoomDirectoryContracts.cs; no conversion to prototype lifecycle states.
export interface RoomDirectoryItem {
  id: number;
  branchId: number;
  roomNumber: string;
  floor: { id: number; code: string; name: string; number: number | null };
  area: { id: number; code: string; name: string } | null;
  roomType: { id: number; code: string; name: string };
  operationalStatusCode: string;
  isActive: boolean;
}
export interface RoomDirectoryResponse {
  items: RoomDirectoryItem[];
  page: number;
  pageSize: number;
  totalCount: number;
}
export const roomStatusCodes = [
  "AVAILABLE",
  "OCCUPIED",
  "DIRTY",
  "CLEANING",
  "INSPECTION",
  "READY",
  "OUT_OF_SERVICE",
] as const;

export function positiveId(value: string): number {
  const id = Number(value);
  if (!/^[1-9]\d*$/.test(value) || !Number.isSafeInteger(id))
    throw new Error("Invalid ID");
  return id;
}

export async function listRooms(
  branch: string,
  page: number,
  status: string,
  search: string,
  signal?: AbortSignal,
): Promise<RoomDirectoryResponse> {
  const branchId = positiveId(branch);
  if (
    !Number.isSafeInteger(page) ||
    page < 1 ||
    (status &&
      !roomStatusCodes.includes(status as (typeof roomStatusCodes)[number])) ||
    search.length > 200
  )
    throw new Error("Invalid filter");
  const query = new URLSearchParams({
    branchId: String(branchId),
    page: String(page),
    pageSize: "20",
    sort: "floor",
  });
  if (status) query.set("operationalStatusCode", status);
  if (search.trim()) query.set("search", search.trim());
  const data = (await getJson(
    `/rooms?${query}`,
    signal,
  )) as RoomDirectoryResponse;
  const validId = (id: unknown) =>
    typeof id === "number" && Number.isSafeInteger(id) && id > 0;
  const reference = (ref: RoomDirectoryItem["roomType"]) =>
    ref &&
    validId(ref.id) &&
    typeof ref.name === "string" &&
    typeof ref.code === "string";
  if (
    !data ||
    !Array.isArray(data.items) ||
    data.page !== page ||
    data.pageSize !== 20 ||
    !Number.isSafeInteger(data.totalCount) ||
    data.totalCount < 0 ||
    data.items.length > 20 ||
    data.items.some(
      (room) =>
        !room ||
        !validId(room.id) ||
        room.branchId !== branchId ||
        typeof room.roomNumber !== "string" ||
        !reference(room.floor) ||
        !reference(room.roomType) ||
        (room.area !== null && !reference(room.area)) ||
        typeof room.operationalStatusCode !== "string" ||
        typeof room.isActive !== "boolean",
    )
  )
    throw new ApiError(502, "INVALID_RESPONSE");
  return data;
}
