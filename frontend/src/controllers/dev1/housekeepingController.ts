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
import { photos } from "./progressController";
const inspection = (b: number, n: number) =>
  `${branchPath(b)}/inspections/${id(n)}`;
const cleaning = (b: number, n: number) =>
  `${branchPath(b)}/cleaning-tasks/${id(n)}`;
const planning = (
  b: number,
  n: number,
  kind: "assignment" | "inspection",
  q: D.HousekeepingPlanningQuery = {},
) => queryPath(`${cleaning(b, n)}/${kind}-options`, q);
export const housekeepingController = {
  assignmentOptions: (
    b: number,
    n: number,
    q: D.HousekeepingPlanningQuery = {},
    o?: CallOptions,
  ) =>
    readVersioned<D.HousekeepingPlanningDto>(
      planning(b, n, "assignment", q),
      o,
    ),
  inspectionOptions: (
    b: number,
    n: number,
    q: D.HousekeepingPlanningQuery = {},
    o?: CallOptions,
  ) => read<D.HousekeepingPlanningDto>(planning(b, n, "inspection", q), o),
  assignCleaning: (
    b: number,
    n: number,
    v: Versioned<D.HousekeepingPlanningDto>,
    input: D.AssignCleaningRequest,
    q: D.HousekeepingPlanningQuery = {},
    o?: CallOptions,
  ) =>
    write<D.HousekeepingWriteDto>(
      `${cleaning(b, n)}/assign`,
      "POST",
      pick(input, ["assigneeId", "dueAt", "reason", "adoptUnassignedLegacy"]),
      o,
      match(v, planning(b, n, "assignment", q)),
    ),
  assignInspection: (
    b: number,
    n: number,
    input: D.AssignInspectionRequest,
    o?: CallOptions,
  ) =>
    write<D.HousekeepingWriteDto>(
      `${cleaning(b, n)}/inspections`,
      "POST",
      pick(input, ["inspectorId", "dueAt", "reason"]),
      o,
    ),
  execution: (b: number, n: number, o?: CallOptions) =>
    readVersioned<D.InspectionExecutionDto>(`${inspection(b, n)}/execution`, o),
  startInspection: (
    b: number,
    n: number,
    v: Versioned<D.InspectionExecutionDto>,
    reason: string,
    o?: CallOptions,
  ) =>
    write<D.InspectionExecutionDto>(
      `${inspection(b, n)}/start`,
      "POST",
      { reason },
      o,
      match(v, `${inspection(b, n)}/execution`),
    ),
  saveInspection: (
    b: number,
    n: number,
    v: Versioned<D.InspectionExecutionDto>,
    input: D.SaveInspectionRequest,
    o?: CallOptions,
  ) =>
    write<D.InspectionExecutionDto>(
      `${inspection(b, n)}/results`,
      "PUT",
      {
        reason: input.reason,
        results: input.results.map((i) => ({
          ...pick(i, ["itemId", "outcome", "note"]),
          photos: photos(i.photos),
        })),
      },
      o,
      match(v, `${inspection(b, n)}/execution`),
    ),
  finishInspection: (
    b: number,
    n: number,
    v: Versioned<D.InspectionExecutionDto>,
    reason: string,
    o?: CallOptions,
  ) =>
    write<D.InspectionExecutionDto>(
      `${inspection(b, n)}/finish`,
      "POST",
      { reason },
      o,
      match(v, `${inspection(b, n)}/execution`),
    ),
  recordDefect: (
    b: number,
    n: number,
    v: Versioned<D.InspectionExecutionDto>,
    input: D.RecordInspectionDefectRequest,
    o?: CallOptions,
  ) =>
    write<D.InspectionExecutionDto>(
      `${inspection(b, n)}/defects`,
      "POST",
      {
        ...pick(input, ["itemId", "severityId", "description", "location"]),
        photos: photos(input.photos),
      },
      o,
      match(v, `${inspection(b, n)}/execution`),
    ),
  requestRework: (
    b: number,
    n: number,
    v: Versioned<D.InspectionExecutionDto>,
    input: D.RequestReworkRequest,
    o?: CallOptions,
  ) =>
    write<D.HousekeepingWriteDto>(
      `${inspection(b, n)}/reworks`,
      "POST",
      pick(input, ["assigneeId", "dueAt", "defectIds", "reason"]),
      o,
      match(v, `${inspection(b, n)}/execution`),
    ),
};
