import type * as D from "./contracts";
import {
  branchPath,
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
const path = (b: number, kind: "floors" | "areas", n: number) =>
  `${branchPath(b)}/${kind}/${id(n)}`;
export const hotelStructureController = {
  structure: (b: number, query: D.BranchStructureQuery = {}, o?: CallOptions) =>
    read<D.BranchStructureDto>(
      queryPath(`${branchPath(b)}/structure`, query),
      o,
    ),
  createFloor: (b: number, input: D.CreateFloorRequest, o?: CallOptions) =>
    write<D.CreatedFloorDto>(
      `${branchPath(b)}/floors`,
      "POST",
      pick(input, ["code", "name", "floorNumber", "description"]),
      o,
    ),
  createArea: (b: number, input: D.CreateAreaRequest, o?: CallOptions) =>
    write<D.CreatedAreaDto>(
      `${branchPath(b)}/areas`,
      "POST",
      pick(input, ["code", "name", "floorId", "description"]),
      o,
    ),
  floor: (b: number, n: number, o?: CallOptions) =>
    readVersioned<D.EditableStructureDto>(path(b, "floors", n), o),
  area: (b: number, n: number, o?: CallOptions) =>
    readVersioned<D.EditableStructureDto>(path(b, "areas", n), o),
  updateFloor: (
    b: number,
    n: number,
    v: Versioned<D.EditableStructureDto>,
    input: D.UpdateFloorRequest,
    o?: CallOptions,
  ) =>
    write<D.EditableStructureDto>(
      path(b, "floors", n),
      "PUT",
      pick(input, ["code", "name", "reason", "description"]),
      o,
      match(v, path(b, "floors", n)),
    ),
  updateArea: (
    b: number,
    n: number,
    v: Versioned<D.EditableStructureDto>,
    input: D.UpdateAreaRequest,
    o?: CallOptions,
  ) =>
    write<D.EditableStructureDto>(
      path(b, "areas", n),
      "PUT",
      pick(input, ["code", "name", "reason", "floorId", "description"]),
      o,
      match(v, path(b, "areas", n)),
    ),
};
