import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";

type BranchOptions = { timezone: string; pageSize: number; branches: { id: number; code: string; name: string; isActive: boolean }[] };
export function useBranchManagementOptions() {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const [result, setResult] = useState<{ owner: typeof owner; data?: BranchOptions; error?: string } | null>(null);
  useEffect(() => {
    setResult(null); if (!owner) return;
    const abort = new AbortController();
    void (async () => {
      const response = await fetch("/api/branches/management-options", { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      if (response.status === 401) actions.invalidateSession(owner);
      if (!response.ok) throw new Error(response.status === 403 ? "ACCESS_DENIED — Không có quyền quản lý chi nhánh." : `Không tải được danh mục (${response.status}).`);
      const data = await response.json() as BranchOptions;
      if (!data.timezone || !Array.isArray(data.branches) || !Number.isInteger(data.pageSize) || data.pageSize<1 || data.pageSize>100) throw new Error("Dữ liệu danh mục không hợp lệ.");
      if (!abort.signal.aborted) setResult({ owner, data });
    })().catch(e => { if (!abort.signal.aborted) setResult({ owner, error: e instanceof Error ? e.message : "Không tải được danh mục." }); });
    return () => abort.abort();
  }, [owner, actions]);
  return owner && result?.owner === owner ? result : null;
}

export function BranchManagementPage() {
  const result = useBranchManagementOptions();
  const [page,setPage]=useState(1);
  if (!result) return <p>Đang kiểm tra quyền quản lý chi nhánh…</p>;
  if (result.error) return <p role="alert">{result.error}</p>;
  return <section><h2>Quản lý chi nhánh</h2><p>Múi giờ hệ thống: {result.data?.timezone}</p>
    <Link to="/branches/new">Tạo chi nhánh</Link>
    <ul>{result.data?.branches.slice((page-1)*result.data.pageSize,page*result.data.pageSize).map(b => <li key={b.id}><Link to={`/branches/${b.id}/edit`}>{b.code} — {b.name}</Link> ({b.isActive ? "Đang hoạt động" : "Ngừng hoạt động"})</li>)}</ul>
    <button disabled={page<=1} onClick={()=>setPage(p=>p-1)}>Trước</button>
    <span> Trang {page} </span>
    <button disabled={!result.data||page*result.data.pageSize>=result.data.branches.length} onClick={()=>setPage(p=>p+1)}>Sau</button>
  </section>;
}
