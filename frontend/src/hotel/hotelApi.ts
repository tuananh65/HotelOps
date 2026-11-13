import {
  areaDto,
  departmentDto,
  editableStructureDto,
  floorDto,
  object,
  roomDetailDto,
  roomHistoryDto,
  roomDto,
  roomsDto,
  roomTypeDto,
  roomTypeItemDto,
  roomTypesDto,
  safeId,
  strongTag,
  structureDto,
} from "./contracts";
import type {
  Context,
  Decoder,
  Filters,
  HotelError,
  MutationKind,
  MutationRequests,
  MutationResult,
  MutationTypes,
  ReadKind,
  ReadResult,
  ReadTypes,
} from "./contracts";

const reads = {
  structure: structureDto,
  floor: editableStructureDto,
  area: editableStructureDto,
  roomType: roomTypeItemDto,
  roomTypes: roomTypesDto,
  roomEdit: roomDto,
  rooms: roomsDto,
  roomDetail: roomDetailDto,
  roomHistory: roomHistoryDto,
  department: departmentDto,
};
const mutations = {
  createFloor: floorDto,
  createArea: areaDto,
  updateFloor: editableStructureDto,
  updateArea: editableStructureDto,
  createType: roomTypeDto,
  updateType: roomTypeDto,
  deactivateType: roomTypeDto,
  createRoom: roomDto,
  updateRoom: roomDto,
  createDepartment: departmentDto,
  updateDepartment: departmentDto,
};
const readNeedsTag = (kind: ReadKind) =>
  ["floor", "area", "roomType", "roomEdit", "department"].includes(kind);
const update = (kind: MutationKind) =>
  kind.startsWith("update") || kind === "deactivateType";
function validContext(c: Context, needsId: boolean) {
  return (
    safeId(c.branchId) !== undefined && (!needsId || safeId(c.id) !== undefined)
  );
}
function readPath(kind: ReadKind, c: Context, f: Filters) {
  const q = new URLSearchParams();
  const add = (key: string, value: unknown) => {
    if (value !== undefined && value !== "") q.set(key, String(value));
  };
  let path: string;
  switch (kind) {
    case "structure":
      path = `/api/branches/${c.branchId}/structure`;
      for (const k of [
        "nodeType",
        "nodeId",
        "collection",
        "isActive",
        "page",
        "pageSize",
      ] as const)
        add(k, f[k]);
      break;
    case "floor":
    case "area":
      path = `/api/branches/${c.branchId}/${kind}s/${c.id}`;
      break;
    case "department":
      path = `/api/branches/${c.branchId}/departments/${c.id}`;
      break;
    case "roomType":
      path = `/api/room-types/${c.id}`;
      add("branchId", c.branchId);
      break;
    case "roomEdit":
      path = `/api/rooms/${c.id}/edit`;
      add("branchId", c.branchId);
      break;
    case "roomDetail":
      path = `/api/rooms/${c.id}`;
      add("branchId", c.branchId);
      break;
    case "roomHistory":
      path = `/api/rooms/${c.id}/history`;
      add("branchId", c.branchId);
      add("page", f.page);
      add("pageSize", f.pageSize);
      break;
    default:
      path = kind === "rooms" ? "/api/rooms" : "/api/room-types";
      if (f.allBranches && c.branchId === 0) {
        if (kind === "rooms") add("allBranches", true);
      } else add("branchId", c.branchId);
      for (const k of [
        "search",
        "isActive",
        "page",
        "pageSize",
        "sort",
        "descending",
      ] as const)
        add(k, f[k]);
      if (kind === "rooms")
        for (const k of [
          "floorId",
          "areaId",
          "roomTypeId",
          "operationalStatusCode",
        ] as const)
          add(k, f[k]);
  }
  return path + (q.size ? `?${q}` : "");
}
function mutationPath(kind: MutationKind, c: Context) {
  switch (kind) {
    case "createFloor":
      return `/api/branches/${c.branchId}/floors`;
    case "createArea":
      return `/api/branches/${c.branchId}/areas`;
    case "updateFloor":
      return `/api/branches/${c.branchId}/floors/${c.id}`;
    case "updateArea":
      return `/api/branches/${c.branchId}/areas/${c.id}`;
    case "createType":
      return "/api/room-types";
    case "updateType":
      return `/api/room-types/${c.id}?branchId=${c.branchId}`;
    case "deactivateType":
      return `/api/room-types/${c.id}/deactivate?branchId=${c.branchId}`;
    case "createRoom":
      return "/api/rooms";
    case "updateRoom":
      return `/api/rooms/${c.id}?branchId=${c.branchId}`;
    case "createDepartment":
      return `/api/branches/${c.branchId}/departments`;
    case "updateDepartment":
      return `/api/branches/${c.branchId}/departments/${c.id}`;
  }
}
async function json(response: Response): Promise<unknown> {
  const type = response.headers
    .get("Content-Type")
    ?.split(";")[0]
    .trim()
    .toLowerCase();
  if (type !== "application/json" && type !== "application/problem+json")
    return;
  const body = await response.text();
  try {
    return JSON.parse(body);
  } catch {
    return;
  }
}
function httpError(status: number, body: unknown): HotelError {
  const p =
    object(body) && (body.status === undefined || body.status === status)
      ? body
      : {};
  const hasCode = Object.hasOwn(p, "code"),
    hasTitle = Object.hasOwn(p, "title");
  const identifier =
    hasCode && hasTitle && p.code !== p.title
      ? undefined
      : hasCode
        ? p.code
        : p.title;
  const reason = [
    "PASSWORD_CHANGE_REQUIRED",
    "ACTIVE_ROOMS_EXIST",
    "ROOM_TYPE_SCHEMA_REQUIRED",
  ].find((v) => v === identifier) as HotelError["reason"];
  const activeRooms =
    status === 409 &&
    reason === "ACTIVE_ROOMS_EXIST" &&
    Array.isArray(p.rooms) &&
    p.rooms.length <= 10000 &&
    p.rooms.every(
      (v) =>
        object(v) &&
        safeId(v.id) !== undefined &&
        typeof v.roomNumber === "string" &&
        v.roomNumber.length <= 30,
    )
      ? p.rooms.map((v) => ({
          id: v.id as number,
          roomNumber: v.roomNumber as string,
        }))
      : undefined;
  return {
    kind: "http",
    status,
    ...(reason ? { reason } : {}),
    ...(activeRooms ? { activeRooms } : {}),
  };
}
function caught(e: unknown, signal?: AbortSignal, status?: number): HotelError {
  return {
    ...(status !== undefined && status >= 400 && status <= 599
      ? { status }
      : {}),
    kind:
      signal?.aborted || (object(e) && e.name === "AbortError")
        ? "cancelled"
        : object(e) && e.name === "TimeoutError"
          ? "timeout"
          : "network",
  };
}
// Recursive branch binding on DTO fields; room-type legacy globals may have branchId=null.
function scoped(value: unknown, c: Context): boolean {
  if (Array.isArray(value)) return value.every((v) => scoped(v, c));
  if (!object(value)) return true;
  if (
    value.branchId !== undefined &&
    value.branchId !== null &&
    value.branchId !== c.branchId
  )
    return false;
  return Object.values(value).every((v) => scoped(v, c));
}
export function createHotelApi(
  fetchImplementation: typeof fetch = globalThis.fetch.bind(globalThis),
) {
  const options = (signal?: AbortSignal) =>
    ({
      credentials: "include",
      cache: "no-store",
      redirect: "error",
      signal,
    }) as const;
  return {
    async read<K extends ReadKind>(
      kind: K,
      context: Context,
      filters: Filters = {},
      signal?: AbortSignal,
    ): Promise<ReadResult<ReadTypes[K]>> {
      const invalid = (): ReadResult<ReadTypes[K]> => ({
        ok: false,
        error: { kind: "invalid-response" },
      });
      const globalList =
        (kind === "rooms" || kind === "roomTypes") &&
        filters.allBranches === true &&
        context.branchId === 0;
      if (
        !Object.hasOwn(reads, kind) ||
        (!globalList &&
          !validContext(
            context,
            !["structure", "rooms", "roomTypes"].includes(kind),
          ))
      )
        return { ok: false, error: { kind: "invalid-request" } };
      if (filters.allBranches && !globalList)
        return { ok: false, error: { kind: "invalid-request" } };
      context = {
        branchId: context.branchId,
        ...(context.id === undefined ? {} : { id: context.id }),
      };
      if (
        (filters.page !== undefined && safeId(filters.page) === undefined) ||
        (filters.pageSize !== undefined &&
          (safeId(filters.pageSize) === undefined || filters.pageSize > 100))
      )
        return { ok: false, error: { kind: "invalid-request" } };
      let observedStatus: number | undefined;
      try {
        if (signal?.aborted) return { ok: false, error: { kind: "cancelled" } };
        const r = await fetchImplementation(readPath(kind, context, filters), {
          ...options(signal),
          method: "GET",
          headers: { Accept: "application/json" },
        });
        observedStatus = r.status;
        const body = await json(r);
        if (signal?.aborted) return { ok: false, error: { kind: "cancelled" } };
        if (r.status !== 200)
          return { ok: false, error: httpError(r.status, body) };
        const value = (reads[kind] as Decoder<ReadTypes[K]>)(body),
          etag = r.headers.get("ETag");
        if (
          !value ||
          (!globalList && !scoped(value, context)) ||
          (readNeedsTag(kind) && !strongTag(etag))
        )
          return invalid();
        const candidate: unknown = value;
        const identity =
          kind === "roomType"
            ? (value as ReadTypes["roomType"]).details.id
            : kind === "roomDetail"
              ? (value as ReadTypes["roomDetail"]).room.id
              : object(candidate)
                ? candidate.id
                : undefined;
        if (
          context.id !== undefined &&
          readNeedsTag(kind) &&
          identity !== context.id
        )
          return invalid();
        if (kind === "roomDetail" && identity !== context.id) return invalid();
        if (
          kind === "structure" &&
          (value as ReadTypes["structure"]).branch.id !== context.branchId
        )
          return invalid();
        if (kind === "structure") {
          const data = value as ReadTypes["structure"];
          if (data.nodeType !== (filters.nodeType ?? "branch"))
            return invalid();
          const node =
            data.nodeType === "floor"
              ? data.selectedFloor
              : data.nodeType === "area"
                ? data.selectedArea
                : data.nodeType === "room"
                  ? data.selectedRoom
                  : data.nodeType === "department"
                    ? data.selectedDepartment
                    : null;
          if (data.nodeType !== "branch" && node?.id !== filters.nodeId)
            return invalid();
        }
        if (
          (kind === "floor" || kind === "area") &&
          (value as ReadTypes["floor"]).kind !== kind
        )
          return invalid();
        return {
          ok: true,
          data: value,
          etag: readNeedsTag(kind) ? etag : null,
        };
      } catch (e) {
        return { ok: false, error: caught(e, signal, observedStatus) };
      }
    },
    async mutate<K extends MutationKind>(
      kind: K,
      context: Context,
      request: MutationRequests[K],
      etag?: string | null,
      signal?: AbortSignal,
    ): Promise<MutationResult<MutationTypes[K]>> {
      let dispatched = false;
      let observedStatus: number | undefined;
      const fail = (error: HotelError): MutationResult<MutationTypes[K]> => ({
        outcome: dispatched ? "OUTCOME_UNKNOWN" : "NOT_DISPATCHED",
        error,
      });
      if (
        !Object.hasOwn(mutations, kind) ||
        !validContext(context, update(kind)) ||
        (update(kind) && !strongTag(etag))
      )
        return fail({ kind: "invalid-request" });
      context = {
        branchId: context.branchId,
        ...(context.id === undefined ? {} : { id: context.id }),
      };
      // Freeze and whitelist before the first await. No caller serialization hooks.
      const keys: Record<MutationKind, string[]> = {
        createFloor: ["code", "name", "floorNumber", "description"],
        createArea: ["code", "name", "floorId", "description"],
        updateFloor: ["code", "name", "reason", "description"],
        updateArea: ["code", "name", "floorId", "reason", "description"],
        createType: [
          "branchId",
          "code",
          "name",
          "standardCleaningMinutes",
          "defaultChecklistId",
          "description",
          "standardCapacity",
        ],
        updateType: [
          "name",
          "standardCleaningMinutes",
          "defaultChecklistId",
          "description",
          "code",
        ],
        deactivateType: ["confirmation", "reason"],
        createRoom: [
          "branchId",
          "floorId",
          "roomTypeId",
          "roomNumber",
          "areaId",
          "notes",
        ],
        updateRoom: [
          "floorId",
          "roomTypeId",
          "roomNumber",
          "areaId",
          "notes",
          "reason",
        ],
        createDepartment: ["code", "name", "description", "managerUserId"],
        updateDepartment: ["name", "description", "managerUserId"],
      };
      if (!object(request)) return fail({ kind: "invalid-request" });
      const copied: Record<string, unknown> = {};
      for (const key of Object.keys(request)) {
        if (!keys[kind].includes(key)) return fail({ kind: "invalid-request" });
        const descriptor = Object.getOwnPropertyDescriptor(request, key);
        if (
          !descriptor ||
          !("value" in descriptor) ||
          (!["string", "number", "boolean"].includes(typeof descriptor.value) &&
            descriptor.value !== null)
        )
          return fail({ kind: "invalid-request" });
        copied[key] = descriptor.value;
      }
      if (copied.branchId !== undefined && copied.branchId !== context.branchId)
        return fail({ kind: "invalid-request" });
      const body = JSON.stringify(copied);
      if (new TextEncoder().encode(body).length > 32768)
        return fail({ kind: "invalid-request" });
      try {
        if (signal?.aborted) return fail({ kind: "cancelled" });
        const bootstrap = await fetchImplementation("/api/auth/csrf", {
          ...options(signal),
          method: "GET",
          headers: { Accept: "application/json" },
        });
        observedStatus = bootstrap.status;
        const csrf = await json(bootstrap);
        if (signal?.aborted) return fail({ kind: "cancelled" });
        if (bootstrap.status !== 200)
          return fail(httpError(bootstrap.status, csrf));
        if (
          !object(csrf) ||
          csrf.headerName !== "X-CSRF-TOKEN" ||
          typeof csrf.csrfToken !== "string" ||
          !/^[\x21-\x7e]+$/.test(csrf.csrfToken)
        )
          return fail({ kind: "invalid-response" });
        const headers = new Headers({
          Accept: "application/json",
          "Content-Type": "application/json",
          "X-CSRF-TOKEN": csrf.csrfToken,
        });
        if (update(kind)) headers.set("If-Match", etag!);
        if (signal?.aborted) return fail({ kind: "cancelled" });
        observedStatus = undefined;
        dispatched = true;
        const r = await fetchImplementation(mutationPath(kind, context), {
          ...options(signal),
          method: kind.startsWith("update") ? "PUT" : "POST",
          headers,
          body,
        });
        observedStatus = r.status;
        const value = await json(r);
        if (signal?.aborted) return fail({ kind: "cancelled" });
        const expected = kind.startsWith("create") ? 201 : 200;
        if (r.status !== expected) {
          const error = httpError(r.status, value);
          const p =
            object(value) &&
            (value.status === undefined || value.status === r.status)
              ? value
              : {};
          const identifier =
            Object.hasOwn(p, "code") &&
            Object.hasOwn(p, "title") &&
            p.code !== p.title
              ? undefined
              : (p.code ?? p.title);
          const knownConflict = [
            "STRUCTURE_CODE_EXISTS",
            "STRUCTURE_NOT_ACTIVE",
            "PARENT_CHANGE_CONFLICT",
            "ROOM_TYPE_CODE_EXISTS",
            "ACTIVE_ROOMS_EXIST",
            "DUPLICATE",
            "INACTIVE",
          ].includes(String(identifier));
          return [400, 401, 403, 404, 412, 413, 415, 428].includes(r.status) ||
            (r.status === 409 && knownConflict)
            ? { outcome: "REJECTED", error }
            : fail(error);
        }
        const data = (mutations[kind] as Decoder<MutationTypes[K]>)(value),
          next = r.headers.get("ETag");
        if (
          !data ||
          !scoped(data, context) ||
          (update(kind) && (!strongTag(next) || next === etag)) ||
          (context.id !== undefined && update(kind) && data.id !== context.id)
        )
          return fail({ kind: "invalid-response" });
        return {
          outcome: "CONFIRMED",
          data,
          etag: strongTag(next) ? next : null,
        };
      } catch (e) {
        return fail(caught(e, signal, observedStatus));
      }
    },
  };
}
