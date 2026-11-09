import {
  decodeRecord,
  safeId,
  strongTag,
  type StructureKind,
  type Snapshot,
  type Failure,
} from "./contracts";
export interface DependencyPreview extends Snapshot {
  dependencies: readonly { kind: string; count: number }[];
  writerCoordinationRequired: true;
}
const known = new Set([
  "ACTIVE_ROOM",
  "ACTIVE_CHILD_AREA",
  "ACTIVE_ASSET",
  "OPEN_INCIDENT",
  "OPEN_OPERATIONAL_TASK",
  "TASK_COMPLETION_CONTRACT_UNRESOLVED",
  "OPEN_MAINTENANCE_WORK",
]);
/** Preview is not authority to mutate; dependencies may change after this read transaction. */
export function createDeactivationPreviewApi(
  fetcher: typeof fetch = globalThis.fetch.bind(globalThis),
) {
  return {
    async read(
      kind: StructureKind,
      branchId: number,
      id: number,
      signal?: AbortSignal,
    ): Promise<
      { ok: true; data: DependencyPreview } | { ok: false; error: Failure }
    > {
      if (
        !safeId(branchId) ||
        !safeId(id) ||
        (kind !== "floor" && kind !== "area")
      )
        return { ok: false, error: { reason: "INVALID_REQUEST" } };
      try {
        const r = await fetcher(
          `/api/branches/${branchId}/${kind}s/${id}/deactivation-dependencies`,
          {
            method: "GET",
            credentials: "include",
            cache: "no-store",
            redirect: "error",
            signal,
            headers: { Accept: "application/json" },
          },
        );
        if (signal?.aborted)
          return { ok: false, error: { reason: "CANCELLED" } };
        if (r.status !== 200)
          return { ok: false, error: { reason: "HTTP", status: r.status } };
        if (
          r.headers.get("Content-Type")?.split(";")[0].trim().toLowerCase() !==
          "application/json"
        )
          return { ok: false, error: { reason: "INVALID_RESPONSE" } };
        let v: unknown;
        try {
          v = await r.json();
        } catch {
          return {
            ok: false,
            error: {
              reason: signal?.aborted ? "CANCELLED" : "INVALID_RESPONSE",
            },
          };
        }
        if (signal?.aborted)
          return { ok: false, error: { reason: "CANCELLED" } };
        if (!v || typeof v !== "object")
          return { ok: false, error: { reason: "INVALID_RESPONSE" } };
        const x = v as Record<string, unknown>,
          record = decodeRecord(x.record, kind, branchId, id),
          etag = r.headers.get("ETag");
        if (
          !record ||
          !strongTag(etag) ||
          x.writerCoordinationRequired !== true ||
          !Array.isArray(x.dependencies) ||
          x.dependencies.length > 7
        )
          return { ok: false, error: { reason: "INVALID_RESPONSE" } };
        const ds = x.dependencies;
        if (
          !ds.every(
            (d) =>
              d &&
              typeof d === "object" &&
              known.has(d.kind) &&
              safeId(d.count) &&
              Array.isArray(d.ids) &&
              d.ids.length <= 100 &&
              d.ids.every(safeId),
          ) ||
          new Set(ds.map((d) => d.kind)).size !== ds.length
        )
          return { ok: false, error: { reason: "INVALID_RESPONSE" } };
        return {
          ok: true,
          data: {
            record,
            etag,
            writerCoordinationRequired: true,
            dependencies: ds.map((d) => ({ kind: d.kind, count: d.count })),
          },
        };
      } catch {
        return {
          ok: false,
          error: { reason: signal?.aborted ? "CANCELLED" : "NETWORK" },
        };
      }
    },
  };
}
