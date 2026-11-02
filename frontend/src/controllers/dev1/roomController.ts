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
import { ApiError } from "../../api/http";
const path = (b: number, n: number, suffix = "") =>
  queryPath(`/api/rooms/${id(n)}${suffix}`, { branchId: Number(id(b)) });
export const roomController = {
  list: (query: D.RoomDirectoryQuery, o?: CallOptions) => {
    if (
      query.allBranches === true
        ? query.branchId !== undefined
        : !query.branchId
    )
      throw new ApiError(
        "Choose one branch or explicit allBranches.",
        400,
        "validation",
      );
    if (query.branchId !== undefined) id(query.branchId);
    return read<D.RoomDirectoryResponse>(queryPath("/api/rooms", query), o);
  },
  detail: (b: number, n: number, o?: CallOptions) =>
    read<D.RoomDirectoryDetailDto>(path(b, n), o),
  edit: (b: number, n: number, o?: CallOptions) =>
    readVersioned<D.CreatedRoomDto>(path(b, n, "/edit"), o),
  history: (b: number, n: number, query: D.PageQuery = {}, o?: CallOptions) =>
    read<D.RoomDirectoryHistoryDto>(
      queryPath(`/api/rooms/${id(n)}/history`, {
        ...query,
        branchId: Number(id(b)),
      }),
      o,
    ),
  create: (input: D.CreateRoomRequest, o?: CallOptions) =>
    write<D.CreatedRoomDto>(
      "/api/rooms",
      "POST",
      pick(input, [
        "branchId",
        "floorId",
        "roomTypeId",
        "roomNumber",
        "areaId",
        "notes",
      ]),
      o,
    ),
  update: (
    b: number,
    n: number,
    v: Versioned<D.CreatedRoomDto>,
    input: D.UpdateRoomInformationRequest,
    o?: CallOptions,
  ) =>
    write<D.CreatedRoomDto>(
      path(b, n),
      "PUT",
      pick(input, [
        "floorId",
        "roomTypeId",
        "roomNumber",
        "reason",
        "areaId",
        "notes",
      ]),
      o,
      match(v, path(b, n, "/edit")),
    ),
};
