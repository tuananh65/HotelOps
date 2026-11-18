import { displayCode } from "../i18n/vi";
import { useEffect, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import type { Dashboard } from "./dashboardApi";

type Records = { missingSources: string[]; totalCount: number; page: number; pageSize: number; snapshot: string;
  records: { columns: string[]; rows: Record<string, string | number | boolean | null>[] } };
const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === "object" && !Array.isArray(v);

export function DashboardSourceRecords({ source, metric, filters, close }: {
  source: string; metric?: string; filters: Dashboard["filters"]; close: () => void;
}) {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && state.pending === null ? state.authorityGeneration : null;
  const [branch, setBranch] = useState(String(filters.branchIds[0] ?? ""));
  const [page, setPage] = useState(1);
  const [refresh, setRefresh] = useState(0);
  const key = JSON.stringify([source, metric, filters, branch, page, refresh]);
  const [result, setResult] = useState<{ owner: typeof owner; key: string; data?: Records; error?: string } | null>(null);
  useEffect(() => {
    setResult(null);
    if (!owner || !branch) return;
    const controller = new AbortController();
    const params = new URLSearchParams({ branchId: branch, from: filters.from, to: filters.to, page: String(page), pageSize: "25" });
    if (metric) params.set("metric", metric);
    if (filters.departmentId !== null) params.set("departmentId", String(filters.departmentId));
    void fetch(`/api/dashboards/sources/${encodeURIComponent(source)}?${params}`, {
      credentials: "same-origin", cache: "no-store", redirect: "error", signal: controller.signal,
      headers: { Accept: "application/json" },
    }).then(async response => {
      if (response.status === 401) { actions.invalidateSession(owner); throw new Error("SESSION_EXPIRED"); }
      if (response.status === 403) throw new Error("ACCESS_DENIED: Cần cả quyền xem tổng quan và quyền xem dữ liệu nguồn trong chi nhánh.");
      if (!response.ok) throw new Error("Hồ sơ nguồn chưa khả dụng.");
      const v: unknown = await response.json();
      if (!object(v) || !Array.isArray(v.missingSources) || !v.missingSources.every(s => typeof s === "string") || v.page !== page || v.pageSize !== 25 || !Number.isSafeInteger(v.totalCount) || Number(v.totalCount) < 0
        || v.source !== source || !object(v.filters) || !Array.isArray(v.filters.branchIds)
        || v.filters.branchIds.length !== 1 || v.filters.branchIds[0] !== Number(branch)
        || v.filters.departmentId !== filters.departmentId || v.filters.from !== filters.from || v.filters.to !== filters.to
        || typeof v.snapshot !== "string" || !object(v.records) || !Array.isArray(v.records.columns)
        || !v.records.columns.every(c => typeof c === "string") || !Array.isArray(v.records.rows)
        || !v.records.rows.every(r => object(r) && (v.records as Records["records"]).columns.every(c =>
          Object.hasOwn(r, c) && (r[c] === null || ["string", "boolean", "number"].includes(typeof r[c])))))
        throw new Error("Phản hồi nguồn không hợp lệ.");
      if (!controller.signal.aborted) setResult({ owner, key, data: v as unknown as Records });
    }).catch((e: unknown) => {
      if (!controller.signal.aborted) setResult({ owner, key, error: e instanceof Error ? e.message : "Nguồn chưa khả dụng." });
    });
    // Drop details before every renewed admission, including focus and session changes.
    const recheck = () => { setResult(null); setRefresh(n => n + 1); };
    const timer = window.setInterval(recheck, 30000);
    window.addEventListener("focus", recheck);
    return () => { controller.abort(); clearInterval(timer); window.removeEventListener("focus", recheck); };
  }, [owner, key, branch, source, metric, filters, page, actions]);
  const visible = result?.owner === owner && result.key === key ? result : null;
  return <section aria-label="Hồ sơ nguồn tổng quan được phép xem">
    <h2>{source} Hồ sơ</h2><button onClick={close}>Đóng hồ sơ</button>
    <p>Quyền nguồn được kiểm tra riêng. Mở danh sách này không cấp quyền chỉnh sửa.</p>
    <label>Chi nhánh nguồn<select value={branch} onChange={e => { setResult(null); setBranch(e.target.value); setPage(1); }}>
      {filters.branchIds.map(id => <option key={id} value={id}>{displayCode(id)}</option>)}
    </select></label>
    {visible?.error && <p role="alert">{visible.error}</p>}
    {!visible && <p role="status">Đang kiểm tra quyền truy cập nguồn hiện tại...</p>}
    {visible?.data && <>
      {visible.data.missingSources.map(note => <p role="status" key={note}>{note}</p>)}
      <p>{visible.data.totalCount} hồ sơ; bản chụp dữ liệu {visible.data.snapshot}. Selected period: {filters.from} đến {filters.to}.</p>
      {visible.data.records.rows.length === 0 ? <p>Không có hồ sơ phù hợp.</p> : <div style={{ overflowX: "auto" }}><table>
        <thead><tr>{visible.data.records.columns.map(c => <th key={c}>{c}</th>)}</tr></thead>
        <tbody>{visible.data.records.rows.map((r, i) => <tr key={i}>{visible.data!.records.columns.map(c =>
          <td key={c}>{r[c] === null ? "Chưa có thông tin" : String(r[c])}</td>)}</tr>)}</tbody>
      </table></div>}
      <button disabled={page <= 1} onClick={() => { setResult(null); setPage(p => p - 1); }}>Trước</button>
      <button disabled={page * 25 >= visible.data.totalCount} onClick={() => { setResult(null); setPage(p => p + 1); }}>Tiếp</button>
    </>}
  </section>;
}
