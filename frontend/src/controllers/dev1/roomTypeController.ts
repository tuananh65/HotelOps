import type * as D from "./contracts";
import {
  id,
  queryPath,
  pick,
  read,
  readVersioned,
  write,
  match,
  type CallOptions,
  type Versioned,
} from "./transportController";
const path = (b: number, n: number) =>
  queryPath(`/api/room-types/${id(n)}`, { branchId: Number(id(b)) });
export const roomTypeController = {
  list: (query: D.RoomTypeQuery = {}, o?: CallOptions) =>
    read<D.RoomTypePage>(queryPath("/api/room-types", query), o),
  detail: (b: number, n: number, o?: CallOptions) =>
    readVersioned<D.RoomTypeDetailsDto>(path(b, n), o),
  create: (input: D.CreateRoomTypeRequest, o?: CallOptions) =>
    write<D.CreatedRoomTypeDto>(
      "/api/room-types",
      "POST",
      pick(input, [
        "branchId",
        "code",
        "name",
        "standardCleaningMinutes",
        "defaultChecklistId",
        "description",
        "standardCapacity",
      ]),
      o,
    ),
  update: (
    b: number,
    n: number,
    v: Versioned<D.RoomTypeDetailsDto>,
    input: D.UpdateRoomTypeRequest,
    o?: CallOptions,
  ) =>
    write<D.RoomTypeDetailsDto>(
      path(b, n),
      "PUT",
      pick(input, [
        "name",
        "standardCleaningMinutes",
        "defaultChecklistId",
        "description",
        "code",
      ]),
      o,
      match(v, path(b, n)),
    ),
  deactivate: (
    b: number,
    n: number,
    v: Versioned<D.RoomTypeDetailsDto>,
    input: D.DeactivateRoomTypeRequest,
    o?: CallOptions,
  ) =>
    write<D.RoomTypeDetailsDto>(
      queryPath(`/api/room-types/${id(n)}/deactivate`, {
        branchId: Number(id(b)),
      }),
      "POST",
      pick(input, ["confirmation", "reason"]),
      o,
      match(v, path(b, n)),
    ),
};
