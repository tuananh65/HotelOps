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
const path = (b: number, n: number) =>
  `${branchPath(b)}/inspection-checklists/${id(n)}`;
const criteria = (items: D.ChecklistCriterionRequest[]) =>
  items.map((i) =>
    pick(i, ["text", "group", "mandatory", "passCondition", "description"]),
  );
export const checklistController = {
  // Backend has no list-all checklist endpoint. Do not substitute mock data.
  detail: (b: number, n: number, o?: CallOptions) =>
    readVersioned<D.ChecklistDetailsDto>(path(b, n), o),
  create: (b: number, input: D.CreateChecklistRequest, o?: CallOptions) =>
    write<D.ChecklistDetailsDto>(
      `${branchPath(b)}/inspection-checklists`,
      "POST",
      {
        ...pick(input, ["code", "name", "roomTypeIds", "description"]),
        criteria: criteria(input.criteria),
      },
      o,
    ),
  update: (
    b: number,
    n: number,
    v: Versioned<D.ChecklistDetailsDto>,
    input: D.UpdateChecklistRequest,
    o?: CallOptions,
  ) =>
    write<D.ChecklistDetailsDto>(
      path(b, n),
      "PUT",
      { reason: input.reason, criteria: criteria(input.criteria) },
      o,
      match(v, path(b, n)),
    ),
  publish: (
    b: number,
    n: number,
    v: Versioned<D.ChecklistDetailsDto>,
    input: D.ChecklistTransitionRequest,
    o?: CallOptions,
  ) =>
    write<D.ChecklistDetailsDto>(
      `${path(b, n)}/publish`,
      "POST",
      pick(input, ["reason", "confirmation"]),
      o,
      match(v, path(b, n)),
    ),
  retire: (
    b: number,
    n: number,
    v: Versioned<D.ChecklistDetailsDto>,
    input: D.ChecklistTransitionRequest,
    o?: CallOptions,
  ) =>
    write<D.ChecklistDetailsDto>(
      `${path(b, n)}/retire`,
      "POST",
      pick(input, ["reason", "confirmation"]),
      o,
      match(v, path(b, n)),
    ),
};
