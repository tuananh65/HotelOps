import { useEffect, useState } from "react";
import { createHttpClient, isObject } from "../auth/http";

type Category = { id: number; code: string; name: string };
const request = createHttpClient();
function decode(value: unknown): Category[] | undefined {
  if (!Array.isArray(value)) return undefined;
  const rows: Category[] = [];
  const ids = new Set<number>();
  for (const row of value) {
    if (!isObject(row) || typeof row.id !== "number" || !Number.isSafeInteger(row.id)
      || row.id <= 0 || ids.has(row.id) || typeof row.code !== "string" || typeof row.name !== "string") return undefined;
    ids.add(row.id);
    rows.push({ id: row.id, code: row.code, name: row.name });
  }
  return rows;
}

export function IncidentCategoryField({ branch, value, onChange }: {
  branch: unknown; value: unknown; onChange: (value: string) => void;
}) {
  const branchId = String(branch ?? "");
  const [loaded, setLoaded] = useState<{ branch: string; rows: Category[]; message: string }>();
  useEffect(() => {
    if (!/^[1-9]\d*$/.test(branchId) || !Number.isSafeInteger(Number(branchId))) return;
    const controller = new AbortController();
    void request({ method: "GET", url: `/api/incidents/categories/${branchId}`,
      expectedStatus: 200, decode, signal: controller.signal }).then(result => {
      if (controller.signal.aborted) return;
      setLoaded({ branch: branchId, rows: result.ok ? result.data : [],
        message: result.ok ? (result.data.length ? "" : "No active incident categories configured.")
          : "Category list unavailable. Current incident read access is required." });
    });
    return () => controller.abort();
  }, [branchId]);
  const current = loaded?.branch === branchId ? loaded : undefined;
  return <label>Category (optional)
    <select name="categoryId" value={String(value ?? "")} disabled={!current?.rows.length}
      onChange={event => onChange(event.target.value)}>
      <option value="">No category</option>
      {current?.rows.map(row => <option key={row.id} value={row.id}>{row.name}</option>)}
    </select>
    <small>{current?.message ?? (branchId ? "Loading categories…" : "Select a branch to load categories.")}</small>
  </label>;
}
