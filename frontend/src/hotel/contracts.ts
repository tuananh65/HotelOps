// DTOs mirror HotelStructure/RoomDirectory records. Decoders detach allowlisted fields.
export type Decoder<T> = (value: unknown) => T | undefined;
export type Decoded<D> = D extends Decoder<infer T> ? T : never;
export const object = (v: unknown): v is Record<string, unknown> =>
  typeof v === "object" && v !== null && !Array.isArray(v);
export const safeId: Decoder<number> = (v) =>
  typeof v === "number" && Number.isSafeInteger(v) && v > 0 ? v : undefined;
const count: Decoder<number> = (v) =>
  typeof v === "number" && Number.isSafeInteger(v) && v >= 0 ? v : undefined;
const integer: Decoder<number> = (v) =>
  typeof v === "number" &&
  Number.isSafeInteger(v) &&
  v >= -2147483648 &&
  v <= 2147483647
    ? v
    : undefined;
const text: Decoder<string> = (v) => (typeof v === "string" ? v : undefined);
const boolean: Decoder<boolean> = (v) =>
  typeof v === "boolean" ? v : undefined;
const nullable =
  <T>(decode: Decoder<T>): Decoder<T | null> =>
  (v) =>
    v === null ? null : decode(v);
const list =
  <T>(decode: Decoder<T>): Decoder<T[]> =>
  (v) => {
    if (!Array.isArray(v) || v.length > 10000) return;
    const result: T[] = [];
    for (const item of v) {
      const parsed = decode(item);
      if (parsed === undefined) return;
      result.push(parsed);
    }
    return result;
  };
function record<S extends Record<string, Decoder<unknown>>>(
  shape: S,
): Decoder<{ [K in keyof S]: Decoded<S[K]> }> {
  return (v) => {
    if (!object(v)) return;
    const output: Record<string, unknown> = {};
    for (const [key, decode] of Object.entries(shape)) {
      const value = decode(v[key]);
      if (value === undefined) return;
      output[key] = value;
    }
    return output as { [K in keyof S]: Decoded<S[K]> };
  };
}
const identity = { id: safeId, code: text, name: text };
const location = {
  ...identity,
  branchId: safeId,
  description: nullable(text),
  isActive: boolean,
};
export const floorDto = record({ ...location, floorNumber: nullable(integer) });
export const areaDto = record({ ...location, floorId: nullable(safeId) });
export const editableStructureDto = record({
  ...location,
  kind: text,
  floorId: nullable(safeId),
  floorNumber: nullable(integer),
});
export const departmentDto = record({
  ...location,
  managerUserId: nullable(safeId),
});
const structureDepartment = record({
  ...identity,
  branchId: safeId,
  isActive: boolean,
});
const structureRoom = record({
  id: safeId,
  branchId: safeId,
  floorId: safeId,
  areaId: nullable(safeId),
  roomNumber: text,
  isActive: boolean,
});
const page = <T>(item: Decoder<T>) =>
  record({
    items: list(item),
    page: safeId,
    pageSize: safeId,
    totalCount: count,
  });
export const structureDto = record({
  branch: record({ ...identity, isActive: boolean }),
  visibility: text,
  nodeType: text,
  collection: nullable(text),
  availableCollections: list(text),
  isActive: nullable(boolean),
  selectedFloor: nullable(floorDto),
  selectedArea: nullable(areaDto),
  selectedRoom: nullable(structureRoom),
  selectedDepartment: nullable(structureDepartment),
  floors: nullable(page(floorDto)),
  areas: nullable(page(areaDto)),
  rooms: nullable(page(structureRoom)),
  departments: nullable(page(structureDepartment)),
});
export const roomTypeDto = record({
  ...identity,
  branchId: nullable(safeId),
  description: nullable(text),
  standardCleaningMinutes: nullable(safeId),
  defaultChecklistId: nullable(safeId),
  standardCapacity: nullable(safeId),
  isActive: boolean,
});
export const roomTypeItemDto = record({
  details: roomTypeDto,
  activeRooms: count,
  canUpdate: boolean,
});
export const roomTypesDto = record({
  items: list(roomTypeItemDto),
  total: count,
  page: safeId,
  pageSize: safeId,
  branchId: nullable(safeId),
  isActive: nullable(boolean),
  search: nullable(text),
  sort: text,
  descending: boolean,
});
export const roomDto = record({
  id: safeId,
  branchId: safeId,
  floorId: safeId,
  areaId: nullable(safeId),
  roomTypeId: safeId,
  roomNumber: text,
  notes: nullable(text),
  operationalStatus: text,
  isActive: boolean,
});
const roomDirectoryItem = record({
  id: safeId,
  branchId: safeId,
  roomNumber: text,
  floor: record({ ...identity, number: nullable(integer) }),
  area: nullable(record(identity)),
  roomType: record(identity),
  operationalStatusCode: text,
  isActive: boolean,
});
export const roomsDto = page(roomDirectoryItem);
const sourceDto = record({
  type: nullable(text),
  id: nullable(safeId),
  availability: text,
});
export const roomDetailDto = record({
  room: roomDirectoryItem,
  description: nullable(text),
  currentStatusEvidence: record({
    availability: text,
    reason: nullable(text),
    recordedAtUtc: nullable(text),
    source: nullable(sourceDto),
  }),
  historyUrl: nullable(text),
});
export const roomHistoryDto = record({
  items: list(
    record({
      id: safeId,
      fromStatusCode: nullable(text),
      toStatusCode: text,
      changedAtUtc: text,
      reason: nullable(text),
      source: sourceDto,
    }),
  ),
  page: safeId,
  pageSize: safeId,
  totalCount: count,
  coverage: text,
});
export type StructureDto = Decoded<typeof structureDto>;
export type RoomTypeDto = Decoded<typeof roomTypeDto>;
export type RoomDto = Decoded<typeof roomDto>;
export type DepartmentDto = Decoded<typeof departmentDto>;
export interface Context {
  branchId: number;
  id?: number;
}
export interface Filters {
  allBranches?: boolean;
  search?: string;
  isActive?: string;
  page?: number;
  pageSize?: number;
  sort?: string;
  descending?: boolean;
  floorId?: number;
  areaId?: number;
  roomTypeId?: number;
  operationalStatusCode?: string;
  collection?: string;
  nodeType?: string;
  nodeId?: number;
}
export interface ReadTypes {
  structure: StructureDto;
  floor: Decoded<typeof editableStructureDto>;
  area: Decoded<typeof editableStructureDto>;
  roomType: Decoded<typeof roomTypeItemDto>;
  roomTypes: Decoded<typeof roomTypesDto>;
  roomEdit: RoomDto;
  rooms: Decoded<typeof roomsDto>;
  roomDetail: Decoded<typeof roomDetailDto>;
  roomHistory: Decoded<typeof roomHistoryDto>;
  department: DepartmentDto;
}
export type ReadKind = keyof ReadTypes;
export interface FloorRequest {
  code: string;
  name: string;
  floorNumber: number | null;
  description: string | null;
}
export interface AreaRequest {
  code: string;
  name: string;
  floorId: number | null;
  description: string | null;
}
export interface CreateTypeRequest {
  branchId: number;
  code: string;
  name: string;
  standardCleaningMinutes: number;
  defaultChecklistId: number;
  description: string | null;
  standardCapacity: number | null;
}
export interface UpdateTypeRequest {
  name: string;
  standardCleaningMinutes: number;
  defaultChecklistId: number;
  description: string | null;
  code?: string | null;
}
export interface CreateRoomRequest {
  branchId: number;
  floorId: number;
  roomTypeId: number;
  roomNumber: string;
  areaId: number | null;
  notes: string | null;
}
export interface MutationRequests {
  createFloor: FloorRequest;
  createArea: AreaRequest;
  updateFloor: {
    code: string;
    name: string;
    reason: string;
    description: string | null;
  };
  updateArea: AreaRequest & { reason: string };
  createType: CreateTypeRequest;
  updateType: UpdateTypeRequest;
  deactivateType: { confirmation: boolean; reason: string };
  createRoom: CreateRoomRequest;
  updateRoom: Omit<CreateRoomRequest, "branchId"> & { reason: string };
  createDepartment: {
    code: string;
    name: string;
    description: string | null;
    managerUserId: number | null;
  };
  updateDepartment: {
    name: string;
    description: string | null;
    managerUserId: number | null;
  };
}
export type MutationKind = keyof MutationRequests;
export interface MutationTypes {
  createFloor: Decoded<typeof floorDto>;
  createArea: Decoded<typeof areaDto>;
  updateFloor: Decoded<typeof editableStructureDto>;
  updateArea: Decoded<typeof editableStructureDto>;
  createType: RoomTypeDto;
  updateType: RoomTypeDto;
  deactivateType: RoomTypeDto;
  createRoom: RoomDto;
  updateRoom: RoomDto;
  createDepartment: DepartmentDto;
  updateDepartment: DepartmentDto;
}
export interface HotelError {
  activeRooms?: { id: number; roomNumber: string }[];
  kind:
    | "invalid-request"
    | "http"
    | "network"
    | "timeout"
    | "cancelled"
    | "invalid-response";
  status?: number;
  reason?:
    | "PASSWORD_CHANGE_REQUIRED"
    | "ACTIVE_ROOMS_EXIST"
    | "ROOM_TYPE_SCHEMA_REQUIRED";
}
export type ReadResult<T> =
  { ok: true; data: T; etag: string | null } | { ok: false; error: HotelError };
export type MutationResult<T> =
  | { outcome: "CONFIRMED"; data: T; etag: string | null }
  | {
      outcome: "NOT_DISPATCHED" | "REJECTED" | "OUTCOME_UNKNOWN";
      error: HotelError;
    };
export const strongTag = (v: unknown): v is string =>
  typeof v === "string" && /^"[0-9A-F]{64}"$/.test(v) && v.length === 66;
export function decimalId(value: string): number | null {
  const n = value && !/[^0-9]/.test(value) ? Number(value) : NaN;
  return safeId(n) ?? null;
}
