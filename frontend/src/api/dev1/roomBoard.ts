import { ApiError, getJson, errorMessage } from "./client";
import {
  listRooms,
  positiveId,
  type RoomDirectoryItem,
  type RoomDirectoryResponse,
} from "./rooms";

export interface RoomSource {
  type: string | null;
  id: number | null;
  availability: string;
}
export interface RoomDetail {
  room: RoomDirectoryItem;
  description: string | null;
  currentStatusEvidence: {
    availability: string;
    reason: string | null;
    recordedAtUtc: string | null;
    source: RoomSource | null;
  };
  unreleasedBlocks: {
    id: number;
    reason: string;
    blockedAtUtc: string;
    expectedReleaseAtUtc: string | null;
  }[];
}
export interface RoomHistory {
  items: {
    id: number;
    fromStatusCode: string | null;
    toStatusCode: string;
    changedAtUtc: string;
    reason: string | null;
    source: RoomSource;
  }[];
  page: number;
  pageSize: number;
  totalCount: number;
  coverage: string;
}
const id = (v: unknown): v is number =>
  typeof v === "number" && Number.isSafeInteger(v) && v > 0;
const count = (v: unknown) =>
  typeof v === "number" && Number.isSafeInteger(v) && v >= 0;
const text = (v: unknown) => v === null || typeof v === "string";
const utc = (v: unknown) => {
  if (
    typeof v !== "string" ||
    !/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?Z$/.test(v)
  )
    return false;
  const parsed = new Date(v);
  return (
    Number.isFinite(parsed.getTime()) &&
    parsed.toISOString().slice(0, 19) === v.slice(0, 19)
  );
};
const optionalUtc = (v: unknown) => v === null || utc(v);
const source = (v: RoomSource) =>
  v &&
  text(v.type) &&
  (v.availability === "VERIFIED_SAME_ROOM"
    ? id(v.id) &&
      ["CLEANING_TASK", "INSPECTION", "ROOM_BLOCK"].includes(v.type || "")
    : ["NOT_RECORDED", "UNRESOLVED"].includes(v.availability) && v.id === null);
function pageFits(
  items: { id: number }[],
  total: number,
  page: number,
): boolean {
  return (
    new Set(items.map((item) => item.id)).size === items.length &&
    items.length <= Math.max(0, total - (page - 1) * 20)
  );
}
function invalid(): never {
  throw new ApiError(502, "INVALID_ROOM_RESPONSE");
}
export function roomPage(value: string | null): number {
  const page = value === null ? 1 : positiveId(value);
  if ((page - 1) * 20 > 2147483647) throw new Error("Invalid page");
  return page;
}
export async function listRoomBoard(
  params: URLSearchParams,
  signal?: AbortSignal,
): Promise<RoomDirectoryResponse> {
  const result = await listRooms(
    params.get("branchId") || "",
    roomPage(params.get("page")),
    params.get("status") || "",
    params.get("search") || "",
    signal,
    {
      floorId: params.get("floorId") || undefined,
      areaId: params.get("areaId") || undefined,
      roomTypeId: params.get("roomTypeId") || undefined,
    },
  );
  const counts = result.statusCounts;
  if (
    !Array.isArray(counts) ||
    counts.some(
      (c) =>
        !c || typeof c.operationalStatusCode !== "string" || !count(c.count),
    ) ||
    new Set(counts.map((c) => c.operationalStatusCode)).size !==
      counts.length ||
    counts.reduce((sum, c) => sum + c.count, 0) !== result.totalCount
  )
    invalid();
  const status = params.get("status");
  if (
    !pageFits(result.items, result.totalCount, result.page) ||
    counts.some((c) => status && c.operationalStatusCode !== status) ||
    result.items.some(
      (r) =>
        (status && r.operationalStatusCode !== status) ||
        (params.get("floorId") &&
          r.floor.id !== positiveId(params.get("floorId")!)) ||
        (params.get("areaId") &&
          r.area?.id !== positiveId(params.get("areaId")!)) ||
        (params.get("roomTypeId") &&
          r.roomType.id !== positiveId(params.get("roomTypeId")!)),
    )
  )
    invalid();
  const pageCounts = new Map<string, number>();
  for (const room of result.items)
    pageCounts.set(
      room.operationalStatusCode,
      (pageCounts.get(room.operationalStatusCode) || 0) + 1,
    );
  for (const [code, n] of pageCounts) {
    if ((counts.find((c) => c.operationalStatusCode === code)?.count || 0) < n)
      invalid();
  }
  return result;
}
function validRoom(
  room: RoomDirectoryItem,
  branch: number,
  roomId: number,
): boolean {
  const ref = (v: RoomDirectoryItem["roomType"]) =>
    v && id(v.id) && typeof v.code === "string" && typeof v.name === "string";
  return (
    !!room &&
    room.id === roomId &&
    room.branchId === branch &&
    typeof room.roomNumber === "string" &&
    typeof room.operationalStatusCode === "string" &&
    typeof room.isActive === "boolean" &&
    !!ref(room.floor) &&
    !!ref(room.roomType) &&
    (room.area === null || !!ref(room.area))
  );
}
export async function readRoomDetail(
  branch: string,
  room: string,
  signal?: AbortSignal,
): Promise<RoomDetail> {
  const branchId = positiveId(branch),
    roomId = positiveId(room);
  const data = (await getJson(
    `/rooms/${roomId}?branchId=${branchId}`,
    signal,
  )) as RoomDetail;
  const e = data?.currentStatusEvidence;
  if (
    !data ||
    !validRoom(data.room, branchId, roomId) ||
    !text(data.description) ||
    !e ||
    !["RECORDED", "NOT_RECORDED", "OUT_OF_SYNC"].includes(e.availability) ||
    (e.availability === "RECORDED"
      ? e.recordedAtUtc === null || e.source === null
      : e.reason !== null || e.recordedAtUtc !== null || e.source !== null) ||
    !text(e.reason) ||
    !optionalUtc(e.recordedAtUtc) ||
    (e.source !== null && !source(e.source)) ||
    !Array.isArray(data.unreleasedBlocks) ||
    data.unreleasedBlocks.some(
      (b) =>
        !b ||
        !id(b.id) ||
        typeof b.reason !== "string" ||
        !utc(b.blockedAtUtc) ||
        !optionalUtc(b.expectedReleaseAtUtc),
    )
  )
    invalid();
  if (
    new Set(data.unreleasedBlocks.map((b) => b.id)).size !==
    data.unreleasedBlocks.length
  )
    invalid();
  return data;
}
export async function readRoomHistory(
  branch: string,
  room: string,
  page: number,
  signal?: AbortSignal,
): Promise<RoomHistory> {
  const branchId = positiveId(branch),
    roomId = positiveId(room);
  roomPage(String(page));
  const data = (await getJson(
    `/rooms/${roomId}/history?branchId=${branchId}&page=${page}&pageSize=20`,
    signal,
  )) as RoomHistory;
  if (
    !data ||
    data.page !== page ||
    data.pageSize !== 20 ||
    !count(data.totalCount) ||
    data.coverage !== "RECORDED_STATUS_EVENTS_ONLY" ||
    !Array.isArray(data.items) ||
    data.items.length > 20 ||
    data.items.some(
      (e) =>
        !e ||
        !id(e.id) ||
        !text(e.fromStatusCode) ||
        typeof e.toStatusCode !== "string" ||
        !utc(e.changedAtUtc) ||
        !text(e.reason) ||
        !source(e.source),
    )
  )
    invalid();
  if (!pageFits(data.items, data.totalCount, page)) invalid();
  return data;
}
export function roomBoardError(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 404)
      return "Không tìm thấy phòng trong phạm vi chi nhánh.";
    if (error.status === 409)
      return "Dữ liệu phòng đã thay đổi. Tải lại để xem trạng thái mới nhất.";
    return errorMessage(error);
  }
  return "Bộ lọc không hợp lệ. Kiểm tra ID và số trang.";
}
