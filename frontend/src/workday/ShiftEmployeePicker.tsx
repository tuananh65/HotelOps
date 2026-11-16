import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { createHttpClient, isObject } from "../auth/http";
import { safeId } from "./contracts";

type Employee = { id: number; name: string };
type Page = { branchId: number; departmentId: number; employees: Employee[]; nextCursor: number | null };
const employee = (v: unknown): v is Employee =>
  isObject(v) && safeId(v.id) && typeof v.name === "string";

export function ShiftEmployeePicker({ branchId, departmentId, disabled, onSelect }: {
  branchId: string; departmentId: string; disabled: boolean; onSelect: (id: string) => void;
}) {
  const { state, actions } = useAuth();
  const owner = state.authorityGeneration;
  const [page, setPage] = useState<Page | null>(null);
  const [message, setMessage] = useState("");
  const [pending, setPending] = useState(false);
  const request = useRef<AbortController | null>(null);
  const generation = useRef(0);
  useEffect(() => {
    generation.current++;
    request.current?.abort();
    setPage(null); setMessage(""); setPending(false);
    return () => { generation.current++; request.current?.abort(); };
  }, [branchId, departmentId, owner]);

  async function load(after = 0) {
    const branch = Number(branchId), department = Number(departmentId);
    if (disabled || pending || !safeId(branch) || !safeId(department)) return;
    request.current?.abort();
    const controller = new AbortController(); request.current = controller;
    const attempt = ++generation.current;
    setPending(true); setPage(null); setMessage("");
    try {
      const result = await createHttpClient()({ method: "GET",
        url: `/api/branches/${branch}/departments/${department}/shift-roster/employees?afterId=${after}`,
        expectedStatus: 200, signal: controller.signal,
        decode(v) {
          if (!isObject(v) || v.branchId !== branch || v.departmentId !== department
            || !Array.isArray(v.employees) || v.employees.length > 50 || !v.employees.every(employee)
            || !(v.nextCursor === null || safeId(v.nextCursor))) return;
          return v as Page;
        },
      });
      if (controller.signal.aborted || attempt !== generation.current) return;
      if (result.ok) {
        setPage(result.data);
        if (!result.data.employees.length) setMessage("No active employees with current membership in this department.");
      } else {
        setMessage("Employee list unavailable. Current department authority and SHIFT_MANAGE are required.");
        if (result.error.kind === "http" && result.error.status === 401) actions.invalidateSession(owner);
      }
    } finally {
      if (!controller.signal.aborted && attempt === generation.current) setPending(false);
    }
  }
  return <fieldset disabled={disabled || pending}>
    <legend>Employees in the selected branch and department</legend>
    <button type="button" disabled={!safeId(Number(branchId)) || !safeId(Number(departmentId))} onClick={() => void load()}>Load employees</button>
    {page && <>
      <label>Choose employee<select value="" onChange={e => onSelect(e.target.value)}>
        <option value="">Select an employee</option>
        {page.employees.map(e => <option key={e.id} value={e.id}>{e.name} (#{e.id})</option>)}
      </select></label>
      {page.nextCursor !== null && <button type="button" onClick={() => void load(page.nextCursor!)}>Next employees</button>}
    </>}
    {pending && <p role="status">Loading employees...</p>}
    {message && <p role="status">{message}</p>}
  </fieldset>;
}
