import type * as D from "./contracts";
import {
  branchPath,
  id,
  pick,
  readVersioned,
  write,
  match,
  type CallOptions,
  type Versioned,
} from "./transportController";
import { hotelStructureController } from "./hotelStructureController";
const path = (b: number, n: number) => `${branchPath(b)}/departments/${id(n)}`;
export const departmentController = {
  // Return the native structure envelope; null departments is not fabricated into an empty page.
  list: (b: number, query: D.PageQuery = {}, o?: CallOptions) =>
    hotelStructureController.structure(
      b,
      { ...query, nodeType: "branch", collection: "departments" },
      o,
    ),
  detail: (b: number, n: number, o?: CallOptions) =>
    readVersioned<D.DepartmentInformationDto>(path(b, n), o),
  create: (b: number, input: D.CreateDepartmentRequest, o?: CallOptions) =>
    write<D.DepartmentInformationDto>(
      `${branchPath(b)}/departments`,
      "POST",
      pick(input, ["code", "name", "description", "managerUserId"]),
      o,
    ),
  update: (
    b: number,
    n: number,
    v: Versioned<D.DepartmentInformationDto>,
    input: D.UpdateDepartmentRequest,
    o?: CallOptions,
  ) =>
    write<D.DepartmentInformationDto>(
      path(b, n),
      "PUT",
      pick(input, ["name", "description", "managerUserId"]),
      o,
      match(v, path(b, n)),
    ),
};
