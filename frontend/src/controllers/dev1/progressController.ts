import type * as D from "./contracts";
import {
  branchPath,
  id,
  pick,
  readVersioned,
  write,
  match,
  download,
  type CallOptions,
  type Versioned,
} from "./transportController";
import { ApiError } from "../../api/http";
export type ProgressKind = "cleaning" | "task";
function path(b: number, n: number, kind: ProgressKind) {
  if (kind !== "cleaning" && kind !== "task")
    throw new ApiError("Invalid progress kind", 400, "validation");
  return `${branchPath(b)}/${kind === "cleaning" ? "cleaning-tasks" : "operational-tasks"}/${id(n)}/progress`;
}
export function photos(input: D.EvidenceUpload[] | null | undefined) {
  return input?.map((p) => pick(p, ["name", "mimeType", "base64"])) ?? input;
}
const definition = (input: D.InitializeProgressRequest) => ({
  reason: input.reason,
  items: input.items?.map((i) => pick(i, ["label", "required"])) ?? null,
});
export const progressController = {
  setCleaningItems: (
    b: number,
    n: number,
    input: D.InitializeProgressRequest,
    o?: CallOptions,
  ) =>
    write<D.InitializedProgress>(
      `${branchPath(b)}/room-types/${id(n)}/cleaning-items`,
      "POST",
      definition(input),
      o,
    ),
  initializeCleaning: (b: number, n: number, reason: string, o?: CallOptions) =>
    write<D.InitializedProgress>(
      `${path(b, n, "cleaning")}-initialization`,
      "POST",
      { items: null, reason },
      o,
    ),
  initializeTask: (
    b: number,
    n: number,
    input: D.InitializeProgressRequest,
    o?: CallOptions,
  ) =>
    write<D.InitializedProgress>(
      `${path(b, n, "task")}-initialization`,
      "POST",
      definition(input),
      o,
    ),
  detail: (b: number, n: number, kind: ProgressKind, o?: CallOptions) =>
    readVersioned<D.WorkProgressDto>(path(b, n, kind), o),
  save: (
    b: number,
    n: number,
    kind: ProgressKind,
    v: Versioned<D.WorkProgressDto>,
    input: D.SaveProgressRequest,
    o?: CallOptions,
  ) =>
    write<D.WorkProgressDto>(
      path(b, n, kind),
      "PUT",
      {
        percent: input.percent,
        note: input.note,
        items: input.items.map((i) =>
          pick(i, ["id", "done", "outcome", "note"]),
        ),
        photos: photos(input.photos),
      },
      o,
      match(v, path(b, n, kind)),
    ),
  evidence: (
    b: number,
    n: number,
    kind: ProgressKind,
    attachment: number,
    o?: CallOptions,
  ) => download(`${path(b, n, kind)}/evidence/${id(attachment)}`, o),
};
