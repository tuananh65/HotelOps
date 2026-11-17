import { useEffect, useState, type FormEvent } from "react";
import type { AuthorityGeneration } from "../auth/authStore";
import {
  DashboardFailure,
  readDashboardOptions,
  type DashboardOptions,
} from "./dashboardApi";

export function DashboardFilters({
  owner,
  roomsOnly = false,
  invalidate,
  apply,
}: {
  owner: AuthorityGeneration;
  roomsOnly?: boolean;
  invalidate: (owner: AuthorityGeneration) => void;
  apply: (query: string) => void;
}) {
  const [retry, setRetry] = useState(0);
  const [result, setResult] = useState<{
    owner: AuthorityGeneration;
    options?: DashboardOptions;
    error?: string;
  } | null>(null);
  const [draft, setDraft] = useState({
    branchId: "",
    departmentId: "",
    from: "",
    to: "",
  });
  useEffect(() => {
    const controller = new AbortController();
    setResult(null);
    setDraft({ branchId: "", departmentId: "", from: "", to: "" });
    void readDashboardOptions(controller.signal, roomsOnly)
      .then((options) => {
        if (!controller.signal.aborted) setResult({ owner, options });
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) return;
        setResult({
          owner,
          error:
            "Dashboard filters are unavailable. Retry to load permitted choices.",
        });
        if (error instanceof DashboardFailure && error.status === 401)
          invalidate(owner);
      });
    return () => controller.abort();
  }, [owner, retry, invalidate, roomsOnly]);
  const visible = result?.owner === owner ? result : null;
  const options = visible?.options;
  const departments =
    options?.departments.filter(
      (d) => !draft.branchId || String(d.branchId) === draft.branchId,
    ) ?? [];
  function submit(event: FormEvent) {
    event.preventDefault();
    if (
      !options ||
      (draft.branchId &&
        !options.branches.some((b) => String(b.id) === draft.branchId)) ||
      (draft.departmentId &&
        !departments.some((d) => String(d.id) === draft.departmentId))
    )
      return;
    const query = new URLSearchParams();
    for (const [key, value] of Object.entries(draft))
      if (value) query.set(key, value);
    apply(query.toString());
  }
  return (
    <section aria-label="Dashboard scope">
      {visible?.error && (
        <>
          <p role="status">{visible.error}</p>
          <button onClick={() => setRetry((n) => n + 1)}>Retry filters</button>
        </>
      )}
      {!visible && <p role="status">Loading permitted filters…</p>}
      <form onSubmit={submit} aria-label="Dashboard filters">
        <fieldset disabled={!options}>
          <legend>Filters</legend>
          <label>
            Branch
            <select
              aria-label="Branch"
              value={options ? draft.branchId : ""}
              onChange={(e) =>
                setDraft((d) => ({
                  ...d,
                  branchId: e.target.value,
                  departmentId: "",
                }))
              }
            >
              <option value="">All permitted branches</option>
              {options?.branches.map((b) => (
                <option key={b.id} value={b.id}>
                  {b.name} ({b.code})
                </option>
              ))}
            </select>
          </label>
          <label>
            Department
            <select
              aria-label="Department"
              value={options ? draft.departmentId : ""}
              onChange={(e) =>
                setDraft((d) => ({ ...d, departmentId: e.target.value }))
              }
            >
              <option value="">All permitted departments</option>
              {departments.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name} —{" "}
                  {options?.branches.find((b) => b.id === d.branchId)?.name}
                </option>
              ))}
            </select>
          </label>
          <label>
            From (UTC)
            <input
              type="date"
              value={draft.from}
              max={draft.to || undefined}
              onChange={(e) =>
                setDraft((d) => ({ ...d, from: e.target.value }))
              }
            />
          </label>
          <label>
            To (UTC)
            <input
              type="date"
              value={draft.to}
              min={draft.from || undefined}
              onChange={(e) => setDraft((d) => ({ ...d, to: e.target.value }))}
            />
          </label>
          <button type="submit">Apply filters</button>
        </fieldset>
      </form>
    </section>
  );
}
