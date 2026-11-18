import { createContext, useContext, useEffect, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";
import { safeId } from "./contracts";

export type PlanningScope = {
  branchId: number;
  branchName: string;
  departmentId: number;
  departmentName: string;
  timezone: string;
};
export type PlanningBranch = {
  id: number;
  name: string;
  timezone: string;
  departments: { id: number; name: string }[];
};
export type PlanningTemplate = {
  id: number;
  name: string;
  start: string;
  end: string;
  crossesMidnight: boolean;
  isActive: boolean;
};
export type PlanningEmployee = { id: number; name: string };
export const ShiftPlanningContext = createContext<{
  scope: PlanningScope;
  reportBusy: (panel: string, busy: boolean) => void;
  reportDrafts: (dirty: boolean) => void;
} | null>(null);
export function usePlanningBusy(panel: string, busy: boolean) {
  const report = useContext(ShiftPlanningContext)?.reportBusy;
  useEffect(() => {
    report?.(panel, busy);
    return () => report?.(panel, false);
  }, [report, panel, busy]);
}
export function localDate(value = new Date()) {
  return new Intl.DateTimeFormat("en-CA", {
    timeZone: "Asia/Ho_Chi_Minh",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(value);
}
export function planningDateTime(value: string, timezone = "Asia/Ho_Chi_Minh") {
  try {
    return new Intl.DateTimeFormat("vi-VN", {
      timeZone: timezone,
      dateStyle: "short",
      timeStyle: "short",
    }).format(new Date(value));
  } catch {
    return "Chưa xác định thời gian";
  }
}
export function validPlanningBranches(
  value: unknown,
): value is PlanningBranch[] {
  return (
    Array.isArray(value) &&
    value.every(
      (b) =>
        isObject(b) &&
        safeId(b.id) &&
        typeof b.name === "string" &&
        typeof b.timezone === "string" &&
        Array.isArray(b.departments) &&
        b.departments.every(
          (d) => isObject(d) && safeId(d.id) && typeof d.name === "string",
        ) &&
        new Set(b.departments.map((d) => d.id)).size === b.departments.length,
    ) &&
    new Set(value.map((b) => b.id)).size === value.length
  );
}
/** Scoped GET only. Discard all partial pages on error or a changed scope. */
export function usePlanningChoices(scope: PlanningScope | undefined) {
  const { state, actions } = useAuth();
  const [revision, setRevision] = useState(0);
  const [data, setData] = useState<{
    templates: PlanningTemplate[];
    employees: PlanningEmployee[];
  } | null>(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const branch = scope?.branchId,
    department = scope?.departmentId,
    owner = state.authorityGeneration;
  useEffect(() => {
    setData(null);
    setError("");
    if (!branch || !department) return;
    const controller = new AbortController();
    setLoading(true);
    async function read(kind: "shift-definitions" | "shift-roster/employees") {
      const rows: unknown[] = [];
      let after = 0;
      for (let page = 0; page < 100; page++) {
        const response = await fetch(
          `/api/branches/${branch}/departments/${department}/${kind}?afterId=${after}`,
          {
            credentials: "same-origin",
            cache: "no-store",
            redirect: "error",
            signal: controller.signal,
          },
        );
        if (response.status === 401) actions.invalidateSession(owner);
        if (!response.ok) throw Error();
        const value: unknown = await response.json();
        if (kind === "shift-definitions") {
          if (
            !Array.isArray(value) ||
            !value.every(
              (t) =>
                isObject(t) &&
                safeId(t.id) &&
                typeof t.name === "string" &&
                typeof t.start === "string" &&
                typeof t.end === "string" &&
                typeof t.crossesMidnight === "boolean" &&
                typeof t.isActive === "boolean",
            )
          )
            throw Error();
          rows.push(...value);
          if (value.length < 100) return rows as PlanningTemplate[];
          const next = value.at(-1)!.id as number;
          if (next <= after) throw Error();
          after = next;
        } else {
          if (
            !isObject(value) ||
            value.branchId !== branch ||
            value.departmentId !== department ||
            !Array.isArray(value.employees) ||
            !value.employees.every(
              (e) => isObject(e) && safeId(e.id) && typeof e.name === "string",
            ) ||
            !(value.nextCursor === null || safeId(value.nextCursor))
          )
            throw Error();
          rows.push(...value.employees);
          if (value.nextCursor === null) return rows as PlanningEmployee[];
          if (Number(value.nextCursor) <= after) throw Error();
          after = Number(value.nextCursor);
        }
      }
      throw Error();
    }
    void Promise.all([
      read("shift-definitions"),
      read("shift-roster/employees"),
    ])
      .then(([templates, employees]) => {
        if (!controller.signal.aborted)
          setData({
            templates: (templates as PlanningTemplate[]).filter(
              (t) => t.isActive,
            ),
            employees: employees as PlanningEmployee[],
          });
      })
      .catch(() => {
        if (!controller.signal.aborted)
          setError(
            "Không tải được danh sách ca và nhân viên. Vui lòng thử lại.",
          );
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [branch, department, owner, revision, actions]);
  return { data, error, loading, reload: () => setRevision((v) => v + 1) };
}
