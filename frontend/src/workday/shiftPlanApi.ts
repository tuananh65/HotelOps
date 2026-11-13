import { createHttpClient } from "../auth/http";
import {
  draftSnapshot,
  decodePreview,
  type PreviewAdapter,
  type ShiftDraft,
} from "./shiftPlanPreview";
export class ShiftPlanApiError extends Error {
  constructor(readonly status?: number) {
    super("Shift plan unavailable");
  }
}
export type ShiftPlanSaveOutcome =
  | {
      ok: true;
      createdShifts: number;
      createdAssignments: number;
      notificationCount: number;
    }
  | {
      ok: false;
      outcome: "NOT_DISPATCHED" | "REJECTED" | "OUTCOME_UNKNOWN";
      status?: number;
    };
export type SavePlanAdapter = (
  drafts: readonly ShiftDraft[],
  signal: AbortSignal,
) => Promise<ShiftPlanSaveOutcome>;
const obj = (v: unknown): v is Record<string, unknown> =>
  typeof v === "object" && v !== null && !Array.isArray(v);
export function createShiftPlanApi(
  fetcher: typeof fetch = globalThis.fetch.bind(globalThis),
  saveEnabled = false,
) {
  let busy = false;
  async function call(
    drafts: readonly ShiftDraft[],
    signal: AbortSignal,
    save: boolean,
  ) {
    const snapshot = draftSnapshot(drafts);
    if (
      busy ||
      !snapshot ||
      snapshot.some(
        (d) =>
          d.branchId !== snapshot[0].branchId ||
          d.departmentId !== snapshot[0].departmentId,
      ) ||
      signal.aborted ||
      (save && !saveEnabled)
    )
      return { ok: false as const, outcome: "NOT_DISPATCHED" as const };
    busy = true;
    let dispatched = false;
    const body = JSON.stringify({
      drafts: snapshot.map(({ employeeId, templateId, shiftDate }) => ({
        employeeId,
        templateId,
        shiftDate,
      })),
    });
    try {
      let bootstrapFailureStatus: number | undefined;
      const http = createHttpClient(async (url, options) => {
        const response = await fetcher(url, options);
        if (response.status >= 400 && response.status <= 599)
          bootstrapFailureStatus = response.status;
        return response;
      });
      const csrf = await http({
        method: "GET",
        url: "/api/auth/csrf",
        expectedStatus: 200,
        signal,
        decode(v) {
          if (
            obj(v) &&
            v.headerName === "X-CSRF-TOKEN" &&
            typeof v.csrfToken === "string" &&
            /^[\x21-\x7e]{1,8192}$/.test(v.csrfToken)
          )
            return v.csrfToken;
        },
      });
      if (!csrf.ok)
        return {
          ok: false as const,
          outcome: "NOT_DISPATCHED" as const,
          ...(csrf.error.kind === "http"
            ? { status: csrf.error.status }
            : bootstrapFailureStatus !== undefined
              ? { status: bootstrapFailureStatus }
              : {}),
        };
      if (signal.aborted)
        return { ok: false as const, outcome: "NOT_DISPATCHED" as const };
      dispatched = true;
      const r = await fetcher(
        `/api/branches/${snapshot[0].branchId}/departments/${snapshot[0].departmentId}/shift-plans${save ? "" : "/preview"}`,
        {
          method: "POST",
          credentials: "include",
          cache: "no-store",
          redirect: "error",
          signal,
          headers: {
            Accept: "application/json",
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": csrf.data,
          },
          body,
        },
      );
      let v: unknown;
      try {
        if (
          ["application/json", "application/problem+json"].includes(
            r.headers.get("Content-Type")?.split(";")[0].trim().toLowerCase() ??
              "",
          )
        )
          v = await r.json();
      } catch {}
      if (signal.aborted)
        return { ok: false as const, outcome: "OUTCOME_UNKNOWN" as const };
      if (!save && r.status === 200) {
        const preview = decodePreview(v, snapshot.length);
        if (preview) return { ok: true as const, value: preview };
      }
      if (
        save &&
        r.status === 201 &&
        obj(v) &&
        Number.isInteger(v.createdShifts) &&
        Number(v.createdShifts) > 0 &&
        Number(v.createdShifts) <= snapshot.length &&
        v.createdAssignments === snapshot.length &&
        v.notificationCount === snapshot.length
      )
        return {
          ok: true as const,
          value: {
            createdShifts: Number(v.createdShifts),
            createdAssignments: snapshot.length,
            notificationCount: snapshot.length,
          },
        };
      return {
        ok: false as const,
        outcome: ([400, 401, 403, 404, 412, 428].includes(r.status) ||
        (r.status === 409 &&
          obj(v) &&
          v.status === 409 &&
          v.title === "SHIFT_PLAN_CONFLICT" &&
          (!("code" in v) || v.code === v.title))
          ? "REJECTED"
          : "OUTCOME_UNKNOWN") as "REJECTED" | "OUTCOME_UNKNOWN",
        status: r.status,
      };
    } catch {
      return {
        ok: false as const,
        outcome: (dispatched ? "OUTCOME_UNKNOWN" : "NOT_DISPATCHED") as
          "OUTCOME_UNKNOWN" | "NOT_DISPATCHED",
      };
    } finally {
      busy = false;
    }
  }
  const preview: PreviewAdapter = async (drafts, signal) => {
    const r = await call(drafts, signal, false);
    if (!r.ok) throw new ShiftPlanApiError(r.status);
    return r.value;
  };
  const save: SavePlanAdapter = async (drafts, signal) => {
    const r = await call(drafts, signal, true);
    if (!r.ok) return r;
    const value = r.value;
    if ("createdShifts" in value) return { ok: true, ...value };
    return { ok: false, outcome: "OUTCOME_UNKNOWN" };
  };
  return { preview, save };
}
